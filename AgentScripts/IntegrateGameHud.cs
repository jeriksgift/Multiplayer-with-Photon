using System;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class IntegrateGameHud
{
    const string Folder = "Assets/UI Toolkit/MyUIToolkits/";
    public static string Setup()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        AssetDatabase.Refresh();
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "game-hud.uxml");
        if (!tree) throw new Exception("HUD UXML failed to import.");
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "GameHudPanelSettings.asset");
        if (!settings)
        {
            settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "LobbyRuntimePanelSettings.asset"));
            settings.name = "Game HUD Panel Settings";
            AssetDatabase.CreateAsset(settings, Folder + "GameHudPanelSettings.asset");
        }
        settings.sortingOrder = -1;
        EditorUtility.SetDirty(settings);
        const string path = "Assets/Resources/PlayerWithGFX.prefab";
        var player = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var hud = player.transform.Find("Canvas/PlayerHUD");
            var exit = player.GetComponent<ToysGameUI>().exitOverlay;
            foreach (Transform child in hud)
            {
                if (child.name == "MatchEndPanel" || (exit && (exit.transform == child || exit.transform.IsChildOf(child)))) continue;
                child.gameObject.SetActive(false);
            }
            var childDoc = player.transform.Find("GameHudDocument");
            if (!childDoc) { childDoc = new GameObject("GameHudDocument").transform; childDoc.SetParent(player.transform, false); }
            var doc = childDoc.GetComponent<UIDocument>() ?? childDoc.gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.visualTreeAsset = tree;
            childDoc.gameObject.SetActive(false);
            var controller = player.GetComponent<GameHudScreen>() ?? player.AddComponent<GameHudScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("document").objectReferenceValue = doc;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        foreach (var name in new[] { "PlayerWithGFX", "PlayerWithGFX_Red" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/" + name + ".prefab");
            if (!prefab.GetComponent<GameHudScreen>() || !prefab.transform.Find("GameHudDocument")) throw new Exception("HUD missing on " + name);
        }
        return "UXML HUD integrated into both team prefabs. Existing result and exit overlays preserved.";
    }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task<string> Verify()
    {
        Check(Application.isPlaying, "Requires Play mode in Game scene");
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.NickName = "VIPER";
        PhotonNetwork.CreateRoom("TOYBOX-01");
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomManager.KeyTeam, 1 }, { RoomManager.KeyTeamIndex, 0 } });
        var spawn = GameManager.Instance.GetSpawnLoc();
        var player = PhotonNetwork.Instantiate("PlayerWithGFX_Red", spawn.position, spawn.rotation);
        await Task.Delay(500);
        var doc = player.transform.Find("GameHudDocument").GetComponent<UIDocument>();
        Check(doc.gameObject.activeInHierarchy, "Local HUD hidden");
        var root = doc.rootVisualElement;
        GameManager.OnScoreChanged?.Invoke(7, 5);
        GameManager.OnTimerChanged?.Invoke(487);
        player.GetComponent<HealthComponent>().Damage(15, player.transform.position, Vector3.up, -1);
        Check(root.Q<Label>("BlueScore").text == "7" && root.Q<Label>("RedScore").text == "5", "Score binding failed");
        Check(root.Q<Label>("MatchTimer").text == "08:07", "Timer binding failed");
        Check(root.Q<Label>("HealthValue").text == "85", "Health binding failed");
        Check(Mathf.Approximately(root.Q("HealthFill").style.width.value.value, 85), "Health fill failed");
        Check(root.Q<Label>("ScoreTarget").text == "FIRST TO " + GameManager.WIN_SCORE, "Target incorrect");
        var button = root.Q<Button>("MenuButton");
        button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt);
        Check(player.GetComponent<ToysGameUI>().IsExitOpen, "Menu button failed");
        player.GetComponent<ToysGameUI>().CancelExit();
        int before = Time.frameCount; await Task.Delay(100); Check(Time.frameCount > before, "Game not advancing");
        return "Red variant HUD: live scores 7/5, timer 08:07, health 85%, score target, menu/cancel and advancing frames passed. " + root.worldBound;
    }

    public static string VerifyMatchEnd()
    {
        var player = UnityEngine.Object.FindObjectsByType<GameHudScreen>().Single(x => x.GetComponent<PhotonView>().IsMine);
        GameManager.OnMatchEnded?.Invoke(1, "");
        Check(!player.transform.Find("GameHudDocument").gameObject.activeSelf, "HUD not hidden at match end");
        Check(player.transform.Find("Canvas/PlayerHUD/MatchEndPanel").gameObject.activeInHierarchy, "Existing results missing");
        return "HUD hides and existing result screen remains functional on match end.";
    }
    public static async Task<string> VerifyBlue()
    {
        var old = UnityEngine.Object.FindObjectsByType<GameHudScreen>().Single(x => x.GetComponent<PhotonView>().IsMine);
        PhotonNetwork.Destroy(old.gameObject);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomManager.KeyTeam, 0 }, { RoomManager.KeyTeamIndex, 0 } });
        var spawn = GameManager.Instance.GetSpawnLoc();
        var player = PhotonNetwork.Instantiate("PlayerWithGFX", spawn.position, spawn.rotation);
        await Task.Delay(300);
        var root = player.transform.Find("GameHudDocument").GetComponent<UIDocument>().rootVisualElement;
        Check(root.Q<Label>("LocalTeam").text == "BLUE TEAM / DEATHMATCH", "Blue team label failed");
        Check(root.Q<Label>("HealthValue").text == "100", "Fresh health failed");
        player.GetComponent<HealthComponent>().ApplyLifeState(1, true, player.transform.position, player.transform.rotation);
        player.GetComponent<HealthComponent>().ApplyLifeState(2, false, player.transform.position, player.transform.rotation);
        Check(root.Q<Label>("HealthValue").text == "100", "Respawn health reset failed");
        return "Blue variant HUD, local team label, fresh health and respawn reset passed.";
    }
}
