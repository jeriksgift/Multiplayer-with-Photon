using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.Animations;
using Photon.Pun;

public static class TuneNaturalJump
{
    const string Dir="Assets/PlayerWithGFX/";
    static AnimationClip Pose(string source,string name,float fraction)
    {
        var src=AssetDatabase.LoadAllAssetsAtPath("Assets/Photon/PhotonUnityNetworking/Demos/Shared Assets/Animations/"+source+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        var clip=new AnimationClip{name=name,frameRate=30};
        // Sample just the pose, leaving all physical translation to the Rigidbody.
        foreach(var binding in AnimationUtility.GetCurveBindings(src))
        {
            if(binding.type!=typeof(Animator)||binding.propertyName.StartsWith("Root"))continue;
            var curve=AnimationUtility.GetEditorCurve(src,binding);
            AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,1,curve.Evaluate(src.length*fraction)));
        }
        clip.SetCurve("",typeof(Animator),"RootT.x",AnimationCurve.Constant(0,1,0));clip.SetCurve("",typeof(Animator),"RootT.y",AnimationCurve.Constant(0,1,.977f));clip.SetCurve("",typeof(Animator),"RootT.z",AnimationCurve.Constant(0,1,0));
        foreach(var axis in new[]{"x","y","z","w"})clip.SetCurve("",typeof(Animator),"RootQ."+axis,AnimationCurve.Constant(0,1,axis=="w"?1:0));
        var path=Dir+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(old){EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(clip,path);
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }
    public static string Apply()
    {
        var rise=Pose("HumanoidIdleJumpUp","RifleJumpRise",.2f);var fall=Pose("HumanoidJumpAndFall","RifleJumpFall",.7f);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir+"RifleLocomotion.controller");
        if(!controller.parameters.Any(p=>p.name=="VerticalSpeed"))controller.AddParameter("VerticalSpeed",AnimatorControllerParameterType.Float);
        var air=controller.layers[0].stateMachine.states.First(s=>s.state.name=="Jump and Fall").state;
        var blend=air.motion as BlendTree;
        if(!blend){blend=new BlendTree{name="Jump Rise Apex Fall",blendType=BlendTreeType.Simple1D,blendParameter="VerticalSpeed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,controller);}
        blend.children=new[]{new ChildMotion{motion=fall,threshold=-5,timeScale=1},new ChildMotion{motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir+"HumanoidMidAir.anim"),threshold=0,timeScale=1},new ChildMotion{motion=rise,threshold=5,timeScale=1}};
        air.motion=blend;EditorUtility.SetDirty(blend);
        foreach(var state in controller.layers[0].stateMachine.states)foreach(var transition in state.state.transitions)transition.duration=(transition.destinationState==air) ? .06f : .09f;
        EditorUtility.SetDirty(controller);
        const string path="Assets/Resources/PlayerWithGFX.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var jump=root.GetComponent<PlayerWithGFXJump>()??root.AddComponent<PlayerWithGFXJump>();jump.jumpHeight=1;jump.risingGravity=2;jump.fallingGravity=3;jump.groundMask=Physics.DefaultRaycastLayers;
            var legacy=new SerializedObject(root.GetComponent<PlayerMovement>());legacy.FindProperty("jumpForce").floatValue=0;legacy.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        var red=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PlayerWithGFX_Red.prefab");if(!red.GetComponent<PlayerWithGFXJump>())throw new Exception("Red variant did not inherit the jump controller");
        return "Both variants: 1m jump, rise gravity 2x, fall gravity 3x; physics-driven rise/apex/fall poses, no duplicate legacy impulse.";
    }

    public static async Task<string> Verify()
    {
        var p=UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().OrderByDescending(p=>p.GetComponent<PhotonView>().ViewID).First();var rb=p.body;
        var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.EnableDevice(Keyboard.current);
        try
        {
            Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            rb.position=new Vector3(-8.474f,0,-.945f);rb.linearVelocity=Vector3.zero;await Task.Delay(150);
            var jump=p.GetComponent<PlayerWithGFXJump>();if(!jump.Grounded)throw new Exception("Character is not grounded before jumping");
            float startY=rb.position.y,peak=startY;float takeoff=Time.time;bool leftGround=false,landed=false;float flight=0;int startFrame=Time.frameCount;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Space));
            for(int i=0;i<110;i++)
            {
                await Task.Delay(10);peak=Mathf.Max(peak,rb.position.y);
                if(rb.position.y>startY+.1f)leftGround=true;
                if(i==12)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                if(leftGround&&jump.Grounded&&rb.position.y<startY+.08f){landed=true;flight=Time.time-takeoff;break;}
            }
            float height=peak-startY;
            var result=$"Peak={height:F3}m; airtime={flight:F3}s; landed={landed}; final Y={rb.position.y:F3}; frames={startFrame}->{Time.frameCount}; right hand grip error={Vector3.Distance(p.animator.GetBoneTransform(HumanBodyBones.RightHand).position,p.rightGrip.position):F4}m.";
            if(!landed||height<jump.jumpHeight-.25f||height>jump.jumpHeight+.12f||flight>1.1f||Time.frameCount==startFrame)throw new Exception(result);
            File.WriteAllText("AgentScripts/PlayerWithGFX-jump-validation.txt",result);return result;
        }
        finally{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;}
    }
    public static async Task<string> VerifyNetworkOwner()
    {
        if(PhotonNetwork.InRoom||PhotonNetwork.IsConnected)throw new Exception("Won't interrupt an existing Photon session");
        GameObject test=null;
        var existing=UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().Select(p=>new {body=p.body,position=p.body.position,kinematic=p.body.isKinematic,collider=p.GetComponent<CapsuleCollider>(),enabled=p.GetComponent<CapsuleCollider>().enabled}).ToArray();
        try
        {
            foreach(var original in existing){original.collider.enabled=false;original.body.isKinematic=true;}
            PhotonNetwork.OfflineMode=true;PhotonNetwork.CreateRoom("LocalJumpVerification");await Task.Delay(100);
            test=PhotonNetwork.Instantiate("PlayerWithGFX",new Vector3(-8.474f,0,-.945f),Quaternion.identity);
            var input=test.GetComponent<PlayerInput>();input.enabled=true;input.ActivateInput();
            // The placed scene player already owns the editor's input devices.
            // This isolated local test reads the same devices without re-pairing them.
            input.actions.devices=null;input.actions.Enable();
            await Task.Delay(150);
            if(!test.GetComponent<PhotonView>().IsMine||!test.GetComponent<PlayerMovement>().enabled)throw new Exception("Network-owner movement path is not active");
            var result=await Verify();File.AppendAllText("AgentScripts/PlayerWithGFX-jump-validation.txt","\nNetwork owner (Photon offline room): "+result);return result;
        }
        finally{if(test)PhotonNetwork.Destroy(test);if(PhotonNetwork.InRoom)PhotonNetwork.LeaveRoom();PhotonNetwork.OfflineMode=false;foreach(var original in existing){original.body.position=original.position;original.body.isKinematic=original.kinematic;original.collider.enabled=original.enabled;}}
    }
}
