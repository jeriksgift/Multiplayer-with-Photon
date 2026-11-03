using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifyMainMenuExit
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Click(Button button) { button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt); }
    public static async Task<string> Run()
    {
        Application.runInBackground = true;
        LoadingManager.Instance.HideLoadingScreen();
        var screen = UnityEngine.Object.FindAnyObjectByType<LobbyScreen>();
        var root = screen.GetComponent<UIDocument>().rootVisualElement;
        var button = root.Q<Button>("ExitGameButton");
        Check(button != null && button.worldBound.width > 0 && button.worldBound.height > 0, "Exit button missing or invisible");
        var menu = UnityEngine.Object.FindAnyObjectByType<ToysGameUI>();
        for (int i = 0; i < 2; i++)
        {
            Click(button); await Task.Delay(100);
            Check(menu.IsExitOpen, "Exit button did not open confirmation");
            var dialog = menu.exitOverlay.GetComponent<UIDocument>().rootVisualElement;
            Check(dialog.Q<Label>("ExitTitle").text == "Exit game?" && dialog.Q<Button>("ConfirmExitButton").text == "Exit Game", "Incorrect confirmation");
            Click(dialog.Q<Button>("CancelExitButton"));
            Check(!menu.IsExitOpen && Time.timeScale == 1f, "Cancel did not restore menu");
        }
        ScreenCapture.CaptureScreenshot("AgentScripts/MainMenuExit-Verified.png");
        await Task.Delay(150);
        return "PASS: visible main-menu Exit button opens Exit Game confirmation; cancel and repeated reopen work.";
    }
}
