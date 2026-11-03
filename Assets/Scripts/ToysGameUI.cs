using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Live interface values and an owner-only, non-pausing exit confirmation.</summary>
public sealed class ToysGameUI : MonoBehaviour
{
    public bool matchInterface;
    public GameObject exitOverlay;
    public TextMeshProUGUI exitRoomText;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI healthValueText;
    public TextMeshProUGUI networkText;
    public TextMeshProUGUI roomCountText;
    public TextMeshProUGUI blueCountText;
    public TextMeshProUGUI redCountText;
    public TextMeshProUGUI targetScoreText;
    public GameObject teamSelectionPanel;
    public LobbyManager lobbyManager;

    HealthComponent health;
    PlayerUI playerUI;
    PhotonView view;
    float nextRefresh;
    CursorLockMode priorLock;
    bool priorVisible;
    public bool IsExitOpen => exitOverlay && exitOverlay.activeSelf;

    void Start()
    {
        view = GetComponent<PhotonView>();
        if (matchInterface && view && PhotonNetwork.InRoom && !view.IsMine)
        {
            enabled = false;
            return;
        }
        playerUI = GetComponent<PlayerUI>();
        health = GetComponent<HealthComponent>();
        if (health) health.HealthChanged += SetHealth;
        SetHealth(1f);
        if (targetScoreText) targetScoreText.text = GameManager.WIN_SCORE.ToString();
        RefreshLabels();
    }

    void OnDestroy()
    {
        if (health) health.HealthChanged -= SetHealth;
    }

    void SetHealth(float amount)
    {
        if (healthValueText) healthValueText.text = Mathf.RoundToInt(Mathf.Clamp01(amount) * 100).ToString("00");
    }

    void Update()
    {
        if (!matchInterface && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            var input = selected ? selected.GetComponent<TMP_InputField>() : null;
            if (input && input.isFocused) input.DeactivateInputField();
            else if (teamSelectionPanel && teamSelectionPanel.activeSelf && lobbyManager) lobbyManager.LeaveRoomBtn();
            else ToggleExit();
        }
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + .25f;
            RefreshLabels();
        }
    }

    void RefreshLabels()
    {
        if (playerNameText) playerNameText.text = string.IsNullOrWhiteSpace(PhotonNetwork.NickName) ? "PLAYER" : PhotonNetwork.NickName.ToUpperInvariant();
        if (networkText) networkText.text = PhotonNetwork.InRoom ? "ROOM CONNECTED" : PhotonNetwork.InLobby ? "NETWORK ONLINE" : PhotonNetwork.IsConnected ? "CONNECTING" : "OFFLINE";
        if (exitRoomText) exitRoomText.text = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name.ToUpperInvariant() : "TOYS DEATH MATCH";
        int blue = 0, red = 0;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (RoomManager.GetSlot(player) < 0) continue;
            if (RoomManager.GetTeam(player) == DataTypes.Team.Blue) blue++; else red++;
        }
        if (roomCountText) roomCountText.text = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Length + " / 8 PLAYERS CONNECTED" : "SELECT A ROOM";
        if (blueCountText) blueCountText.text = blue + " / 4";
        if (redCountText) redCountText.text = red + " / 4";
    }

    public void ToggleExit()
    {
        if (IsExitOpen) CancelExit(); else ShowExit();
    }

    public void ShowExit()
    {
        if (!exitOverlay || IsExitOpen) return;
        priorLock = Cursor.lockState;
        priorVisible = Cursor.visible;
        RefreshLabels();
        exitOverlay.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (matchInterface) GetComponent<PlayerWithGFXJump>()?.SetMoveVelocity(Vector3.zero);
    }

    public void CancelExit()
    {
        if (!exitOverlay) return;
        exitOverlay.SetActive(false);
        Cursor.lockState = priorLock;
        Cursor.visible = priorVisible;
    }

    public void ConfirmExit()
    {
        if (matchInterface)
        {
            if (playerUI) playerUI.ExitToLobby();
        }
        else Application.Quit();
    }
}
