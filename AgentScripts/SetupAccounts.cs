using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class SetupAccounts
{
    const string Ui = "Assets/UI Toolkit/MyUIToolkits/";

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop play mode before setting up accounts.");
        AssetDatabase.Refresh();
        Directory.CreateDirectory("AgentScripts/Backups/Accounts");
        var lobby = SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");
        bool opened = !lobby.isLoaded;
        if (opened) lobby = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Additive);
        if (!File.Exists("AgentScripts/Backups/Accounts/Lobby-before-accounts.unity"))
            EditorSceneManager.SaveScene(lobby, "AgentScripts/Backups/Accounts/Lobby-before-accounts.unity", true);
        var document = lobby.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UIDocument>(true))
            .Single(d => d.GetComponent<LobbyScreen>());
        if (!document.GetComponent<LobbySettingsScreen>()) document.gameObject.AddComponent<LobbySettingsScreen>();
        EditorUtility.SetDirty(document.gameObject);
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);
        if (opened) EditorSceneManager.CloseScene(lobby, true);

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var config = AssetDatabase.LoadAssetAtPath<AccountServicesConfig>("Assets/Resources/AccountServicesConfig.asset");
        if (!config) AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AccountServicesConfig>(), "Assets/Resources/AccountServicesConfig.asset");
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(Ui + "AuthenticationScreen/AuthenticationPanelSettings.asset");
        if (!panel)
        {
            panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Ui + "LobbyRuntimePanelSettings.asset"));
            panel.name = "AuthenticationPanelSettings";
            panel.sortingOrder = 10;
            panel.clearColor = true;
            panel.colorClearValue = Color.black;
            AssetDatabase.CreateAsset(panel, Ui + "AuthenticationScreen/AuthenticationPanelSettings.asset");
        }
        string loginPath = "Assets/Scenes/Login.unity";
        if (!File.Exists(loginPath))
        {
            var previous = SceneManager.GetActiveScene();
            var login = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(login);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = Color.black;
            camera.transform.position = new Vector3(0, 0, -10);
            var root = new GameObject("AuthenticationDocument", typeof(UIDocument));
            var doc = root.GetComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Ui + "AuthenticationScreen/authentication-screen.uxml");
            root.AddComponent<AuthenticationScreen>();
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(login, loginPath);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(login, true);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(loginPath, true) }
            .Concat(EditorBuildSettings.scenes.Where(s => s.path != loginPath)).ToArray();
        SetupQuality();
        AssetDatabase.SaveAssets();
        return "Login scene first in build, lobby settings wired, config placeholders created, four quality presets installed. Existing open lobby state backed up before editing.";
    }

    private static void SetupQuality()
    {
        const string folder = "Assets/Settings/PlayerQuality";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Settings", "PlayerQuality");
        var original = QualitySettings.GetRenderPipelineAssetAt(1) as UniversalRenderPipelineAsset;
        if (!original) throw new Exception("Expected the existing PC URP pipeline.");
        var serialized = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = serialized.FindProperty("m_QualitySettings");
        for (int i = 0; i < 4; i++)
        {
            string name = PlayerSettingsService.GraphicsOptions[i];
            string path = folder + "/" + name + ".asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!pipeline)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), path);
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            }
            pipeline.renderScale = new[] { .65f, .85f, 1f, 1.15f }[i];
            pipeline.msaaSampleCount = new[] { 1, 2, 4, 8 }[i];
            pipeline.shadowDistance = new[] { 20f, 40f, 70f, 100f }[i];
            pipeline.shadowCascadeCount = new[] { 1, 2, 2, 4 }[i];
            pipeline.mainLightShadowmapResolution = new[] { 512, 1024, 2048, 4096 }[i];
            EditorUtility.SetDirty(pipeline);
            int index = -1;
            for (int j = 0; j < levels.arraySize; j++) if (levels.GetArrayElementAtIndex(j).FindPropertyRelative("name").stringValue == name) index = j;
            if (index < 0)
            {
                index = levels.arraySize;
                levels.InsertArrayElementAtIndex(index);
            }
            var level = levels.GetArrayElementAtIndex(index);
            level.FindPropertyRelative("name").stringValue = name;
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            level.FindPropertyRelative("lodBias").floatValue = new[] { .6f, 1f, 1.6f, 2f }[i];
            level.FindPropertyRelative("shadowDistance").floatValue = pipeline.shadowDistance;
            level.FindPropertyRelative("excludedTargetPlatforms").ClearArray();
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
