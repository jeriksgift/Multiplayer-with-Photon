using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public event System.Action<string> StatusChanged;
    public string StatusMessage { get; private set; } = string.Empty;
    [SerializeField] private TextMeshProUGUI nickNameInputField;

    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private TextMeshProUGUI lobbyStatusText;

    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;

    private void Awake()
    {
        // Set before connecting: Photon requires AutomaticallySyncScene on every client
        // so remote clients follow the master's PhotonNetwork.LoadLevel(). Clients that
        // leave it false stay in the lobby when the host starts the game.
        // Don't sync scene if we're returning from a match (avoids SetProperties error)
        if (!PlayerUI.isReturningFromMatch)
            PhotonNetwork.AutomaticallySyncScene = true;

        PhotonNetwork.ConnectUsingSettings();
        Debug.Log("Connecting...");
    }

    private void Start()
    {
        PlayerUI.isReturningFromMatch = false;
        LoadingManager.Instance.ShowLoadingScreen("Connecting...");
    }

    public override void OnConnectedToMaster()
    {
        if (PhotonNetwork.OfflineMode) return;
        Debug.Log("Connected to Master " + Time.time);
        PhotonNetwork.JoinLobby();
    }
    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby " + PhotonNetwork.CurrentLobby.Name);
        LoadingManager.Instance.HideLoadingScreen();
    }

    public void CreateOrJoinRoomBtn() => EnterRoom(0);
    public void CreateRoomBtn() => EnterRoom(1);
    public void JoinRoomBtn() => EnterRoom(2);

    private void EnterRoom(int mode)
    {
        if (nickNameInputField == null || roomNameInputField == null) return;
        var nicknameInput = nickNameInputField.GetComponentInParent<TMP_InputField>();
        string nickname = nicknameInput ? nicknameInput.text : nickNameInputField.text;
        if (!TryEnterRoom(nickname, roomNameInputField.text, mode))
        {
            if (NormalizeEntry(nickname).Length < 2 && nicknameInput) nicknameInput.ActivateInputField();
            else if (string.IsNullOrWhiteSpace(NormalizeEntry(roomNameInputField.text))) roomNameInputField.ActivateInputField();
        }
    }

    public static string NormalizeEntry(string value) => (value ?? string.Empty).Replace("\u200B", "").Trim();

    public bool TryEnterRoom(string nickname, string roomName, int mode = 0)
    {
        nickname = NormalizeEntry(nickname);
        roomName = NormalizeEntry(roomName);
        if (nickname.Length < 2)
        {
            SetLobbyStatus("Enter a player name with at least 2 characters.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(roomName))
        {
            SetLobbyStatus("ROOM NAME REQUIRED");
            return false;
        }
        if (!PhotonNetwork.IsConnectedAndReady)
        {
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
