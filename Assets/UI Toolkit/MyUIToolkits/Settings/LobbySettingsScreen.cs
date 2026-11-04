using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(90)]
[RequireComponent(typeof(UIDocument))]
public sealed class LobbySettingsScreen : MonoBehaviour
{
    private static readonly string[] Tabs = { "Accounts", "Controls", "Sound", "Graphics", "Friends" };
    private readonly List<Action> unbind = new List<Action>();
    private VisualElement root;
    private VisualElement overlay;
    private Label status;
    private bool busy;
    private bool inMatch;
    private GameHudScreen matchHud;
    private CursorLockMode priorLock;
    private bool priorVisible;
    private bool friendsInitialized;
    private int generation;
    private readonly Dictionary<string, string> publicPlayerIds = new Dictionary<string, string>();
    public bool IsOpen => overlay != null && !overlay.ClassListContains("toys-hidden");

    private void OnEnable()
    {
        generation++;
        root = GetComponent<UIDocument>().rootVisualElement;
        overlay = root.Q("SettingsOverlay");
        status = root.Q<Label>("SettingsStatus");
        matchHud = GetComponentInParent<GameHudScreen>();
        inMatch = matchHud != null;
        if (inMatch) { BindMatch(); return; }
        Bind("SettingsButton", Open);
        Bind("CloseSettingsButton", Close);
        foreach (string tab in Tabs) { string selected = tab; Bind(tab + "Tab", () => SelectTab(selected)); }
        Bind("CopyPlayerIdButton", () => { GUIUtility.systemCopyBuffer = AccountSession.Instance.PublicPlayerId; status.text = "Player ID copied."; });
        Bind("LinkGooglePlayButton", () => Run(async () => { await AccountSession.Instance.LinkGoogleAsync(); RefreshAccount(); }, "Google Play linked. Your player ID and progress have been kept."));
        Bind("ExitFromSettingsButton", () => { PlayerPrefs.Save(); Application.Quit(); });
        Bind("OpenDeleteButton", OpenDelete);
        Bind("CancelDeleteButton", CloseDelete);
        Bind("ConfirmDeleteButton", Delete);
        Bind("SendFriendRequestButton", SendFriendRequest);
        Bind("RefreshFriendsButton", RefreshFriends);
        var field = root.Q<TextField>("DeleteConfirmationText");
        field.RegisterValueChangedCallback(OnDeleteText);
        unbind.Add(() => field.UnregisterValueChangedCallback(OnDeleteText));
        foreach (string channel in new[] { "Master", "Music", "Sfx" })
        {
            var slider = root.Q<Slider>(channel + "Volume");
            slider.RegisterValueChangedCallback(OnVolumeChanged);
            unbind.Add(() => slider.UnregisterValueChangedCallback(OnVolumeChanged));
        }
        for (int i = 0; i < PlayerSettingsService.GraphicsOptions.Length; i++)
        {
            int index = i;
            Bind("Graphics" + PlayerSettingsService.GraphicsOptions[i], () => { PlayerSettingsService.Instance.SetGraphics(index); RefreshGraphics(); });
        }
        root.Q<Button>("ConfirmDeleteButton").SetEnabled(false);
    }

    private void OnDisable()
    {
        generation++;
        foreach (var action in unbind) action();
        unbind.Clear();
        if (friendsInitialized)
        {
            FriendsService.Instance.RelationshipAdded -= OnRelationshipAdded;
            FriendsService.Instance.RelationshipDeleted -= OnRelationshipDeleted;
            friendsInitialized = false;
        }
        busy = false;
    }

    private void Bind(string name, Action action)
    {
        var button = root.Q<Button>(name);
        button.clicked += action;
        unbind.Add(() => button.clicked -= action);
    }

    public void Open()
    {
        if (inMatch)
        {
            if (matchHud.IsPanelOpen || GetComponentInParent<ToysGameUI>().IsExitOpen) return;
            priorLock = UnityEngine.Cursor.lockState; priorVisible = UnityEngine.Cursor.visible;
            matchHud.IsPanelOpen = true;
            Show("SettingsOverlay", true); RefreshSound(); RefreshGraphics(); SelectTab("Controls");
            UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
            return;
        }
        if (!AccountSession.Instance.IsReady) { AccountSession.Instance.RequireLogin(); return; }
        Show("SettingsOverlay", true);
        SetLobbyEnabled(false);
        RefreshAccount();
        RefreshSound();
        RefreshGraphics();
        SelectTab("Accounts");
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        root.Q<Button>("AccountsTab").Focus();
    }

    public void Close()
    {
        if (inMatch)
        {
            Show("SettingsOverlay", false); matchHud.IsPanelOpen = false;
            UnityEngine.Cursor.lockState = priorLock; UnityEngine.Cursor.visible = priorVisible;
            PlayerPrefs.Save(); return;
        }
        if (busy) return;
        if (!root.Q("DeleteConfirmationOverlay").ClassListContains("toys-hidden")) { CloseDelete(); return; }
        Show("SettingsOverlay", false);
        SetLobbyEnabled(true);
        PlayerPrefs.Save();
        root.Q<Button>("SettingsButton").Focus();
    }

    private void SelectTab(string selected)
    {
        if (busy) return;
        foreach (string tab in Tabs)
        {
            Show(tab + "Page", tab == selected);
            root.Q<Button>(tab + "Tab").EnableInClassList("toys-button--selected", tab == selected);
        }
        status.text = string.Empty;
        if (selected == "Friends") RefreshFriends();
    }

    private void BindMatch()
    {
        foreach (string tab in new[] { "Accounts", "Friends" }) Show(tab + "Tab", false);
        Bind("CloseSettingsButton", Close);
        foreach (string tab in new[] { "Controls", "Sound", "Graphics" }) { string name = tab; Bind(tab + "Tab", () => SelectTab(name)); }
        foreach (string channel in new[] { "Master", "Music", "Sfx" })
        {
            var slider = root.Q<Slider>(channel + "Volume");
            slider.RegisterValueChangedCallback(OnVolumeChanged);
            unbind.Add(() => slider.UnregisterValueChangedCallback(OnVolumeChanged));
        }
        for (int i = 0; i < PlayerSettingsService.GraphicsOptions.Length; i++)
        {
            int index = i;
            Bind("Graphics" + PlayerSettingsService.GraphicsOptions[i], () => { PlayerSettingsService.Instance.SetGraphics(index); RefreshGraphics(); });
        }
    }

    private void RefreshAccount()
    {
        var session = AccountSession.Instance;
        root.Q<Label>("AccountNickname").text = session.Profile?.nickname ?? string.Empty;
        root.Q<Label>("AccountType").text = session.GoogleLinked ? "Linked with Google Play" : "Guest (Anonymous)";
        root.Q<Label>("AccountPlayerId").text = session.PublicPlayerId;
        root.Q<Label>("FriendsPlayerId").text = "Your player ID: " + session.PublicPlayerId;
        Show("LinkGooglePlayButton", !session.GoogleLinked);
    }

    private void RefreshSound()
    {
        var settings = PlayerSettingsService.Instance;
        root.Q<Slider>("MasterVolume").SetValueWithoutNotify(settings.Master);
        root.Q<Slider>("MusicVolume").SetValueWithoutNotify(settings.Music);
        root.Q<Slider>("SfxVolume").SetValueWithoutNotify(settings.Sfx);
        RefreshVolumeLabels();
    }

    private void OnVolumeChanged(ChangeEvent<float> evt)
    {
        PlayerSettingsService.Instance.SetVolumes(root.Q<Slider>("MasterVolume").value,
            root.Q<Slider>("MusicVolume").value, root.Q<Slider>("SfxVolume").value);
        RefreshVolumeLabels();
    }

    private void RefreshVolumeLabels()
    {
        foreach (string channel in new[] { "Master", "Music", "Sfx" })
            root.Q<Label>(channel + "VolumeValue").text = Mathf.RoundToInt(root.Q<Slider>(channel + "Volume").value * 100) + "%";
    }

    private void RefreshGraphics()
    {
        for (int i = 0; i < PlayerSettingsService.GraphicsOptions.Length; i++)
            root.Q<Button>("Graphics" + PlayerSettingsService.GraphicsOptions[i]).EnableInClassList("toys-button--selected", i == PlayerSettingsService.Instance.Graphics);
    }

    private void OpenDelete()
    {
        root.Q("SettingsCard").SetEnabled(false);
        root.Q<TextField>("DeleteConfirmationText").value = string.Empty;
        root.Q<Label>("DeleteStatus").text = string.Empty;
        root.Q<Button>("ConfirmDeleteButton").SetEnabled(false);
        Show("DeleteConfirmationOverlay", true);
        root.Q<TextField>("DeleteConfirmationText").Focus();
    }

    private void CloseDelete()
    {
        if (busy) return;
        if (AccountSession.Instance.DeletionPending) { AccountSession.Instance.RequireLogin(); return; }
        Show("DeleteConfirmationOverlay", false);
        root.Q("SettingsCard").SetEnabled(true);
        root.Q<TextField>("DeleteConfirmationText").value = string.Empty;
        root.Q<Button>("OpenDeleteButton").Focus();
    }

    private void OnDeleteText(ChangeEvent<string> evt) { root.Q<Button>("ConfirmDeleteButton").SetEnabled(!busy && evt.newValue == "CONFIRM"); }
    private void Delete()
    {
        Run(() => AccountSession.Instance.DeleteAccountAsync(root.Q<TextField>("DeleteConfirmationText").value), string.Empty, true);
    }

    private void RefreshFriends()
    {
        Run(async () => { await EnsureFriendsAsync(); await FriendsService.Instance.ForceRelationshipsRefreshAsync(); await LoadFriendIdsAsync(); RenderFriends(); }, string.Empty);
    }

    private async Task LoadFriendIdsAsync()
    {
        var ids = FriendsService.Instance.Relationships.Select(r => r.Member.Id).Distinct().ToArray();
        for (int offset = 0; offset < ids.Length; offset += 100)
        {
            var values = await AccountSession.Instance.LookupPublicPlayerIdsAsync(ids.Skip(offset).Take(100));
            foreach (var pair in values) publicPlayerIds[pair.Key] = pair.Value;
        }
    }

    private async Task EnsureFriendsAsync()
    {
        if (friendsInitialized) return;
        int current = generation;
        await FriendsService.Instance.InitializeAsync();
        if (!this || current != generation) return;
        FriendsService.Instance.RelationshipAdded += OnRelationshipAdded;
        FriendsService.Instance.RelationshipDeleted += OnRelationshipDeleted;
        friendsInitialized = true;
    }

    private void OnRelationshipAdded(IRelationshipAddedEvent evt) { RenderFriends(); }
    private void OnRelationshipDeleted(IRelationshipDeletedEvent evt) { RenderFriends(); }

    private void SendFriendRequest()
    {
        string publicId = root.Q<TextField>("FriendPlayerId").value.Trim();
        Run(async () =>
        {
            if (!AccountSession.IsPublicPlayerId(publicId)) throw new ArgumentException("Enter the other player's 10-digit ID.");
            if (publicId == AccountSession.Instance.PublicPlayerId) throw new ArgumentException("You cannot add yourself.");
            string id = await AccountSession.Instance.ResolvePublicPlayerIdAsync(publicId);
            publicPlayerIds[id] = publicId;
            await EnsureFriendsAsync();
            if (FriendsService.Instance.Friends.Any(r => r.Member.Id == id)) throw new ArgumentException("This player is already your friend.");
            if (FriendsService.Instance.OutgoingFriendRequests.Any(r => r.Member.Id == id)) throw new ArgumentException("You already sent a request to this player.");
            if (FriendsService.Instance.IncomingFriendRequests.Any(r => r.Member.Id == id))
                throw new ArgumentException("This player sent you a request. Choose Accept below to become friends.");
            await FriendsService.Instance.AddFriendAsync(id);
            root.Q<TextField>("FriendPlayerId").value = string.Empty;
            RenderFriends();
        }, "Friend request sent. The other player must accept it.");
    }

    private void RenderFriends()
    {
        if (!friendsInitialized || !this) return;
        var list = root.Q("FriendsList");
        list.Clear();
        AddGroup(list, "Incoming requests", FriendsService.Instance.IncomingFriendRequests, "incoming");
        AddGroup(list, "Friends", FriendsService.Instance.Friends, "friends");
        AddGroup(list, "Sent requests", FriendsService.Instance.OutgoingFriendRequests, "outgoing");
    }

    private void AddGroup(VisualElement parent, string title, IReadOnlyList<Relationship> relationships, string type)
    {
        var heading = new Label(title + " (" + relationships.Count + ")");
        heading.AddToClassList("toys-heading"); parent.Add(heading);
        if (relationships.Count == 0) { var empty = new Label("None yet"); empty.AddToClassList("toys-caption"); parent.Add(empty); }
        foreach (var relationship in relationships)
        {
            string id = relationship.Member.Id;
            var row = new VisualElement(); row.AddToClassList("settings-friend-row");
            var name = new Label(relationship.Member.Profile?.Name ?? "Player"); name.enableRichText = false; row.Add(name);
            if (publicPlayerIds.TryGetValue(id, out string publicId))
            {
                var idLabel = new Label(publicId); idLabel.AddToClassList("settings-friend-id"); row.Add(idLabel);
            }
            var actions = new VisualElement(); actions.AddToClassList("settings-friend-actions"); row.Add(actions);
            if (type == "incoming")
            {
                AddAction(actions, "Accept", async () => { await FriendsService.Instance.AddFriendAsync(id); }, "Friend request accepted.");
                AddAction(actions, "Decline", () => FriendsService.Instance.DeleteIncomingFriendRequestAsync(id), "Friend request declined.");
            }
            else if (type == "outgoing") AddAction(actions, "Cancel Request", () => FriendsService.Instance.DeleteOutgoingFriendRequestAsync(id), "Friend request cancelled.");
            else AddAction(actions, "Remove", () => FriendsService.Instance.DeleteFriendAsync(id), "Friend removed.");
            parent.Add(row);
        }
    }

    private void AddAction(VisualElement parent, string text, Func<Task> operation, string success)
    {
        var button = new Button(() => Run(async () => { await operation(); RenderFriends(); }, success)) { text = text };
        button.AddToClassList("toys-button"); button.SetEnabled(!busy); parent.Add(button);
    }

    private async void Run(Func<Task> operation, string success, bool deleting = false)
    {
        if (busy) return;
        busy = true;
        int current = generation;
        var message = deleting ? root.Q<Label>("DeleteStatus") : status;
        message.text = deleting ? "Deleting your account and data…" : "Please wait…";
        SetBusy(true);
        try
        {
            await operation();
            if (this && current == generation) message.text = success;
        }
        catch (Exception ex)
        {
            if (this && current == generation) message.text = AuthenticationScreen.FriendlyError(ex);
        }
        finally
        {
            if (this && current == generation)
            {
                busy = false;
                SetBusy(false);
                root.Q<Button>("ConfirmDeleteButton").SetEnabled(root.Q<TextField>("DeleteConfirmationText").value == "CONFIRM");
            }
        }
    }

    private void SetBusy(bool value)
    {
        foreach (var button in overlay.Query<Button>().ToList()) button.SetEnabled(!value);
        root.Q<TextField>("DeleteConfirmationText").SetEnabled(!value);
        root.Q<TextField>("FriendPlayerId").SetEnabled(!value);
    }

    private void SetLobbyEnabled(bool value)
    {
        root.Q<Button>("SettingsButton").SetEnabled(value);
        root.Q<Button>("CreateJoinRoomButton").SetEnabled(value);
        root.Q<TextField>("RoomNameTextField").SetEnabled(value);
    }
    private void Show(string name, bool visible) { root.Q(name).EnableInClassList("toys-hidden", !visible); }
}
