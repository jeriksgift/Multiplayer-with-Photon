using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class IntegrateLobbyToolkit
{
    const string Folder="Assets/UI Toolkit/MyUIToolkits/";
    public static string Setup()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Additive);
        try
        {
            var roots=scene.GetRootGameObjects();var manager=roots.SelectMany(g=>g.GetComponentsInChildren<LobbyManager>(true)).Single();
            var oldCanvas=roots.Single(g=>g.name=="Canvas");var oldMenu=oldCanvas.transform.Find("Menu");
            if(manager.transform.IsChildOf(oldMenu))throw new Exception("LobbyManager must stay active when its old menu is hidden.");
            var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder+"lobby.uxml");if(!tree)throw new Exception("lobby.uxml failed to import.");
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder+"LobbyRuntimePanelSettings.asset");
            if(!panel)
            {
                panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/PanelSettings.asset"));panel.name="Lobby Runtime Panel Settings";
                AssetDatabase.CreateAsset(panel,Folder+"LobbyRuntimePanelSettings.asset");
            }
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1920,1080);panel.screenMatchMode=PanelScreenMatchMode.Expand;panel.sortingOrder=-1;EditorUtility.SetDirty(panel);
            var root=roots.FirstOrDefault(g=>g.name=="LobbyDocument");if(!root){root=new GameObject("LobbyDocument");SceneManager.MoveGameObjectToScene(root,scene);}
            var doc=root.GetComponent<UIDocument>()??root.AddComponent<UIDocument>();doc.panelSettings=panel;doc.visualTreeAsset=tree;
            var controller=root.GetComponent<LobbyScreen>()??root.AddComponent<LobbyScreen>();var so=new SerializedObject(controller);so.FindProperty("lobbyManager").objectReferenceValue=manager;so.ApplyModifiedPropertiesWithoutUndo();
            // The user's translucent panel composites over the scene's main camera.
            var backdrop=root.transform.Find("ExistingArenaBackground");
            if(backdrop)UnityEngine.Object.DestroyImmediate(backdrop.gameObject);
            so=new SerializedObject(manager);so.FindProperty("lobbyPanel").objectReferenceValue=root;so.ApplyModifiedPropertiesWithoutUndo();oldMenu.gameObject.SetActive(false);oldCanvas.SetActive(true);root.SetActive(true);
            EditorUtility.SetDirty(doc);EditorUtility.SetDirty(controller);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Connected lobby.uxml over the live main camera; fixed backdrop removed and authored panel transparency preserved. Other screens retained.";
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
