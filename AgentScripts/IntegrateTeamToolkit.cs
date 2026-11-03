using System;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class IntegrateTeamToolkit
{
    const string Folder = "Assets/UI Toolkit/MyUIToolkits/";
    public static string Setup()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Additive);
        try
        {
            var all = scene.GetRootGameObjects();
            var lobby = all.SelectMany(g => g.GetComponentsInChildren<LobbyManager>(true)).Single();
            var room = all.SelectMany(g => g.GetComponentsInChildren<RoomManager>(true)).Single();
            var canvas = all.Single(g => g.name == "Canvas");
            var oldRoom = canvas.transform.Find("Room").gameObject;
            if (room.transform.IsChildOf(oldRoom.transform)) throw new Exception("Room manager must remain active outside old UI.");
            var go = all.FirstOrDefault(g => g.name == "TeamSelectionDocument");
            if (!go) { go = new GameObject("TeamSelectionDocument"); SceneManager.MoveGameObjectToScene(go, scene); }
            var doc = go.GetComponent<UIDocument>() ?? go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "LobbyRuntimePanelSettings.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "team-selection.uxml");
            if (!doc.visualTreeAsset) throw new Exception("Team selection failed to import.");
            var screen = go.GetComponent<TeamSelectionScreen>() ?? go.AddComponent<TeamSelectionScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("roomManager").objectReferenceValue = room;
            so.FindProperty("lobbyManager").objectReferenceValue = lobby;
            so.ApplyModifiedPropertiesWithoutUndo();
            so = new SerializedObject(lobby);
            so.FindProperty("roomPanel").objectReferenceValue = go;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var ui in all.SelectMany(g => g.GetComponentsInChildren<ToysGameUI>(true)))
            {
                so = new SerializedObject(ui);
                so.FindProperty("teamSelectionPanel").objectReferenceValue = go;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            oldRoom.SetActive(false);
            go.SetActive(false);
            EditorUtility.SetDirty(doc);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Team UXML wired to existing room manager, lobby transitions and Escape; legacy visuals hidden.";
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static UIDocument Document() => SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "TeamSelectionDocument").GetComponent<UIDocument>();
    sealed class PreviewPlayer : Photon.Realtime.Player
    {
        public PreviewPlayer(string name, int actor) : base(name, actor, false) { }
    }
    public static string VerifyOpponent()
    {
        var manager = RoomManager.Instance;
        int redIndex = Enumerable.Range(0, manager.Slots.Count).First(i => manager.Slots[i].Team == DataTypes.Team.Red);
        var opponent = new PreviewPlayer("RIVAL", 2);
        opponent.CustomProperties[RoomManager.KeySlot] = redIndex;
        opponent.CustomProperties[RoomManager.KeyTeam] = (int)DataTypes.Team.Red;
        opponent.CustomProperties[RoomManager.KeyTeamIndex] = 0;
        opponent.CustomProperties[RoomManager.KeyReady] = true;
        PhotonNetwork.CurrentRoom.StorePlayer(opponent);
        manager.OnPlayerEnteredRoom(opponent);
        var root = Document().rootVisualElement;
        Check(root.Q<Button>("StartButton").enabledSelf, "Both teams ready did not enable Start");
        Check(!root.Q<Button>("RedSlot0").enabledSelf, "Opponent's slot remained selectable");
        Check(root.Q<Button>("RedSlot0").Q<Label>(className: "slot-name").text == "RIVAL", "Opponent name not rendered");
        opponent.CustomProperties[RoomManager.KeyReady] = false;
        manager.OnPlayerPropertiesUpdate(opponent, new ExitGames.Client.Photon.Hashtable());
        Check(!root.Q<Button>("StartButton").enabledSelf, "Unready opponent did not disable Start");
        return "Opponent name/occupancy, both-team start availability and unready gating passed.";
    }
    static void Click(Button button) { button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt); }
    public static async Task<string> Verify()
    {
        var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
        lobby.enabled = false;
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        for (int i = 0; i < 100 && PhotonNetwork.IsConnected; i++) await Task.Delay(30);
        PhotonNetwork.OfflineMode = true;
        lobby.enabled = true;
        Check(lobby.TryEnterRoom("JERIKS", "NIGHT SHIFT"), "Room entry failed");
        await Task.Delay(150);
        var doc = Document();
        Check(doc.gameObject.activeSelf, "New menu not opened");
        var root = doc.rootVisualElement;
        Click(root.Q<Button>("RedSlot0"));
        await Task.Delay(100);
        Check(RoomManager.GetLocalTeam() == DataTypes.Team.Red && RoomManager.GetLocalTeamIndex() == 0, "Red team selection failed");
        Click(root.Q<Button>("ReadyButton"));
        await Task.Delay(100);
        Check(RoomManager.GetReady(PhotonNetwork.LocalPlayer), "Ready failed");
        Check(!root.Q<Button>("StartButton").enabledSelf, "Start allowed without both teams");
        Click(root.Q<Button>("BlueSlot1"));
        await Task.Delay(100);
        Check(RoomManager.GetLocalTeam() == DataTypes.Team.Blue && RoomManager.GetLocalTeamIndex() == 1 && !RoomManager.GetReady(PhotonNetwork.LocalPlayer), "Switch team did not reset ready");
        Click(root.Q<Button>("BlueSlot1"));
        await Task.Delay(100);
        Check(RoomManager.GetSlot(PhotonNetwork.LocalPlayer) == -1 && !root.Q<Button>("ReadyButton").enabledSelf, "Deselect failed");
        Click(root.Q<Button>("LeaveButton"));
        await Task.Delay(150);
        Check(!doc.gameObject.activeSelf && !PhotonNetwork.InRoom, "Leave failed");
        Check(lobby.TryEnterRoom("JERIKS", "NIGHT SHIFT"), "Reopen failed");
        await Task.Delay(150);
        root = doc.rootVisualElement;
        Click(root.Q<Button>("BlueSlot0"));
        Click(root.Q<Button>("ReadyButton"));
        await Task.Delay(100);
        Check(RoomManager.GetSlot(PhotonNetwork.LocalPlayer) >= 0 && RoomManager.GetReady(PhotonNetwork.LocalPlayer), "Reopened controls failed");
        LoadingManager.Instance.HideLoadingScreen();
        return "UXML submit events passed: red/blue selection, ready, switch/reset, deselect, host gating, leave and reopen. " + root.Q("TeamPanel").worldBound;
    }
}
