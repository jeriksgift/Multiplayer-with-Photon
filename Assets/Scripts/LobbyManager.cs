using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using System.Collections;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public event System.Action<string> StatusChanged;
    public string StatusMessage { get; private set; } = string.Empty;
    [SerializeField] private TextMeshProUGUI nickNameInputField;

    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private TextMeshProUGUI lobbyStatusText;

    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;
    private Coroutine reconnectRoutine;
    private int reconnectAttempts;
    private bool quitting;

    private void Awake()
    {
        if (!AccountSession.Instance || !AccountSession.Instance.IsReady)
        {
            enabled = false;
            if (lobbyPanel) lobbyPanel.SetActive(false);
            AccountSession.Instance?.RequireLogin();
            return;
        }
        PhotonNetwork.AuthValues = new AuthenticationValues(AccountSession.Instance.PlayerId);
        PhotonNetwork.NickName = AccountSession.Instance.Profile.nickname;
        // Set before connecting: Photon requires AutomaticallySyncScene on every client
        // so remote clients follow the master's PhotonNetwork.LoadLevel(). Clients that
        // leave it false stay in the lobby when the host starts the game.
        // Don't sync scene if we're returning from a match (avoids SetProperties error)
        if (!PlayerUI.isReturningFromMatch)
            PhotonNetwork.AutomaticallySyncScene = true;

        if (!PhotonNetwork.OfflineMode &&
            (PhotonNetwork.NetworkClientState == ClientState.Disconnected ||
             PhotonNetwork.NetworkClientState == ClientState.PeerCreated))
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("Connecting...");
        }
    }

    private void Start()
    {
        PlayerUI.isReturningFromMatch = false;
        LoadingManager.Instance.ShowLoadingScreen("Connecting...");
        if (PhotonNetwork.OfflineMode || PhotonNetwork.InLobby)
            LoadingManager.Instance.HideLoadingScreen();
        else if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
            OnConnectedToMaster();
    }

    public override void OnConnectedToMaster()
    {
        if (PhotonNetwork.OfflineMode) return;
        StopReconnect();
        reconnectAttempts = 0;
        Debug.Log("Connected to Master " + Time.time);
        PhotonNetwork.JoinLobby();
    }
    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby " + PhotonNetwork.CurrentLobby.Name);
        LoadingManager.Instance.HideLoadingScreen();
        SetLobbyStatus(string.Empty);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (LoadingManager.Instance) LoadingManager.Instance.HideLoadingScreen();
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        if (quitting || PhotonNetwork.OfflineMode || cause == DisconnectCause.DisconnectByClientLogic)
        {
            StopReconnect();
            return;
        }
        if (reconnectRoutine == null) reconnectRoutine = StartCoroutine(Reconnect());
    }

    private IEnumerator Reconnect()
    {
        while (!quitting && !PhotonNetwork.OfflineMode && !PhotonNetwork.IsConnectedAndReady)
        {
            float delay = Mathf.Min(2f * Mathf.Pow(2f, Mathf.Min(reconnectAttempts, 4)), 30f);
            SetLobbyStatus("Connection lost. Retrying in " + delay.ToString("0") + "s...");
            yield return new WaitForSecondsRealtime(delay);
            if (quitting || PhotonNetwork.OfflineMode || PhotonNetwork.IsConnectedAndReady) break;
            if (PhotonNetwork.NetworkClientState == ClientState.Disconnected ||
                PhotonNetwork.NetworkClientState == ClientState.PeerCreated)
            {
                reconnectAttempts++;
                SetLobbyStatus("Reconnecting... Attempt " + reconnectAttempts);
                PhotonNetwork.ConnectUsingSettings();
            }
            while (!quitting && !PhotonNetwork.OfflineMode && !PhotonNetwork.IsConnectedAndReady &&
                   PhotonNetwork.NetworkClientState != ClientState.Disconnected &&
                   PhotonNetwork.NetworkClientState != ClientState.PeerCreated)
                yield return null;
        }
        reconnectRoutine = null;
    }

    private void StopReconnect()
    {
        if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);
        reconnectRoutine = null;
    }

    public override void OnDisable()
    {
        StopReconnect();
        base.OnDisable();
    }

    private void OnApplicationQuit()
    {
        quitting = true;
        StopReconnect();
    }

    public void CreateOrJoinRoomBtn() => EnterRoom(0);
    public void CreateRoomBtn() => EnterRoom(1);
    public void JoinRoomBtn() => EnterRoom(2);

    private void EnterRoom(int mode)
    {
        if (roomNameInputField == null) return;
        if (!TryEnterRoom(roomNameInputField.text, mode))
        {
            if (string.IsNullOrWhiteSpace(NormalizeEntry(roomNameInputField.text))) roomNameInputField.ActivateInputField();
        }
    }

    public static string NormalizeEntry(string value) => (value ?? string.Empty).Replace("\u200B", "").Trim();

    [System.Obsolete("The saved profile supplies the nickname. Use TryEnterRoom(roomName, mode).")]
    public bool TryEnterRoom(string nickname, string roomName, int mode = 0) => TryEnterRoom(roomName, mode);

    public bool TryEnterRoom(string roomName, int mode = 0)
    {
        if (!AccountSession.Instance || !AccountSession.Instance.IsReady)
        {
            AccountSession.Instance?.RequireLogin();
            return false;
        }
        string nickname = AccountSession.Instance.Profile.nickname;
        roomName = NormalizeEntry(roomName);
        if (string.IsNullOrWhiteSpace(roomName))
        {
            SetLobbyStatus("ROOM NAME REQUIRED");
            return false;
        }
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            if (!PhotonNetwork.OfflineMode &&
                (PhotonNetwork.NetworkClientState == ClientState.Disconnected ||
                 PhotonNetwork.NetworkClientState == ClientState.PeerCreated))
                PhotonNetwork.ConnectUsingSettings();
            SetLobbyStatus("Connecting to server. Try again in a moment.");
            return false;
        }

        PhotonNetwork.NickName = nickname;
        if (roomNameInputField) roomNameInputField.text = roomName;
        SetLobbyStatus(string.Empty);
        bool sent = mode == 1
            ? PhotonNetwork.CreateRoom(roomName, new RoomOptions { MaxPlayers = 8 }, TypedLobby.Default)
            : mode == 2 ? PhotonNetwork.JoinRoom(roomName)
            : PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = 8 }, TypedLobby.Default);
        if (!sent)
        {
            SetLobbyStatus("Room request unavailable. Please try again.");
            return false;
        }
        if (!PhotonNetwork.InRoom) LoadingManager.Instance.ShowLoadingScreen("Loading...");
        return true;
    }

    public void LeaveRoomBtn()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        SetLobbyStatus(string.Empty);
        if (LoadingManager.Instance) LoadingManager.Instance.HideLoadingScreen();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        LoadingManager.Instance.HideLoadingScreen();
        SetLobbyStatus("Unable to enter room: " + message);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        LoadingManager.Instance.HideLoadingScreen();
        SetLobbyStatus("Unable to create room: " + message);
    }

    private void SetLobbyStatus(string message)
    {
        StatusMessage = message;
        if (lobbyStatusText != null) lobbyStatusText.text = message;
        StatusChanged?.Invoke(message);
    }

    public override void OnJoinedRoom()
    {
        // Scene transition to Game is driven by RoomManager's Start button,
        // once a team slot is picked and everyone is ready.
        lobbyPanel.SetActive(false);
        roomPanel.SetActive(true);

        LoadingManager.Instance.HideLoadingScreen();
    }

    public void ExitBtn()
    {
        Application.Quit();
    }
}
