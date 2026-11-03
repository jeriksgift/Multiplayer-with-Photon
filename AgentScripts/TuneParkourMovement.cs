using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.Animations;

public static class TuneParkourMovement
{
    static Collider[] ArenaColliders()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Arena").GetComponentsInChildren<Collider>();
    static Collider Wood()=>ArenaColliders().Where(c=>c.name.Contains("Wooden")&&c.name.Contains("square")&&c.bounds.max.y>1).OrderBy(c=>new Vector2(c.bounds.center.x+8.474f,c.bounds.center.z+.945f).sqrMagnitude).First();
    static Collider Container(Collider wood)=>ArenaColliders().Where(c=>c.name.Contains("Cargo_container")&&c.bounds.max.y<3.5f).OrderBy(c=>Vector3.Distance(c.ClosestPoint(wood.bounds.center),wood.bounds.center)).First();
    public static string Apply()
    {
        const string path="Assets/Resources/PlayerWithGFX.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var p=root.GetComponent<PlayerWithGFXPresentation>();p.walkSpeed=4;p.runSpeed=8;
            var jump=root.GetComponent<PlayerWithGFXJump>();jump.jumpHeight=1.8f;
            const string materialPath="Assets/PlayerWithGFX/PlayerMovementPhysics.physicMaterial";
            var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(materialPath);
            if(!material){material=new PhysicsMaterial("PlayerMovementPhysics");AssetDatabase.CreateAsset(material,materialPath);}
            material.staticFriction=0;material.dynamicFriction=0;material.bounciness=0;material.frictionCombine=PhysicsMaterialCombine.Minimum;material.bounceCombine=PhysicsMaterialCombine.Minimum;EditorUtility.SetDirty(material);
            root.GetComponent<CapsuleCollider>().sharedMaterial=material;
            var movement=new SerializedObject(root.GetComponent<PlayerMovement>());movement.FindProperty("walkSpeed").floatValue=4;movement.FindProperty("runSpeed").floatValue=8;movement.ApplyModifiedPropertiesWithoutUndo();
            var controller=(AnimatorController)p.animator.runtimeAnimatorController;var tree=(BlendTree)controller.layers[0].stateMachine.defaultState.motion;var children=tree.children;children[1].threshold=4;children[2].threshold=8;tree.children=children;EditorUtility.SetDirty(tree);
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var wood=Wood();var container=Container(wood);
        var red=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PlayerWithGFX_Red.prefab");
        if(red.GetComponent<PlayerWithGFXPresentation>().walkSpeed!=4||red.GetComponent<PlayerWithGFXJump>().jumpHeight!=1.8f)throw new Exception("Red variant did not inherit the settings");
        return $"Both variants: walk 5->4m/s, sprint 10->8m/s, jump height 1->1.8m; gravity unchanged. Wood top={wood.bounds.max.y:F3}m at {wood.bounds.center}; container top={container.bounds.max.y:F3}m at {container.bounds.center}.";
    }

    static async Task<string> JumpTo(PlayerWithGFXPresentation p,Vector3 from,Vector3 target,float targetHeight,string label)
    {
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());p.GetComponent<PlayerWithGFXJump>().SetMoveVelocity(Vector3.zero);
        p.body.position=from;p.body.rotation=Quaternion.LookRotation(new Vector3(target.x-from.x,0,target.z-from.z));p.body.linearVelocity=Vector3.zero;await Task.Delay(160);
        if(!p.GetComponent<PlayerWithGFXJump>().Grounded)throw new Exception(label+" takeoff is not grounded at "+p.body.position);
        var direction=new Vector3(target.x-from.x,0,target.z-from.z).normalized;float distance=Vector3.Dot(target-from,direction);
        float peak=p.body.position.y;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Space,Key.W));
        bool released=false,air=false;float started=Time.time;
        for(int i=0;i<160;i++)
        {
            await Task.Delay(10);peak=Mathf.Max(peak,p.body.position.y);if(!p.GetComponent<PlayerWithGFXJump>().Grounded)air=true;
            if(i==8&&!released)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));
            if(!released&&Vector3.Dot(p.body.position-from,direction)>=distance){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());released=true;}
            if(air&&Time.time-started>.2f&&p.GetComponent<PlayerWithGFXJump>().Grounded&&p.body.position.y>=targetHeight-.08f)
            {
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());await Task.Delay(160);
                return $"{label}: landed Y={p.body.position.y:F3}m (target {targetHeight:F3}), peak={peak:F3}m, horizontal error={new Vector2(p.body.position.x-target.x,p.body.position.z-target.z).magnitude:F3}m";
            }
        }
        throw new Exception($"{label}: did not reach target. Position={p.body.position}, target={target}, peak={peak:F3}");
    }
    public static async Task<string> VerifyRoute()
    {
        var p=UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().First();var wood=Wood();var container=Container(wood);
        var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.EnableDevice(Keyboard.current);Cursor.lockState=CursorLockMode.Locked;
        try
        {
            var top=new Vector3(wood.bounds.center.x,wood.bounds.max.y,wood.bounds.center.z);
            var direction=new Vector3(container.bounds.center.x-top.x,0,container.bounds.center.z-top.z).normalized;
            var start=top-direction*2;start.y=.01f;
            string first=await JumpTo(p,start,top,top.y,"Ground to wood");
            var edge=container.ClosestPoint(new Vector3(top.x,container.bounds.max.y+.1f,top.z));
            var landing=edge+direction*.6f;landing.y=container.bounds.max.y;
            // Keep the actual first landing as the next takeoff, proving the full route.
            string second=await JumpTo(p,p.body.position,landing,landing.y,"Wood to container");
            var result=first+"\n"+second;File.WriteAllText("AgentScripts/PlayerWithGFX-parkour-validation.txt",result);return result;
        }
        finally{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;}
    }
}
