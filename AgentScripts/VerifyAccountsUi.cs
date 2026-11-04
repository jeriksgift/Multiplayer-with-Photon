using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifyAccountsUi
{
    const string Ui = "Assets/UI Toolkit/MyUIToolkits/";
    public static async Task<string> Profile()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        foreach (var bad in new[] { ("ValidName", 15), ("ValidName", 0), ("x", 16), ("<bad>", 16), ("bad name", 16) })
        {
            bool rejected = false;
            try { await AccountSession.Instance.CompleteProfileAsync(bad.Item1, bad.Item2); }
            catch (ArgumentException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid profile was accepted: " + bad);
        }
        var doc = UnityEngine.Object.FindAnyObjectByType<AuthenticationScreen>().GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        root.Q("SignInPage").AddToClassList("toys-hidden");
        root.Q("ProfilePage").RemoveFromClassList("toys-hidden");
        root.Q<TextField>("ProfileNickname").SetValueWithoutNotify("ToyPlayer");
        root.Q<IntegerField>("ProfileAge").SetValueWithoutNotify(16);
        return "Five invalid nickname/age cases rejected before contacting a service; profile form ready for visual inspection.";
    }

    public static string Settings()
    {
        var existing = GameObject.Find("Account Settings Verification");
        if (existing) UnityEngine.Object.DestroyImmediate(existing);
        var go = new GameObject("Account Settings Verification");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Ui + "LobbyRuntimePanelSettings.asset"));
        doc.panelSettings.sortingOrder = 20;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Ui + "lobby.uxml");
        go.AddComponent<LobbySettingsScreen>();
        var root = doc.rootVisualElement;
        root.Q("SettingsOverlay").RemoveFromClassList("toys-hidden");
        root.Q<Label>("AccountNickname").text = "ToyPlayer";
        root.Q<Label>("AccountType").text = "Guest (Anonymous)";
        root.Q<Label>("AccountPlayerId").text = "1000000000";
        var settings = PlayerSettingsService.Instance;
        float master = settings.Master, music = settings.Music, sfx = settings.Sfx;
        int graphics = settings.Graphics;
        Click(root, "ControlsTab");
        if (root.Q("ControlsPage").ClassListContains("toys-hidden") || root.Q("ControlsPage").childCount != 0)
            throw new Exception("Controls tab should be visible and empty.");
        Click(root, "SoundTab");
        root.Q<Slider>("MasterVolume").value = .4f;
        root.Q<Slider>("MusicVolume").value = .3f;
        root.Q<Slider>("SfxVolume").value = .2f;
        if (Math.Abs(AudioListener.volume - .4f) > .001f || Math.Abs(settings.Music - .3f) > .001f || Math.Abs(settings.Sfx - .2f) > .001f)
            throw new Exception("Volume sliders did not update the settings service.");
        for (int i = 0; i < 4; i++)
        {
            Click(root, "Graphics" + PlayerSettingsService.GraphicsOptions[i]);
            if (QualitySettings.names[QualitySettings.GetQualityLevel()] != PlayerSettingsService.GraphicsOptions[i])
                throw new Exception("Graphics selection did not apply the corresponding Unity quality preset.");
        }
        settings.SetVolumes(master, music, sfx);
        settings.SetGraphics(graphics);
        Click(root, "AccountsTab");
        Click(root, "OpenDeleteButton");
        var confirmation = root.Q<TextField>("DeleteConfirmationText");
        confirmation.value = "confirm";
        if (root.Q<Button>("ConfirmDeleteButton").enabledSelf) throw new Exception("Lowercase confirmation must not enable deletion.");
        confirmation.value = "CONFIRM";
        if (!root.Q<Button>("ConfirmDeleteButton").enabledSelf) throw new Exception("Exact confirmation should enable deletion.");
        Click(root, "CancelDeleteButton");
        if (!root.Q("DeleteConfirmationOverlay").ClassListContains("toys-hidden")) throw new Exception("Cancel did not close confirmation.");
        return "Verified empty Controls tab, three volume sliders, all four actual graphics presets, case-sensitive deletion guard, and cancellation. No account was created or deleted.";
    }

    public static string Tab(string name)
    {
        var root = GameObject.Find("Account Settings Verification").GetComponent<UIDocument>().rootVisualElement;
        Click(root, name + "Tab");
        return name;
    }

    public static string Sound() { return Tab("Sound"); }
    public static string Graphics() { return Tab("Graphics"); }
    public static string DeleteDialog()
    {
        var root = GameObject.Find("Account Settings Verification").GetComponent<UIDocument>().rootVisualElement;
        Click(root, "OpenDeleteButton");
        return "Deletion confirmation dialog opened without submitting.";
    }

    private static void Click(VisualElement root, string name)
    {
        var clickable = root.Q<Button>(name).clickable;
        var method = typeof(Clickable).GetMethod("SimulateSingleClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null) throw new Exception("Button click simulation is unavailable.");
        method.Invoke(clickable, new object[] { null, 0 });
    }
}
