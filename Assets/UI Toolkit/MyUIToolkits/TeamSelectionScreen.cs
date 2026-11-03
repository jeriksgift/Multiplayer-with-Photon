using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(UIDocument))]
public sealed class TeamSelectionScreen : MonoBehaviour
{
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private LobbyManager lobbyManager;
    private VisualElement root;
    private Button ready;
    private Button start;
    private Button leave;
    private readonly System.Collections.Generic.List<Button> slotButtons = new();

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        ready = root.Q<Button>("ReadyButton");
        start = root.Q<Button>("StartButton");
        leave = root.Q<Button>("LeaveButton");
        ready.clicked += roomManager.OnReadyClicked;
        start.clicked += roomManager.OnStartClicked;
        leave.clicked += lobbyManager.LeaveRoomBtn;
        slotButtons.Clear();
        int blue = 0, red = 0;
        for (int i = 0; i < roomManager.Slots.Count; i++)
        {
            var slot = roomManager.Slots[i];
            string name = slot.Team == DataTypes.Team.Blue ? "BlueSlot" + blue++ : "RedSlot" + red++;
            var button = root.Q<Button>(name);
            int index = i;
            button.clicked += () => roomManager.OnSlotClicked(index);
            slotButtons.Add(button);
        }
        roomManager.StateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (roomManager)
        {
            roomManager.StateChanged -= Refresh;
            if (ready != null) ready.clicked -= roomManager.OnReadyClicked;
            if (start != null) start.clicked -= roomManager.OnStartClicked;
        }
        if (leave != null && lobbyManager) leave.clicked -= lobbyManager.LeaveRoomBtn;
        slotButtons.Clear();
    }

    private void Refresh()
    {
        if (!PhotonNetwork.InRoom) return;
        root.Q<Label>("RoomName").text = PhotonNetwork.CurrentRoom.Name;
        root.Q<Label>("PlayerCount").text = PhotonNetwork.PlayerList.Length + " / " + PhotonNetwork.CurrentRoom.MaxPlayers + " PLAYERS";
        int blue = 0, red = 0;
        for (int i = 0; i < slotButtons.Count; i++)
        {
            var slot = roomManager.Slots[i];
            var button = slotButtons[i];
            button.Q<Label>(className: "slot-name").text = slot.isOccupied ? slot.PlayerName + (slot.isMine ? " (You)" : "") : "Open slot";
            var status = button.Q<Label>(className: "slot-status");
            status.text = !slot.isOccupied ? "JOIN" : slot.occupantIsReady ? "READY" : "NOT READY";
            button.EnableInClassList("slot-mine", slot.isMine);
            status.EnableInClassList("status-ready", slot.occupantIsReady);
            button.SetEnabled(!slot.isOccupied || slot.isMine);
            if (slot.isOccupied) { if (slot.Team == DataTypes.Team.Blue) blue++; else red++; }
        }
        root.Q<Label>("BlueCount").text = blue + " / 4";
        root.Q<Label>("RedCount").text = red + " / 4";
        bool inSlot = RoomManager.GetSlot(PhotonNetwork.LocalPlayer) >= 0;
        bool isReady = RoomManager.GetReady(PhotonNetwork.LocalPlayer);
        ready.SetEnabled(inSlot);
        ready.text = isReady ? "Cancel Ready" : "Ready";
        start.EnableInClassList("team-hidden", !PhotonNetwork.IsMasterClient);
        start.SetEnabled(roomManager.CanStartMatch);
        root.Q<Label>("TeamHint").text = !inSlot ? "Choose an open slot to join a team."
            : !isReady ? "Ready up when you are set."
            : roomManager.CanStartMatch ? "Both teams are ready. Start the match."
            : PhotonNetwork.IsMasterClient ? "Waiting for both teams to be ready."
            : "Waiting for the host to start.";
    }
}
