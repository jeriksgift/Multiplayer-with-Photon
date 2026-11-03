using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class IntegrateLoadingToolkit
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
            var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LoadingManager>(true)).Single();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "loading.uxml");
            if (!tree) throw new Exception("Loading UXML failed to import.");
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "LoadingPanelSettings.asset");
            if (!settings)
            {
                settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "LobbyRuntimePanelSettings.asset"));
                settings.name = "Loading Panel Settings";
                AssetDatabase.CreateAsset(settings, Folder + "LoadingPanelSettings.asset");
            }
            settings.sortingOrder = 100;
            EditorUtility.SetDirty(settings);
            var child = manager.transform.Find("LoadingDocument");
            if (!child)
            {
                child = new GameObject("LoadingDocument").transform;
                child.SetParent(manager.transform, false);
            }
            var doc = child.GetComponent<UIDocument>() ?? child.gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.visualTreeAsset = tree;
            var screen = child.GetComponent<LoadingScreen>() ?? child.gameObject.AddComponent<LoadingScreen>();
            var so = new SerializedObject(manager);
            so.FindProperty("loadingScreen").objectReferenceValue = screen;
            var oldPanel = (RectTransform)so.FindProperty("loadingPanel").objectReferenceValue;
            if (oldPanel) oldPanel.gameObject.SetActive(false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(doc);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Minimal loading UXML connected to the persistent LoadingManager; legacy loading panel disabled.";
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    public static string Show()
    {
        LoadingManager.Instance.ShowLoadingScreen("Connecting...");
        return "Loading visible";
    }

    public static string Verify()
    {
        var screen = UnityEngine.Object.FindAnyObjectByType<LoadingScreen>();
        var root = screen.GetComponent<UIDocument>().rootVisualElement;
        LoadingManager.Instance.ShowLoadingScreen("Synchronizing match...");
        if (!screen.IsVisible || root.Q<Label>("LoadingStatus").text != "Synchronizing match...") throw new Exception("Status failed");
        screen.SetProgress(.63f);
        if (root.Q<Label>("LoadingProgress").text != "63%") throw new Exception("Progress failed");
        LoadingManager.Instance.HideLoadingScreen();
        if (screen.IsVisible || !root.Q("LoadingPanel").ClassListContains("loading-hidden")) throw new Exception("Hide failed");
        LoadingManager.Instance.ShowLoadingScreen("Connecting...");
        if (root.Q<Label>("LoadingProgress").text != "") throw new Exception("Indeterminate reset failed");
        return "Show/hide, status updates, actual progress and reconnect reset passed.";
    }
}
