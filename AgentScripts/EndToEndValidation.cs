using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public static class EndToEndValidation
{
    sealed class Opponent : Player { public Opponent(string name, int actor) : base(name, actor, false) {} }
    const string Report = "AgentScripts/EndToEnd-validation.txt";
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Log(string text) { File.AppendAllText(Report, text + "\n"); }
    static void Click(Button button) { Check(button != null && button.enabledInHierarchy, "Button missing or disabled"); button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt); }
    static VisualElement Root<T>() where T : MonoBehaviour => UnityEngine.Object.FindAnyObjectByType<T>().GetComponent<UIDocument>().rootVisualElement;
    static void RefreshRoom() => typeof(RoomManager).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(RoomManager.Instance, null);

    public static async Task<string> Network()
    {
        File.WriteAllText(Report, "End-to-end verification " + DateTime.UtcNow.ToString("u") + "\n");
        Application.runInBackground = true;
        for (int i = 0; i < 300 && !PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.Disconnected; i++) await Task.Delay(100);
        string result = "ONLINE: state=" + PhotonNetwork.NetworkClientState + ", inLobby=" + PhotonNetwork.InLobby;
        Log(result);
        return result;
    }

    public static async Task<string> LobbyAndStart()
    {
        Check(Application.isPlaying && SceneManager.GetActiveScene().name == "Lobby", "Start in Lobby Play mode");
        var manager = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
        var root = Root<LobbyScreen>();
        var nickname = root.Q<TextField>("NickNameTextField");
        var room = root.Q<TextField>("RoomNameTextField");
        var enter = root.Q<Button>("CreateJoinRoomButton");
        nickname.value = "A"; room.value = "E2E"; Click(enter);
        Check(manager.StatusMessage.Contains("at least 2"), "Short nickname accepted");
        nickname.value = "E2E PLAYER";
        foreach (string value in new[] { "", "   ", "\u200b " })
        {
            room.value = value; Click(enter);
            Check(manager.StatusMessage == "ROOM NAME REQUIRED", "Blank room accepted");
        }
        manager.enabled = false;
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        for (int i = 0; i < 300 && PhotonNetwork.NetworkClientState != ClientState.Disconnected; i++) await Task.Delay(50);
        Check(PhotonNetwork.NetworkClientState == ClientState.Disconnected, "Cannot start offline test while connected");
        PhotonNetwork.OfflineMode = true;
        manager.enabled = true;
        LoadingManager.Instance.HideLoadingScreen();
        room.value = "  E2E-VALIDATION  "; Click(enter);
        await Task.Delay(200);
        Check(PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == "E2E-VALIDATION", "Room create/normalization failed");
        var team = Root<TeamSelectionScreen>();
        Check(!team.Q<Button>("ReadyButton").enabledSelf && !team.Q<Button>("StartButton").enabledSelf, "Ready/start available without slot");
        Click(team.Q<Button>("BlueSlot0"));
        Check(RoomManager.GetLocalTeam() == DataTypes.Team.Blue && RoomManager.GetSlot(PhotonNetwork.LocalPlayer) >= 0, "Blue selection failed");
        Click(team.Q<Button>("ReadyButton"));
        Check(RoomManager.GetReady(PhotonNetwork.LocalPlayer), "Ready failed");
        Click(team.Q<Button>("ReadyButton"));
        Check(!RoomManager.GetReady(PhotonNetwork.LocalPlayer), "Cancel ready failed");
        Click(team.Q<Button>("RedSlot0"));
        Check(RoomManager.GetLocalTeam() == DataTypes.Team.Red && !RoomManager.GetReady(PhotonNetwork.LocalPlayer), "Switch team failed");
        Click(team.Q<Button>("BlueSlot0")); Click(team.Q<Button>("ReadyButton"));
        Check(!RoomManager.Instance.CanStartMatch, "Single player can start");
        int redSlot = RoomManager.Instance.Slots.ToList().FindIndex(x => x.Team == DataTypes.Team.Red);
        var opponent = new Opponent("SIMULATED OPPONENT", 9);
        opponent.CustomProperties[RoomManager.KeySlot] = redSlot;
        opponent.CustomProperties[RoomManager.KeyTeam] = 1;
        opponent.CustomProperties[RoomManager.KeyTeamIndex] = 0;
        opponent.CustomProperties[RoomManager.KeyReady] = false;
        PhotonNetwork.CurrentRoom.StorePlayer(opponent); RefreshRoom();
        Check(!team.Q<Button>("RedSlot0").enabledSelf && !RoomManager.Instance.CanStartMatch, "Occupied slot/not-ready gate failed");
        opponent.CustomProperties[RoomManager.KeyReady] = true; RefreshRoom();
        Check(RoomManager.Instance.CanStartMatch && team.Q<Label>("BlueCount").text == "1 / 4" && team.Q<Label>("RedCount").text == "1 / 4", "Ready/team counts failed");
        Log("PASS: lobby fields, invalid inputs, normalization, room create, slots, switching teams, ready/cancel, occupied slots and both-team start gates (offline simulated opponent).");
        Click(team.Q<Button>("StartButton"));
        for (int i = 0; i < 100 && SceneManager.GetActiveScene().name != "Game"; i++) await Task.Delay(100);
        await Task.Delay(800);
        Check(SceneManager.GetActiveScene().name == "Game", "Start button did not load Game");
        Check(UnityEngine.Object.FindObjectsByType<GameHudScreen>().Count(x => x.GetComponent<PhotonView>().IsMine) == 1, "Local player missing or duplicated");
        Check(!UnityEngine.Object.FindAnyObjectByType<LoadingScreen>().IsVisible, "Loading overlay stuck after match start");
        float time = GameManager.Instance.remainingTime;
        await Task.Delay(1200);
        Check(GameManager.Instance.remainingTime < time, "Timer not ticking");
        Log("PASS: actual Start button, network scene loader, automatic player spawn, HUD, loading overlay dismissal and running match timer.");
        return "Lobby through match start passed.";
    }

    public static async Task<string> CombatAndWin()
    {
        var player = UnityEngine.Object.FindObjectsByType<GameHudScreen>().Single(x => x.GetComponent<PhotonView>().IsMine);
        var health = player.GetComponent<HealthComponent>();
        var hud = player.transform.Find("GameHudDocument").GetComponent<UIDocument>().rootVisualElement;
        health.Damage(25, player.transform.position, Vector3.up, -1);
        Check(hud.Q<Label>("HealthValue").text == "75", "Health HUD failed");
        health.Damage(100, player.transform.position, Vector3.up, 9);
        Check(health.IsDead && hud.Q<Label>("HealthValue").text == "0", "Lethal damage failed");
        await Task.Delay(2400);
        Check(!health.IsDead && hud.Q<Label>("HealthValue").text == "100", "Respawn/health reset failed");
        Check(GameManager.Instance.redKills == 1, "Kill did not increment opposing score");
        Check(hud.Q<Label>("RedScore").text == "1", "Score HUD failed");
        for (int i = 1; i < GameManager.WIN_SCORE; i++) GameManager.AddKill(9);
        await Task.Delay(200);
        Check(GameManager.Instance.matchEnded, "Score target did not end match");
        var results = player.transform.Find("MatchResultsDocument").GetComponent<UIDocument>();
        Check(results.gameObject.activeInHierarchy && results.rootVisualElement.Q<Label>("WinnerTitle").text == "Red Team Wins!", "Actual match result failed");
        Check(results.rootVisualElement.Q<Label>("RedFinalScore").text == GameManager.WIN_SCORE.ToString("00"), "Results final score incorrect");
        Check(!player.transform.Find("GameHudDocument").gameObject.activeSelf, "HUD remains over results");
        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Results cursor locked");
        Log("PASS: damage, lethal damage RPC, death, two-second owner respawn, health restore, kill credit, score HUD, real score-target match end and results.");
        Click(results.rootVisualElement.Q<Button>("ReturnToLobbyButton"));
        for (int i = 0; i < 100 && SceneManager.GetActiveScene().name != "Lobby"; i++) await Task.Delay(100);
        await Task.Delay(400);
        Check(SceneManager.GetActiveScene().name == "Lobby" && !PhotonNetwork.InRoom, "Return to Lobby failed");
        Check(UnityEngine.Object.FindObjectsByType<MatchLobbyReturn>().Length == 0, "Return object leaked");
        Check(!UnityEngine.Object.FindAnyObjectByType<LoadingScreen>().IsVisible, "Lobby loading stuck");
        Check(UnityEngine.Object.FindAnyObjectByType<LobbyScreen>().isActiveAndEnabled, "Lobby menu hidden");
        Log("PASS: actual results Return button, leave room, lobby menu restoration, loading dismissal and transition cleanup.");
        return "Combat, respawn, score-target win, results and return passed.";
    }

    public static async Task<string> ReplayAndLeave()
    {
        var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
        Check(lobby.TryEnterRoom("E2E REPLAY", "REPLAY-ROOM"), "Cannot create another room after return");
        await Task.Delay(150);
        var team = Root<TeamSelectionScreen>();
        Check(RoomManager.GetSlot(PhotonNetwork.LocalPlayer) == -1 && !RoomManager.GetReady(PhotonNetwork.LocalPlayer), "Stale ready/slot from previous match");
        Click(team.Q<Button>("LeaveButton"));
        await Task.Delay(200);
        Check(!PhotonNetwork.InRoom && UnityEngine.Object.FindAnyObjectByType<LobbyScreen>().isActiveAndEnabled, "Leave room failed");
        var menu = UnityEngine.Object.FindObjectsByType<ToysGameUI>().Single(x => !x.matchInterface);
        menu.ShowExit(); await Task.Delay(100);
        var exit = menu.exitOverlay.GetComponent<UIDocument>().rootVisualElement;
        Check(exit.Q<Label>("ExitTitle").text == "Exit game?", "Lobby quit title incorrect");
        Click(exit.Q<Button>("CancelExitButton"));
        Check(!menu.IsExitOpen && Time.timeScale == 1f, "Lobby cancel failed");
        Log("PASS: second room after match, cleared team/ready properties, actual Leave button, lobby quit dialog and Cancel. Quit deliberately not invoked in shared Editor.");
        return "Replay/leave/quit-cancel passed.";
    }

    public static async Task<string> PrepareTeamTest()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("Game");
        await Task.Delay(400);
        return "Game scene loaded without a room for two-team tests";
    }

    public static string Audit()
    {
        Check(!Application.isPlaying, "Audit in Edit mode");
        int count = 0;
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + path + "/" + t.name);
            count++;
        }
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Check(enabled.SequenceEqual(new[] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/Game.unity" }), "Build scene order incorrect");
        foreach (var path in enabled)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                foreach (var obj in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj.gameObject) == 0, "Missing script in " + path + "/" + obj.name);
            }
            finally { if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }
        string result = "PASS: " + count + " Resources prefabs and both build scenes have no missing scripts; Lobby/Game build scene order correct.";
        Log(result);
        return result;
    }

    public static async Task<string> TimerAndEndInput()
    {
        if (!PhotonNetwork.InRoom)
        {
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("TIMER-CHECK");
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomManager.KeyTeam, 0 }, { RoomManager.KeyTeamIndex, 0 } });
        }
        SceneManager.LoadScene("Game");
        await Task.Delay(700);
        var player = UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().Single(x => x.photonView.IsMine);
        GameManager.Instance.remainingTime = .1f;
        await Task.Delay(1200);
        Check(GameManager.Instance.matchEnded, "Timer did not end match");
        var results = player.transform.Find("MatchResultsDocument").GetComponent<UIDocument>();
        Check(results.gameObject.activeInHierarchy && results.rootVisualElement.Q<Label>("WinnerTitle").text == "Draw!", "Timer draw failed");
        var oldEditor = UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
        UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard); UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);
            var before = player.transform.position;
            int shots = player.Shots;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            await Task.Delay(250);
            Check(player.Shots == shots, "Player can shoot after match ends");
            Check(Vector2.Distance(new Vector2(before.x,before.z),new Vector2(player.transform.position.x,player.transform.position.z)) < .08f, "Player can move after match ends");
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new UnityEngine.InputSystem.LowLevel.MouseState());
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = oldBackground;
        }
        Log("PASS: real timer expiry produces Draw; gameplay input blocked under results.");
        return "Timer draw and match-end input passed.";
    }

    public static string Build()
    {
        Check(!Application.isPlaying, "Stop before build");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/Game.unity" },
            locationPathName = "Builds/EndToEndValidation/DeathMatch.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        string result = "BUILD: " + report.summary.result + "; errors=" + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings + "; size=" + report.summary.totalSize + "; time=" + report.summary.totalTime;
        Log(result);
        Check(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded, result);
        return result;
    }

    public static async Task<string> DisconnectFeedback()
    {
        var manager = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
        LoadingManager.Instance.ShowLoadingScreen("Connection test");
        manager.OnDisconnected(DisconnectCause.ClientTimeout);
        await Task.Delay(100);
        Check(!UnityEngine.Object.FindAnyObjectByType<LoadingScreen>().IsVisible, "Connection failure leaves loading overlay stuck");
        Check(manager.StatusMessage.Contains("reconnect") && UnityEngine.Object.FindAnyObjectByType<LobbyScreen>().isActiveAndEnabled, "Connection failure has no recovery message");
        Log("PASS: connection failure dismisses loading and restores lobby with reconnect guidance.");
        return "Disconnect feedback passed";
    }
}
