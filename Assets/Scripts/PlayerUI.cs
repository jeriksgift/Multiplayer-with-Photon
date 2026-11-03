using UnityEngine;
using TMPro;
using Photon.Pun;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerUI : MonoBehaviourPunCallbacks
{
    public static bool isReturningFromMatch = false;

    //[SerializeField] private PhotonView photonView;
    [SerializeField] private TextMeshProUGUI roomNameTxt;
    public TextMeshProUGUI worldNameText;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TextMeshProUGUI blueScoreText;
    [SerializeField] private TextMeshProUGUI redScoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject matchEndPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI endScoreText;
    [SerializeField] private Button exitToLobbyButton;

    private HealthComponent healthComponent;

    // Prevent multiple exit requests.
    private bool isExitingToLobby = false;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
    }

    private void Start()
    {
        string nick = photonView != null && photonView.Owner != null
            ? photonView.Owner.NickName
            : string.Empty;

        string roomName = PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.Name
            : "No Room";

        if (roomNameTxt != null)
            roomNameTxt.text = roomName.ToUpperInvariant();

        if (worldNameText != null &&
            photonView != null &&
            !photonView.IsMine)
        {
            worldNameText.text = nick;
        }

        ApplyTeamColor();

        if (healthComponent != null)
        {
            healthComponent.HealthChanged += OnHealthChanged;

            // Initialize the health bar.
            OnHealthChanged(1f);
        }

        GameManager.OnScoreChanged += OnScoreChanged;
        GameManager.OnTimerChanged += OnTimerChanged;
        GameManager.OnMatchEnded += OnMatchEnded;

        // Initialize the team scores.
        if (GameManager.Instance != null)
        {
            UpdateTeamScores(
                GameManager.Instance.blueKills,
                GameManager.Instance.redKills
            );
        }

        if (photonView != null && photonView.IsMine)
        {
            DestroyOtherCanvases();

            if (worldNameText != null)
                worldNameText.gameObject.SetActive(false);
        }
        else
        {
            // Hide the screen-space HUD for non-local players.
            Canvas canvas = GetComponentInChildren<Canvas>();

            if (canvas != null &&
                canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (healthComponent != null)
            healthComponent.HealthChanged -= OnHealthChanged;

        GameManager.OnScoreChanged -= OnScoreChanged;
        GameManager.OnTimerChanged -= OnTimerChanged;
        GameManager.OnMatchEnded -= OnMatchEnded;
    }

    private void OnScoreChanged(int blue, int red)
    {
        UpdateTeamScores(blue, red);
    }

    private void OnTimerChanged(float time)
    {
        if (timerText == null)
            return;

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        timerText.text = string.Format(
            "{0:00}:{1:00}",
            minutes,
            seconds
        );
    }

    private void OnMatchEnded(int winner, string playerStats)
    {
        if (matchEndPanel == null)
            return;

        matchEndPanel.SetActive(true);

        // Unlock the cursor for UI interaction.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (winnerText != null)
        {
            if (winner == 1)
                winnerText.text = "Blue Team Wins!";
            else if (winner == 2)
                winnerText.text = "Red Team Wins!";
            else
                winnerText.text = "Draw!";
        }

        // Update the scores displayed in the match-end panels.
        Transform bluePanel =
            matchEndPanel.transform.Find("BlueTeamPanel");

        Transform redPanel =
            matchEndPanel.transform.Find("RedTeamPanel");

        if (bluePanel != null)
        {
            TextMeshProUGUI blueEndScore =
                bluePanel.Find("EndScoreText")
                ?.GetComponent<TextMeshProUGUI>();

            if (blueEndScore != null && GameManager.Instance != null)
            {
                blueEndScore.text =
                    "Kills: " + GameManager.Instance.blueKills;
            }
        }

        if (redPanel != null)
        {
            TextMeshProUGUI redEndScore =
                redPanel.Find("EndScoreText")
                ?.GetComponent<TextMeshProUGUI>();

            if (redEndScore != null && GameManager.Instance != null)
            {
                redEndScore.text =
                    "Kills: " + GameManager.Instance.redKills;
            }
        }

        // Display the per-player statistics.
        FillPlayerStats(playerStats);

        // Register the exit button listener.
        if (exitToLobbyButton != null)
        {
            exitToLobbyButton.onClick.RemoveListener(ExitToLobby);
            exitToLobbyButton.onClick.AddListener(ExitToLobby);
        }
    }

    // =========================================================
    // EXIT TO LOBBY
    // =========================================================

    public void ExitToLobby()
    {
        if (isExitingToLobby)
            return;

        isExitingToLobby = true;

        Debug.Log("Exiting match and returning to lobby...");

        // Prevent automatic scene synchronization during the exit.
        PhotonNetwork.AutomaticallySyncScene = false;
        isReturningFromMatch = true;

        if (PhotonNetwork.InRoom)
        {
            bool leaveRequested = PhotonNetwork.LeaveRoom();
            Debug.Log("LeaveRoom requested: " + leaveRequested);

            if (!leaveRequested)
            {
                Debug.LogError("Failed to initiate Photon LeaveRoom(). Falling back to direct load.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
                isExitingToLobby = false;
                return;
            }

            // Wait for Photon to finish leaving the room before loading lobby
            StartCoroutine(WaitForLeaveRoom());
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            isExitingToLobby = false;
        }
    }

    private System.Collections.IEnumerator WaitForLeaveRoom()
    {
        float timeout = 5f;
        float elapsed = 0f;
        while (PhotonNetwork.InRoom && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("Left room. InRoom=" + PhotonNetwork.InRoom + " after " + elapsed + "s");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        isExitingToLobby = false;
    }

    // =========================================================
    // PLAYER STATISTICS
    // =========================================================

    private void FillPlayerStats(string stats)
    {
        if (string.IsNullOrEmpty(stats))
            return;

        Transform bluePanel =
            matchEndPanel.transform.Find("BlueTeamPanel");

        Transform redPanel =
            matchEndPanel.transform.Find("RedTeamPanel");

        Transform bluePlayers = bluePanel?.Find("Players");
        Transform redPlayers = redPanel?.Find("Players");

        var blueSlots = new System.Collections.Generic.List<Transform>();
        var redSlots = new System.Collections.Generic.List<Transform>();

        if (bluePlayers != null)
        {
            foreach (Transform child in bluePlayers)
                blueSlots.Add(child);
        }

        if (redPlayers != null)
        {
            foreach (Transform child in redPlayers)
                redSlots.Add(child);
        }

        string[] entries = stats.Split(';');

        int blueIdx = 0;
        int redIdx = 0;

        // Hide all player slots without deactivating them.
        foreach (Transform slot in blueSlots)
            SetSlotVisible(slot, false);

        foreach (Transform slot in redSlots)
            SetSlotVisible(slot, false);

        foreach (string entry in entries)
        {
            string[] parts = entry.Split(':');

            if (parts.Length < 4)
                continue;

            if (!int.TryParse(parts[0], out int actor))
                continue;

            if (!int.TryParse(parts[1], out int team))
                continue;

            if (!int.TryParse(parts[2], out int kills))
                continue;

            if (!float.TryParse(
                parts[3],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float dmg))
            {
                continue;
            }

            // Find the player's display name.
            string playerName = "Player " + actor;

            foreach (Photon.Realtime.Player player
                     in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber == actor)
                {
                    playerName = string.IsNullOrEmpty(player.NickName)
                        ? "Player " + actor
                        : player.NickName;

                    break;
                }
            }

            Transform slot = null;

            if (team == 0 && blueIdx < blueSlots.Count)
            {
                slot = blueSlots[blueIdx++];
            }
            else if (team == 1 && redIdx < redSlots.Count)
            {
                slot = redSlots[redIdx++];
            }

            if (slot == null)
                continue;

            // Show the slot.
            SetSlotVisible(slot, true);

            // Player name.
            TextMeshProUGUI nameText =
                slot.Find("Name")?.GetComponent<TextMeshProUGUI>();

            if (nameText != null)
                nameText.text = playerName;

            // Kill count.
            TextMeshProUGUI killsText =
                slot.Find("Kills")?.GetComponent<TextMeshProUGUI>();

            if (killsText != null)
                killsText.text = kills.ToString();

            // Damage dealt.
            TextMeshProUGUI damageText =
                slot.Find("Kills (1)")?.GetComponent<TextMeshProUGUI>();

            if (damageText != null)
                damageText.text = ((int)dmg).ToString();
        }
    }

    // =========================================================
    // HEALTH AND SCORES
    // =========================================================

    private void OnHealthChanged(float normalizedHealth)
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount =
                Mathf.Clamp01(normalizedHealth);
        }
    }

    public void UpdateTeamScores(int blueKills, int redKills)
    {
        if (blueScoreText != null)
            blueScoreText.text = blueKills.ToString();

        if (redScoreText != null)
            redScoreText.text = redKills.ToString();
    }

    // =========================================================
    // WORLD NAME BILLBOARD
    // =========================================================

    private void LateUpdate()
    {
        if (worldNameText != null)
        {
            Camera cam = Camera.main;

            if (cam != null && worldNameText.transform.parent != null)
            {
                worldNameText.transform.parent.rotation =
                    Quaternion.LookRotation(
                        worldNameText.transform.parent.position -
                        cam.transform.position
                    );
            }
        }
    }

    // =========================================================
    // UI SLOT VISIBILITY
    // =========================================================

    private void SetSlotVisible(Transform slot, bool visible)
    {
        if (slot == null)
            return;

        // Change alpha without deactivating the objects.
        Image[] images =
            slot.GetComponentsInChildren<Image>(true);

        foreach (Image img in images)
        {
            Color c = img.color;
            c.a = visible ? 1f : 0f;
            img.color = c;
        }

        TextMeshProUGUI[] texts =
            slot.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI txt in texts)
        {
            Color c = txt.color;
            c.a = visible ? 1f : 0f;
            txt.color = c;
        }
    }

    // =========================================================
    // TEAM COLORS
    // =========================================================

    private void ApplyTeamColor()
    {
        if (photonView == null || photonView.Owner == null)
            return;

        DataTypes.Team playerTeam;

        if (photonView.IsMine)
            playerTeam = RoomManager.GetLocalTeam();
        else
            playerTeam = RoomManager.GetTeam(photonView.Owner);

        Color teamColor =
            playerTeam == DataTypes.Team.Blue
                ? new Color(0.2f, 0.4f, 1f)
                : new Color(1f, 0.2f, 0.2f);

        // Apply the color only to the character body.
        Transform gfx = transform.Find("GFX");

        if (gfx != null)
        {
            MeshRenderer gfxRenderer =
                gfx.GetComponent<MeshRenderer>();

            if (gfxRenderer != null)
            {
                if (gfxRenderer.material != null)
                {
                    gfxRenderer.material.color = teamColor;
                }
                else if (gfxRenderer.sharedMaterial != null)
                {
                    gfxRenderer.sharedMaterial.color = teamColor;
                }
            }
        }

        // Match the player's name color to their team.
        if (worldNameText != null)
            worldNameText.color = teamColor;
    }

    // =========================================================
    // CANVAS MANAGEMENT
    // =========================================================

    /// <summary>
    /// Removes other player HUD canvases while preserving
    /// scene-level canvases that do not belong to a player.
    /// </summary>
    private void DestroyOtherCanvases()
    {
        GameObject[] canvases =
            GameObject.FindGameObjectsWithTag("Canvas");

        foreach (GameObject canvasObject in canvases)
        {
            PhotonView owner = PhotonView.Get(canvasObject);

            // Preserve canvases without an owning PhotonView.
            if (owner == null)
                continue;

            // Preserve the local player's canvas.
            if (owner.IsMine)
                continue;

            // Remove other player HUD canvases.
            Destroy(canvasObject);
        }
    }
}
