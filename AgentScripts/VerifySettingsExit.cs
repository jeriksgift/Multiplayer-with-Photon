using System;
using System.Reflection;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public static class VerifySettingsExit
{
    public static void Prepare() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    private static void Click(Button button)
    {
        typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button.clickable, new object[] { null, 0 });
    }

    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode after Prepare.");
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("Settings verification");
        var player = PhotonNetwork.Instantiate("PlayerWithGFX", Vector3.zero, Quaternion.identity);
        await Task.Delay(500);
        var hud = player.GetComponent<GameHudScreen>();
        var menu = player.GetComponent<ToysGameUI>();
        var document = player.transform.Find("GameHudDocument").GetComponent<UIDocument>();
        var root = document.rootVisualElement;
        var settings = document.GetComponent<LobbySettingsScreen>();
        if (root.Q<Button>("LeaveMatchButton") != null) throw new Exception("Exit still in HUD.");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Click(root.Q<Button>("MenuButton"));
        await Task.Delay(100);
        var exit = root.Q<Button>("ExitFromSettingsButton");
        if (!settings.IsOpen || !hud.IsPanelOpen || exit.text != "Leave Match" || exit.resolvedStyle.display == DisplayStyle.None)
            throw new Exception("Settings exit is unavailable.");
        Click(exit);
        await Task.Delay(100);
        if (settings.IsOpen || hud.IsPanelOpen || !menu.IsExitOpen)
            throw new Exception("Exit confirmation did not open after Settings closed.");
        menu.CancelExit();
        if (menu.IsExitOpen || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            throw new Exception("Cancel did not restore gameplay cursor.");
        settings.Open();
        if (!settings.IsOpen) throw new Exception("Settings cannot reopen after cancel.");
        settings.Close();
        return "No exit button in HUD. Settings > Leave Match opens confirmation; cancel restores gameplay and Settings can reopen.";
    }
}
