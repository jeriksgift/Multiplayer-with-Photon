using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(UIDocument))]
public sealed class LobbyScreen : MonoBehaviour
{
    [SerializeField] private LobbyManager lobbyManager;

    private TextField nicknameField;
    private TextField roomField;
    private Button enterButton;
    private Button exitButton;
    private Label dateTimeLabel;
    private Label statusLabel;
    private VisualElement root;
    private IVisualElementScheduledItem clock;
    private string nickname;
    private string roomName;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        nicknameField = root.Q<TextField>("NickNameTextField");
        roomField = root.Q<TextField>("RoomNameTextField");
        enterButton = root.Q<Button>("CreateJoinRoomButton");
        exitButton = root.Q<Button>("ExitGameButton");
        dateTimeLabel = root.Q<Label>("DateTimeLabel");
        statusLabel = root.Q<Label>("LobbyStatusLabel");
        if (!lobbyManager || nicknameField == null || roomField == null || enterButton == null || exitButton == null || dateTimeLabel == null || statusLabel == null)
        {
            Debug.LogError("LobbyScreen is missing its manager or a named element in lobby.uxml.", this);
            return;
        }

        if (nickname != null) nicknameField.SetValueWithoutNotify(nickname);
        if (roomName != null) roomField.SetValueWithoutNotify(roomName);
        nicknameField.maxLength = 16;
        roomField.maxLength = 24;
        nicknameField.RegisterValueChangedCallback(OnNicknameChanged);
        roomField.RegisterValueChangedCallback(OnRoomChanged);
        enterButton.clicked += Submit;
        exitButton.clicked += OpenExit;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        lobbyManager.StatusChanged += ShowStatus;
        ShowStatus(lobbyManager.StatusMessage);
        UpdateClock();
        clock = root.schedule.Execute(UpdateClock).Every(1000);
    }

    private void OnDisable()
    {
        clock?.Pause();
        clock = null;
        if (nicknameField != null)
        {
            nickname = nicknameField.value;
            nicknameField.UnregisterValueChangedCallback(OnNicknameChanged);
        }
        if (roomField != null)
        {
            roomName = roomField.value;
            roomField.UnregisterValueChangedCallback(OnRoomChanged);
        }
        if (enterButton != null) enterButton.clicked -= Submit;
        if (exitButton != null) exitButton.clicked -= OpenExit;
        root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        if (lobbyManager) lobbyManager.StatusChanged -= ShowStatus;
    }

    private void OnNicknameChanged(ChangeEvent<string> evt)
    {
        nickname = evt.newValue;
        ShowStatus(string.Empty);
    }

    private void OnRoomChanged(ChangeEvent<string> evt)
    {
        roomName = evt.newValue;
        ShowStatus(string.Empty);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
        if (evt.target is VisualElement target && (target == exitButton || exitButton.Contains(target))) return;
        Submit();
        evt.StopPropagation();
    }

    private void Submit()
    {
        string player = LobbyManager.NormalizeEntry(nicknameField.value);
        string room = LobbyManager.NormalizeEntry(roomField.value);
        // Cache the canonical values before joining can disable this screen.
        nickname = player;
        roomName = room;
        nicknameField.SetValueWithoutNotify(player);
        roomField.SetValueWithoutNotify(room);
        if (lobbyManager.TryEnterRoom(player, room)) return;
        if (player.Length < 2) nicknameField.Focus();
        else if (string.IsNullOrWhiteSpace(room)) roomField.Focus();
    }

    private void OpenExit()
    {
        var menu = FindAnyObjectByType<ToysGameUI>();
        if (menu) menu.ShowExit();
    }

    private void ShowStatus(string message)
    {
        if (statusLabel == null) return;
        statusLabel.text = message;
        statusLabel.EnableInClassList("lobby-status--hidden", string.IsNullOrEmpty(message));
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused && isActiveAndEnabled) UpdateClock();
    }

    private void UpdateClock()
    {
        if (dateTimeLabel == null) return;
        string value = FormatLocalTime(DateTime.Now);
        if (dateTimeLabel.text != value) dateTimeLabel.text = value;
    }

    public static string FormatLocalTime(DateTime value)
    {
        int day = value.Day;
        string suffix = day % 100 >= 11 && day % 100 <= 13 ? "th"
            : day % 10 == 1 ? "st" : day % 10 == 2 ? "nd" : day % 10 == 3 ? "rd" : "th";
        return day.ToString(CultureInfo.InvariantCulture) + suffix + " "
            + value.ToString("MMMM, yyyy - HH:mm", CultureInfo.InvariantCulture);
    }
}
