using UnityEngine;
using TMPro;
using Photon.Pun;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] private PhotonView photonView;
    [SerializeField] private TextMeshProUGUI roomNameTxt;
    public TextMeshProUGUI worldNameText;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TextMeshProUGUI blueScoreText;
    [SerializeField] private TextMeshProUGUI redScoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject matchEndPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI endScoreText;
    [SerializeField] private UnityEngine.UI.Button exitToLobbyButton;

    private HealthComponent healthComponent;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
    }

    private void Start()
    {
        string nick = photonView.Owner != null ? photonView.Owner.NickName : string.Empty;

        string roomName = PhotonNetwork.CurrentRoom.Name;

        if (roomNameTxt != null)
            roomNameTxt.text = "Room Name: " + roomName;

        if (worldNameText != null && !photonView.IsMine)
            worldNameText.text = nick;

        ApplyTeamColor();

        if (healthComponent != null)
        {
            healthComponent.HealthChanged += OnHealthChanged;
            // Initial health
            OnHealthChanged(1f);
        }

        GameManager.OnScoreChanged += OnScoreChanged;
        GameManager.OnTimerChanged += OnTimerChanged;
        GameManager.OnMatchEnded += OnMatchEnded;

        // Initial score update
        if (GameManager.Instance != null)
            UpdateTeamScores(GameManager.Instance.blueKills, GameManager.Instance.redKills);

        if (photonView.IsMine)
        {
            DestroyOtherCanvases();
            worldNameText.gameObject.SetActive(false);
        }
        else
        {
            // Hide the screen-space HUD for non-local players
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                canvas.gameObject.SetActive(false);
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
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void OnMatchEnded(int winner, string playerStats)
    {
        if (matchEndPanel == null) return;
        matchEndPanel.SetActive(true);

        // Unlock cursor so player can interact with UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (winnerText != null)
        {
            if (winner == 1) winnerText.text = "Blue Team Wins!";
            else if (winner == 2) winnerText.text = "Red Team Wins!";
            else winnerText.text = "Draw!";
        }

        // Update team score texts in the panels
        var bluePanel = matchEndPanel.transform.Find("BlueTeamPanel");
        var redPanel = matchEndPanel.transform.Find("RedTeamPanel");

        if (bluePanel != null)
        {
            var blueScoreText = bluePanel.Find("EndScoreText")?.GetComponent<TextMeshProUGUI>();
            if (blueScoreText != null && GameManager.Instance != null)
                blueScoreText.text = "Kills: " + GameManager.Instance.blueKills;
        }

        if (redPanel != null)
        {
            var redScoreText = redPanel.Find("EndScoreText")?.GetComponent<TextMeshProUGUI>();
            if (redScoreText != null && GameManager.Instance != null)
                redScoreText.text = "Kills: " + GameManager.Instance.redKills;
        }

        // Fill in per-player stats in the pre-built slots
        FillPlayerStats(playerStats);

        // Wire up exit button
        if (exitToLobbyButton != null)
        {
            exitToLobbyButton.onClick.RemoveAllListeners();
            exitToLobbyButton.onClick.AddListener(ExitToLobby);
        }
    }

    public void ExitToLobby()
    {
        if (Photon.Pun.PhotonNetwork.InRoom)
        {
            Photon.Pun.PhotonNetwork.LeaveRoom();
            StartCoroutine(WaitForLeaveRoom());
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        }
    }

    private System.Collections.IEnumerator WaitForLeaveRoom()
    {
        while (Photon.Pun.PhotonNetwork.InRoom)
        {
            yield return null;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    private void FillPlayerStats(string stats)
    {
        if (string.IsNullOrEmpty(stats)) return;

        var bluePanel = matchEndPanel.transform.Find("BlueTeamPanel");
        var redPanel = matchEndPanel.transform.Find("RedTeamPanel");

        var bluePlayers = bluePanel?.Find("Players");
        var redPlayers = redPanel?.Find("Players");

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
        int blueIdx = 0, redIdx = 0;

        // Make all slots invisible (keep in layout, don't deactivate)
        foreach (var slot in blueSlots)
            SetSlotVisible(slot, false);
        foreach (var slot in redSlots)
            SetSlotVisible(slot, false);

        foreach (var entry in entries)
        {
            string[] parts = entry.Split(':');
            if (parts.Length < 4) continue;
            int actor = int.Parse(parts[0]);
            int team = int.Parse(parts[1]);
            int kills = int.Parse(parts[2]);
            float dmg = float.Parse(parts[3]);

            // Find player name
            string name = "Player " + actor;
            foreach (var p in Photon.Pun.PhotonNetwork.PlayerList)
            {
                if (p.ActorNumber == actor)
                {
                    name = string.IsNullOrEmpty(p.NickName) ? ("Player " + actor) : p.NickName;
                    break;
                }
            }

            Transform slot = null;
            if (team == 0 && blueIdx < blueSlots.Count)
                slot = blueSlots[blueIdx++];
            else if (team == 1 && redIdx < redSlots.Count)
                slot = redSlots[redIdx++];

            if (slot == null) continue;

            // Make visible and fill
            SetSlotVisible(slot, true);

            // Fill name
            var nameText = slot.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = name;

            // Fill kills
            var killsText = slot.Find("Kills")?.GetComponent<TextMeshProUGUI>();
            if (killsText != null)
                killsText.text = kills.ToString();

            // Fill damage (Kills (1))
            var damageText = slot.Find("Kills (1)")?.GetComponent<TextMeshProUGUI>();
            if (damageText != null)
                damageText.text = "" + (int)dmg;
        }
    }

    private void OnHealthChanged(float normalizedHealth)
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = Mathf.Clamp01(normalizedHealth);
    }

    public void UpdateTeamScores(int blueKills, int redKills)
    {
        if (blueScoreText != null)
            blueScoreText.text = blueKills.ToString();
        if (redScoreText != null)
            redScoreText.text = redKills.ToString();
    }

    private void LateUpdate()
    {
        // Billboard world name to face camera
        if (worldNameText != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                worldNameText.transform.parent.rotation = Quaternion.LookRotation(
                    worldNameText.transform.parent.position - cam.transform.position);
            }
        }
    }

    private void SetSlotVisible(Transform slot, bool visible)
    {
        // Make invisible without deactivating (keeps layout space)
        var images = slot.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images)
        {
            var c = img.color;
            c.a = visible ? 1f : 0f;
            img.color = c;
        }
        var texts = slot.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in texts)
        {
            var c = txt.color;
            c.a = visible ? 1f : 0f;
            txt.color = c;
        }
    }

    private void ApplyTeamColor()
    {
        if (photonView == null || photonView.Owner == null) return;

        // Absolute team color: Blue team = blue, Red team = red
        DataTypes.Team playerTeam;
        if (photonView.IsMine)
            playerTeam = RoomManager.GetLocalTeam();
        else
            playerTeam = RoomManager.GetTeam(photonView.Owner);

        Color teamColor = (playerTeam == DataTypes.Team.Blue)
            ? new Color(0.2f, 0.4f, 1f)
            : new Color(1f, 0.2f, 0.2f);

        // Apply color ONLY to the capsule body (GFX), not the gun or eyes
        var gfx = transform.Find("GFX");
        if (gfx != null)
        {
            var gfxRenderer = gfx.GetComponent<MeshRenderer>();
            if (gfxRenderer != null)
            {
                if (gfxRenderer.material != null)
                    gfxRenderer.material.color = teamColor;
                else if (gfxRenderer.sharedMaterial != null)
                    gfxRenderer.sharedMaterial.color = teamColor;
            }
        }

        // Set world name text color to match
        if (worldNameText != null)
            worldNameText.color = teamColor;
    }

    /// <summary>
    /// Player HUD canvases resolve their owning player through PhotonView.Get, which
    /// walks up from Player/Canvas to the view on the player root. Canvases that belong
    /// to no player - the Lobby canvas, the LoadingManager overlay, the debug console -
    /// return null and are left alone.
    /// </summary>
    private void DestroyOtherCanvases()
    {
        GameObject[] canvases = GameObject.FindGameObjectsWithTag("Canvas");

        foreach (GameObject canvasObject in canvases)
        {
            PhotonView owner = PhotonView.Get(canvasObject);

            // Not a player HUD - scene/LoadingManager/IngameDebugConsole. Keep it.
            if (owner == null) continue;

            if (owner.IsMine) continue;

            Destroy(canvasObject);
        }
    }
}
