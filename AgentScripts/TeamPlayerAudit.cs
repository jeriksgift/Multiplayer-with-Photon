using System;
using System.Linq;
using System.Text;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class TeamPlayerAudit
{
    public static string OpenGame()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        return "Game scene opened";
    }

    public static string RestoreLobby()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
        return "Lobby scene restored";
    }

    public static string RepairAssets()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before repairing assets.");
        const string bluePath = "Assets/Resources/PlayerWithGFX.prefab";
        var root = PrefabUtility.LoadPrefabContents(bluePath);
        try
        {
            var view = root.GetComponent<PhotonView>();
            root.GetComponent<PlayerInput>().enabled = false;
            var sync = root.GetComponent<PlayerNetworkSync>() ?? root.AddComponent<PlayerNetworkSync>();
            view.ObservedComponents = new System.Collections.Generic.List<Component> { sync, root.GetComponent<PlayerWithGFXPresentation>() };
            if (root.GetComponent<PhotonTransformView>()) UnityEngine.Object.DestroyImmediate(root.GetComponent<PhotonTransformView>());
            if (root.GetComponent<PhotonRigidbodyView>()) UnityEngine.Object.DestroyImmediate(root.GetComponent<PhotonRigidbodyView>());
            PrefabUtility.SaveAsPrefabAsset(root, bluePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var red = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PlayerWithGFX_Red.prefab");
        // Ground checks must use the same layers as the inherited movement/jump controller.
        foreach (var component in new Component[] { red.GetComponent<PlayerMovement>(), red.GetComponent<PlayerWithGFXPresentation>() })
        {
            var so = new SerializedObject(component);
            var mask = so.FindProperty(component is PlayerMovement ? "groundLayer" : "groundMask");
            if (mask.prefabOverride) PrefabUtility.RevertPropertyOverride(mask, InteractionMode.AutomatedAction);
        }
        AssetDatabase.SaveAssets();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Additive);
        try
        {
            var objects = scene.GetRootGameObjects();
            var manager = objects.SelectMany(g => g.GetComponentsInChildren<GameManager>(true)).Single();
            var so = new SerializedObject(manager);
            so.FindProperty("bluePlayerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            so.FindProperty("redPlayerPrefab").objectReferenceValue = red;
            so.ApplyModifiedPropertiesWithoutUndo();
            int previews = 0;
            foreach (var p in objects.SelectMany(g => g.GetComponentsInChildren<PlayerWithGFXPresentation>(true)))
            {
                p.gameObject.SetActive(false);
                previews++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Assigned both team prefabs; disabled " + previews + " placed preview players; both variants inherit PlayerNetworkSync and matching ground masks.";
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    public static string Inspect()
    {
        var report = new StringBuilder();
        report.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
        foreach (var name in new[] { "PlayerWithGFX", "PlayerWithGFX_Red" })
        {
            var root = Resources.Load<GameObject>(name);
            if (!root) throw new Exception("Missing Resources prefab: " + name);
            report.AppendLine(name + " active=" + root.activeSelf + " type=" + PrefabUtility.GetPrefabAssetType(root));
            foreach (var component in root.GetComponents<Component>())
            {
                if (!component) throw new Exception("Missing script: " + name);
                report.AppendLine("  " + component.GetType().Name);
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                        report.AppendLine("    " + property.propertyPath + "=" + (property.objectReferenceValue ? property.objectReferenceValue.name : "NULL"));
            }
            report.AppendLine("  observed=" + string.Join(",", root.GetComponent<PhotonView>().ObservedComponents.Select(c => c ? c.GetType().Name : "NULL")));
        }
        foreach (var manager in UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(manager);
            foreach (var field in new[] { "bluePlayerPrefab", "redPlayerPrefab" })
                report.AppendLine(field + "=" + so.FindProperty(field)?.objectReferenceValue);
        }
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>(FindObjectsInactive.Include))
            report.AppendLine("Placed player " + p.name + " active=" + p.gameObject.activeSelf + " view=" + p.GetComponent<PhotonView>().ViewID);
        return report.ToString();
    }
}
