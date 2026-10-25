using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager Instance;
    public const string KeyTeam = "team";
    public const string KeySlot = "slot";
    public const string KeyTeamIndex = "teamIndex";
    public const string KeyReady = "ready";

    [Header("Scene References")]
    [SerializeField] private GameObject slotsContainer;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonLabel;
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI roomNameTxt;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "Game";

    [Tooltip("Minimum slotted, ready players needed before the host can start.")]
    [SerializeField] private int minPlayersToStart = 2;

    private readonly List<RoomSlot> slots = new();
    private readonly Dictionary<int, int> slotOwners = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // Must be enabled on EVERY client, not just the master. LoadLevelIfSynced()
        // returns immediately when this is false, so a client that never sets it
        // ignores the master's scene change and stays in the lobby.
        PhotonNetwork.AutomaticallySyncScene = true;

        foreach (var slot in slotsContainer.GetComponentsInChildren<RoomSlot>(true))
        {
            slots.Add(slot);

            var button = slot.GetComponent<Button>();
            if (button != null)
            {
                int index = slots.Count - 1;
                button.onClick.AddListener(() => OnSlotClicked(index));
            }
        }

        if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    public override void OnJoinedRoom()
    {
        SetLocalProps(-1, DataTypes.Team.Blue, -1, false);
        if (roomNameTxt != null)
        {
            roomNameTxt.text = "Room Name: " + PhotonNetwork.CurrentRoom.Name;
        }
        Refresh();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => Refresh();

    public override void OnPlayerLeftRoom(Player player) => Refresh();

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) => Refresh();

    private void OnSlotClicked(int index)
    {
        if (index < 0 || index >= slots.Count) return;

        if (GetSlot(PhotonNetwork.LocalPlayer) == index)
        {
            SetLocalSlot(-1);
            return;
        }

        if (IsSlotTaken(index)) return;

        SetLocalSlot(index);
    }

    private void OnReadyClicked()
    {
        if (GetSlot(PhotonNetwork.LocalPlayer) < 0)
        {
            Debug.Log("Pick a slot before readying up.");
            return;
        }

        SetLocalProps(
            GetSlot(PhotonNetwork.LocalPlayer),
            GetTeam(PhotonNetwork.LocalPlayer),
            GetTeamIndex(PhotonNetwork.LocalPlayer),
            !GetReady(PhotonNetwork.LocalPlayer));
    }

    private void OnStartClicked()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (startButton != null) startButton.interactable = false;

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.ShowLoadingScreen("Loading...");
        }

        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    private void SetLocalSlot(int slotIndex)
    {
        if (slotIndex < 0)
        {
            SetLocalProps(-1, DataTypes.Team.Blue, -1, false);
            return;
        }

        SetLocalProps(slotIndex, slots[slotIndex].Team, GetTeamIndexForSlot(slotIndex), false);
    }

    private void SetLocalProps(int slotIndex, DataTypes.Team team, int teamIndex, bool ready)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        {
            { KeyTeam, (int)team },
            { KeySlot, slotIndex },
            { KeyTeamIndex, teamIndex },
            { KeyReady, ready }
        });
    }

    private int GetTeamIndexForSlot(int slotIndex)
    {
        DataTypes.Team team = slots[slotIndex].Team;
        int index = 0;

        for (int i = 0; i < slotIndex; i++)
        {
            if (slots[i].Team == team) index++;
        }

        return index;
    }

    private bool IsSlotTaken(int index)
    {
        return slotOwners.TryGetValue(index, out int actor) && actor != PhotonNetwork.LocalPlayer.ActorNumber;
    }

    public static DataTypes.Team GetLocalTeam() => GetTeam(PhotonNetwork.LocalPlayer);

    public static int GetLocalTeamIndex() => GetTeamIndex(PhotonNetwork.LocalPlayer);

    public static DataTypes.Team GetTeam(Player player) =>
        TryGetInt(player, KeyTeam, out int value) ? (DataTypes.Team)value : DataTypes.Team.Blue;

    public static int GetTeamIndex(Player player) =>
        TryGetInt(player, KeyTeamIndex, out int value) ? value : -1;

    public static int GetSlot(Player player) =>
        TryGetInt(player, KeySlot, out int value) ? value : -1;

    public static bool GetReady(Player player) =>
        player != null
        && player.CustomProperties != null
        && player.CustomProperties.TryGetValue(KeyReady, out object value)
        && value is bool ready
        && ready;

    private static bool TryGetInt(Player player, string key, out int value)
    {
        value = 0;

        if (player == null || player.CustomProperties == null) return false;
        if (!player.CustomProperties.TryGetValue(key, out object raw)) return false;
        if (!(raw is int intValue)) return false;

        value = intValue;
        return true;
    }

    private void Refresh()
    {
        if (slots.Count == 0) return;

        var ordered = GetOrderedPlayers();

        slotOwners.Clear();

        foreach (var player in ordered)
        {
            int index = GetSlot(player);
            if (index < 0 || index >= slots.Count) continue;
            if (slotOwners.ContainsKey(index)) continue;
            slotOwners[index] = player.ActorNumber;
        }

        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int mySlot = GetSlot(PhotonNetwork.LocalPlayer);
        if (mySlot >= 0 && (!slotOwners.TryGetValue(mySlot, out int winner) || winner != myActor))
        {
            SetLocalSlot(-1);
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            if (!slotOwners.TryGetValue(i, out int actor))
            {
                slot.SetEmpty();
                continue;
            }

            var player = FindPlayer(actor);
            string name = player != null && !string.IsNullOrEmpty(player.NickName)
                ? player.NickName
                : "Player " + actor;

            slot.SetOccupied(name, actor, GetReady(player), actor == myActor);
        }

        UpdateButtons(ordered);
    }

    private static List<Player> GetOrderedPlayers()
    {
        var players = new List<Player>(PhotonNetwork.PlayerList.Length);
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player != null) players.Add(player);
        }
        players.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        return players;
    }

    private static Player FindPlayer(int actorNumber)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player != null && player.ActorNumber == actorNumber) return player;
        }
        return null;
    }

    private void UpdateButtons(List<Player> ordered)
    {
        bool inSlot = GetSlot(PhotonNetwork.LocalPlayer) >= 0;
        bool ready = GetReady(PhotonNetwork.LocalPlayer);

        if (readyButton != null) readyButton.interactable = inSlot;

        if (readyButtonLabel != null)
        {
            readyButtonLabel.text = ready ? "Cancel Ready" : "Ready";
        }

        if (startButton == null) return;

        bool isHost = PhotonNetwork.IsMasterClient;
        startButton.gameObject.SetActive(isHost);
        startButton.interactable = isHost && CanStart(ordered);
    }

    private bool CanStart(List<Player> ordered)
    {
        if (slotOwners.Count < minPlayersToStart) return false;

        if (!slotOwners.ContainsValue(PhotonNetwork.LocalPlayer.ActorNumber)) return false;

        int slottedReady = 0;
        bool hasBlue = false;
        bool hasRed = false;

        foreach (var player in ordered)
        {
            if (!slotOwners.ContainsValue(player.ActorNumber)) continue;
            if (!GetReady(player)) return false;

            slottedReady++;

            if (GetTeam(player) == DataTypes.Team.Blue) hasBlue = true;
            else hasRed = true;
        }

        return slottedReady >= minPlayersToStart && hasBlue && hasRed;
    }
}