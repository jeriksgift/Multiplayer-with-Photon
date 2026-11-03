using System;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public static class IntegrateMatchResults
{
    const string Folder = "Assets/UI Toolkit/MyUIToolkits/";
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static string Setup()
    {
        Check(!Application.isPlaying, "Stop Play mode before setup.");
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "match-results.uxml");
        Check(tree, "Results UXML failed to import.");
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "MatchResultsPanelSettings.asset");
        if (!settings)
        {
            settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "LobbyRuntimePanelSettings.asset"));
            settings.name = "Match Results Panel Settings";
            AssetDatabase.CreateAsset(settings, Folder + "MatchResultsPanelSettings.asset");
        }
        settings.sortingOrder = 60;
        EditorUtility.SetDirty(settings);
        const string path = "Assets/Resources/PlayerWithGFX.prefab";
        var player = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var child = player.transform.Find("MatchResultsDocument");
            if (!child) { child = new GameObject("MatchResultsDocument").transform; child.SetParent(player.transform, false); }
            child.gameObject.SetActive(false);
            var doc = child.GetComponent<UIDocument>() ?? child.gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.visualTreeAsset = tree;
            var controller = player.GetComponent<MatchResultsScreen>() ?? player.AddComponent<MatchResultsScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("document").objectReferenceValue = doc;
            so.ApplyModifiedPropertiesWithoutUndo();
            player.transform.Find("Canvas/PlayerHUD/MatchEndPanel").gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(player, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        foreach (var name in new[] { "PlayerWithGFX", "PlayerWithGFX_Red" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/" + name + ".prefab");
            Check(prefab.GetComponent<MatchResultsScreen>() && prefab.transform.Find("MatchResultsDocument"), "Missing results on " + name);
        }
        return "Results document integrated into both team prefabs.";
    }
    public static string PreparePlay()
    {
        Check(!Application.isPlaying, "Already playing");
        Check(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "Save current scene before preview");
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        return "Game scene ready";
    }
    public static async Task<string> Verify()
    {
        Check(Application.isPlaying, "Requires Play mode");
        Application.runInBackground = true;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Game")
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
            await Task.Delay(500);
        }
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            for (int i = 0; i < 50 && PhotonNetwork.IsConnected; i++) await Task.Delay(100);
        }
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.NickName = "VIPER";
        PhotonNetwork.CreateRoom("TOYBOX-01");
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomManager.KeyTeam, 0 }, { RoomManager.KeyTeamIndex, 0 } });
        var spawn = GameManager.Instance.GetSpawnLoc();
        var player = PhotonNetwork.Instantiate("PlayerWithGFX", spawn.position, spawn.rotation);
        await Task.Delay(600);
        GameManager.Instance.blueKills = 20;
        GameManager.Instance.redKills = 14;
        GameManager.Instance.matchEnded = true;
        var ui = player.GetComponent<ToysGameUI>();
        ui.ShowExit();
        var actor = PhotonNetwork.LocalPlayer.ActorNumber;
        string stats = actor + ":0:12:1240;9:0:8:850;10:1:9:960;11:1:5:640";
        GameManager.OnMatchEnded?.Invoke(1, stats);
        await Task.Delay(350);
        var doc = player.transform.Find("MatchResultsDocument").GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        Check(doc.gameObject.activeInHierarchy, "Results hidden");
        Check(!ui.IsExitOpen, "Exit overlay remained open");
        Check(!player.transform.Find("GameHudDocument").gameObject.activeSelf, "HUD remained open");
        Check(!player.transform.Find("Canvas/PlayerHUD/MatchEndPanel").gameObject.activeSelf, "Legacy panel remained open");
        Check(root.Q<Label>("WinnerTitle").text == "Blue Team Wins!", "Blue title incorrect");
        Check(root.Q<Label>("BlueFinalScore").text == "20" && root.Q<Label>("RedFinalScore").text == "14", "Final scores incorrect");
        Check(root.Q("BluePlayers").childCount == 2 && root.Q("RedPlayers").childCount == 2, "Stats rows missing");
        Check(root.Q("BluePlayers").Q<Label>(className: "player-name").text == "VIPER (YOU)", "Local name incorrect");
        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Cursor locked");
        int frames = Time.frameCount;
        await Task.Delay(150);
        Check(Time.frameCount > frames, "Game frames stalled");
        ScreenCapture.CaptureScreenshot("AgentScripts/MatchResults-Verified.png");
        await Task.Delay(250);
        player.GetComponent<MatchResultsScreen>().Show(2, stats);
        Check(root.Q<Label>("WinnerTitle").text == "Red Team Wins!" && root.Q("RedCard").ClassListContains("winning-team"), "Red winner incorrect");
        player.GetComponent<MatchResultsScreen>().Show(0, "bad;12:0:invalid:1");
        Check(root.Q<Label>("WinnerTitle").text == "Draw!" && root.Q("BluePlayers").childCount == 1, "Draw/empty stats failed");
        player.GetComponent<MatchResultsScreen>().Show(1, stats);
        return "Blue/red winners, draw, live score/stat/name binding, malformed/empty stats, HUD/exit closure, cursor and advancing frames passed. " + root.worldBound;
    }
    public static async Task<string> VerifyReturn()
    {
        var player = UnityEngine.Object.FindObjectsByType<MatchResultsScreen>().Single(x => x.GetComponent<PhotonView>().IsMine);
        var button = player.transform.Find("MatchResultsDocument").GetComponent<UIDocument>().rootVisualElement.Q<Button>("ReturnToLobbyButton");
        button.Focus();
        using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt);
        Check(!button.enabledSelf, "Return button accepts repeated clicks");
        UnityEngine.Object.Destroy(player.gameObject);
        for (int i = 0; i < 70 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Lobby"; i++) await Task.Delay(100);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby" && !PhotonNetwork.InRoom, "Return button failed");
        await Task.Delay(400);
        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Lobby cursor is locked");
        Check(UnityEngine.Object.FindObjectsByType<MatchLobbyReturn>().Length == 0, "Return helper leaked");
        return "Return button left room and loaded Lobby even after player destruction; repeat clicks blocked, cursor unlocked, helper cleaned up.";
    }
}
