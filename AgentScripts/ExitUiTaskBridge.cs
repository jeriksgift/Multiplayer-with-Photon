using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class ExitUiTaskBridge
{
    const string Folder = "Assets/UI Toolkit/MyUIToolkits/";
    const string Command = "AgentScripts/exit-ui-command.txt";
    const string Result = "AgentScripts/exit-ui-result.txt";
    static bool busy;
    static ExitUiTaskBridge() { EditorApplication.update += Tick; }
    static async void Tick()
    {
        if (busy || EditorApplication.isCompiling || !File.Exists(Command)) return;
        busy = true;
        string command = File.ReadAllText(Command).Trim();
        File.Delete(Command);
        try
        {
            string result;
            switch (command)
            {
                case "setup": result = Setup(); break;
                case "play":
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
                    EditorApplication.isPlaying = true;
                    result = "Entering Game Play mode"; break;
                case "test": result = await Verify(); break;
                case "confirm": result = await VerifyConfirm(); break;
                case "stop": EditorApplication.isPlaying = false; result = "Stopping Play mode"; break;
                case "restore": EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity"); result = "Restored Lobby"; break;
                default: throw new Exception("Unknown command: " + command);
            }
            File.WriteAllText(Result, "PASS: " + result);
        }
        catch (Exception error) { File.WriteAllText(Result, "FAIL: " + error); Debug.LogException(error); }
        finally { busy = false; }
    }

    static void Configure(ToysGameUI ui, PanelSettings settings, VisualTreeAsset tree)
    {
        if (ui.exitOverlay) ui.exitOverlay.SetActive(false);
        var child = ui.transform.Find("ExitConfirmationDocument");
        if (!child) { child = new GameObject("ExitConfirmationDocument").transform; child.SetParent(ui.transform, false); }
        child.gameObject.SetActive(false);
        var doc = child.GetComponent<UIDocument>() ?? child.gameObject.AddComponent<UIDocument>();
        doc.panelSettings = settings;
        doc.visualTreeAsset = tree;
        var controller = child.GetComponent<ExitConfirmationScreen>() ?? child.gameObject.AddComponent<ExitConfirmationScreen>();
        var so = new SerializedObject(controller);
        so.FindProperty("gameUI").objectReferenceValue = ui;
        so.ApplyModifiedPropertiesWithoutUndo();
        ui.exitOverlay = child.gameObject;
        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(doc);
        EditorUtility.SetDirty(controller);
    }

    static string Setup()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before setup.");
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "ExitConfirmationPanelSettings.asset");
        if (!settings)
        {
            settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "LobbyRuntimePanelSettings.asset"));
            settings.name = "Exit Confirmation Panel Settings";
            AssetDatabase.CreateAsset(settings, Folder + "ExitConfirmationPanelSettings.asset");
        }
        settings.sortingOrder = 50;
        EditorUtility.SetDirty(settings);
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "exit-confirmation.uxml");
        if (!tree) throw new Exception("Exit UXML failed to import.");
        const string path = "Assets/Resources/PlayerWithGFX.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try { Configure(prefab.GetComponent<ToysGameUI>(), settings, tree); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Additive);
        foreach (var ui in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ToysGameUI>(true))) Configure(ui, settings, tree);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        return "Exit UXML connected in Lobby and shared player prefab; blue/red inherit shared crosshair.";
    }

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Click(Button button) { button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt); }
    static async Task<string> Verify()
    {
        Check(Application.isPlaying, "Requires Play mode.");
        Application.runInBackground = true;
        if (!PhotonNetwork.InRoom) { PhotonNetwork.OfflineMode = true; PhotonNetwork.NickName = "VIPER"; PhotonNetwork.CreateRoom("CROSSHAIR-CHECK"); }
        GameObject player = null;
        foreach (var team in new[] { DataTypes.Team.Blue, DataTypes.Team.Red })
        {
            if (player) { PhotonNetwork.Destroy(player); await Task.Delay(100); }
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomManager.KeyTeam, (int)team }, { RoomManager.KeyTeamIndex, 0 } });
            var spawn = GameManager.Instance.GetSpawnLoc();
            player = PhotonNetwork.Instantiate(team == DataTypes.Team.Blue ? "PlayerWithGFX" : "PlayerWithGFX_Red", spawn.position, spawn.rotation);
            await Task.Delay(500);
            var root = player.transform.Find("GameHudDocument").GetComponent<UIDocument>().rootVisualElement;
            var cross = root.Q("Crosshair");
            Check(cross != null && cross.worldBound.width == 24 && cross.worldBound.height == 24, team + " crosshair has no bounds");
            Check(Vector2.Distance(cross.worldBound.center, root.worldBound.center) < 1f, team + " crosshair is off center");
            Check(cross.Q(className: "crosshair-horizontal").resolvedStyle.backgroundColor == Color.white, "Crosshair is not white");
            var ui = player.GetComponent<ToysGameUI>();
            ui.ShowExit(); await Task.Delay(100);
            Check(ui.IsExitOpen && player.GetComponent<InputManager>().IsGameplayBlocked && UnityEngine.Cursor.visible, "Exit did not block input");
            var dialog = ui.exitOverlay.GetComponent<UIDocument>().rootVisualElement;
            Check(dialog.Q<Label>("ExitTitle").text == "Leave match?", "Match title incorrect");
            Check(dialog.Q<Label>("ExitRoom").text == "CROSSHAIR-CHECK", "Room label incorrect");
            int before = Time.frameCount; await Task.Delay(100); Check(Time.frameCount > before && Time.timeScale == 1f, "Dialog paused game");
            Click(dialog.Q<Button>("CancelExitButton"));
            Check(!ui.IsExitOpen && !player.GetComponent<InputManager>().IsGameplayBlocked, "Cancel did not restore input");
            ui.ShowExit(); await Task.Delay(100); ui.CancelExit();
            if (team == DataTypes.Team.Blue)
            {
                ScreenCapture.CaptureScreenshot("AgentScripts/BlueCrosshair-Verified.png");
                await Task.Delay(200);
            }
        }
        player.GetComponent<ToysGameUI>().ShowExit();
        await Task.Delay(100);
        ScreenCapture.CaptureScreenshot("AgentScripts/ExitConfirmation-Verified.png");
        await Task.Delay(200);
        return "Blue/red centered white outlined crosshairs, exit dialog, room label, input blocking, cancel/reopen, and continued match frames passed.";
    }

    static async Task<string> VerifyConfirm()
    {
        var player = UnityEngine.Object.FindObjectsByType<GameHudScreen>().Single(x => x.GetComponent<PhotonView>().IsMine);
        var ui = player.GetComponent<ToysGameUI>();
        if (!ui.IsExitOpen) ui.ShowExit();
        Click(ui.exitOverlay.GetComponent<UIDocument>().rootVisualElement.Q<Button>("ConfirmExitButton"));
        for (int i = 0; i < 70 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Lobby"; i++) await Task.Delay(100);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby" && !PhotonNetwork.InRoom, "Confirm did not return to Lobby");
        await Task.Delay(300);
        var lobbyUI = UnityEngine.Object.FindObjectsByType<ToysGameUI>().Single(x => !x.matchInterface);
        lobbyUI.ShowExit();
        await Task.Delay(100);
        var root = lobbyUI.exitOverlay.GetComponent<UIDocument>().rootVisualElement;
        Check(root.Q<Label>("ExitTitle").text == "Exit game?" && root.Q<Button>("ConfirmExitButton").text == "Exit Game", "Lobby quit presentation incorrect");
        Check(root.Q<Label>("LiveMatchNotice").ClassListContains("exit-hidden"), "Lobby shows match-only note");
        Click(root.Q<Button>("CancelExitButton"));
        Check(!lobbyUI.IsExitOpen, "Lobby cancel failed");
        return "Actual Confirm button leaves match and returns to Lobby; lobby Exit Game presentation and Cancel passed.";
    }
}
