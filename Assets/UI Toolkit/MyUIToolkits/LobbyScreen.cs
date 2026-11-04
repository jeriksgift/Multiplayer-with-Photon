using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(UIDocument))]
public sealed class LobbyScreen : MonoBehaviour
{
    [SerializeField] private LobbyManager lobbyManager;

    private TextField roomField;
    private Button enterButton;
    private Button settingsButton;
    private LobbySettingsScreen settings;
    private Label dateTimeLabel;
    private Label statusLabel;
    private VisualElement root;
    private IVisualElementScheduledItem clock;
    private string roomName;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        roomField = root.Q<TextField>("RoomNameTextField");
        enterButton = root.Q<Button>("CreateJoinRoomButton");
        settingsButton = root.Q<Button>("SettingsButton");
        settings = GetComponent<LobbySettingsScreen>();
        dateTimeLabel = root.Q<Label>("DateTimeLabel");
        statusLabel = root.Q<Label>("LobbyStatusLabel");
        if (!lobbyManager || roomField == null || enterButton == null || settingsButton == null || dateTimeLabel == null || statusLabel == null)
        {
            Debug.LogError("LobbyScreen is missing its manager or a named element in lobby.uxml.", this);
            return;
        }

        if (roomName != null) roomField.SetValueWithoutNotify(roomName);
        roomField.maxLength = 24;
        roomField.RegisterValueChangedCallback(OnRoomChanged);
        enterButton.clicked += Submit;
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
        if (roomField != null)
        {
            roomName = roomField.value;
            roomField.UnregisterValueChangedCallback(OnRoomChanged);
        }
        if (enterButton != null) enterButton.clicked -= Submit;
        root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        if (lobbyManager) lobbyManager.StatusChanged -= ShowStatus;
    }

    private void OnRoomChanged(ChangeEvent<string> evt)
    {
        roomName = evt.newValue;
        ShowStatus(string.Empty);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
        if (settings && settings.IsOpen) return;
        if (evt.target is VisualElement target && (target == settingsButton || settingsButton.Contains(target))) return;
        Submit();
        evt.StopPropagation();
    }

    private void Submit()
    {
        string room = LobbyManager.NormalizeEntry(roomField.value);
        // Cache the canonical values before joining can disable this screen.
        roomName = room;
        roomField.SetValueWithoutNotify(room);
        if (lobbyManager.TryEnterRoom(room)) return;
        if (string.IsNullOrWhiteSpace(room)) roomField.Focus();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame || !settings) return;
        if (settings.IsOpen) settings.Close(); else settings.Open();
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
