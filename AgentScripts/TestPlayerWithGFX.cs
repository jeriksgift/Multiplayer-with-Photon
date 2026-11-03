using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using Photon.Pun;

public static class TestPlayerWithGFX
{
    static PlayerWithGFXPresentation Player()=>UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().First();
    public static string Inspect()
    {
        var p=Player();var a=p.animator;
        return $"frame={Time.frameCount} pos={p.transform.position:F3} speed={p.AnimationSpeed:F2} ground={p.Grounded} animator={a.isHuman} state={a.GetCurrentAnimatorStateInfo(0).fullPathHash} RH={a.GetBoneTransform(HumanBodyBones.RightHand).position:F3} target={p.rightGrip.position:F3} LH={a.GetBoneTransform(HumanBodyBones.LeftHand).position:F3} target={p.leftGrip.position:F3} cam={p.playerCamera.enabled} input={p.GetComponent<PlayerInput>().currentActionMap?.name}";
    }
    public static string Details()
    {
        var p=Player();var a=p.animator;var rb=p.body;var pi=p.GetComponent<PlayerInput>();
        return $"root={p.transform.position} gfx={p.transform.Find("GFX").position} soldier={a.transform.position} local={a.transform.localPosition} hips={a.GetBoneTransform(HumanBodyBones.Hips).position} foot={p.footPoint.position} body={rb.isKinematic}/{rb.useGravity}/{rb.constraints} input={pi.enabled}/{pi.defaultActionMap}/{pi.currentActionMap?.name} presentation={p.enabled} IK={a.GetComponent<SoldierRifleIK>().enabled} root motion={a.applyRootMotion}";
    }
    public static string Capture()
    {
        var p=Player();var g=new GameObject("VerificationCamera");var camera=g.AddComponent<Camera>();camera.fieldOfView=32;camera.nearClipPlane=.05f;camera.farClipPlane=200;
        camera.transform.position=p.transform.position+p.transform.TransformDirection(new Vector3(2.5f,1.8f,3.7f));camera.transform.LookAt(p.transform.position+Vector3.up*.95f);
        var rt=RenderTexture.GetTemporary(1000,1000,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1000,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();Directory.CreateDirectory("Screenshots");File.WriteAllBytes("Screenshots/PlayerWithGFX-play.png",tex.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(tex);UnityEngine.Object.Destroy(g);return Inspect();
    }
    public static string CaptureFirstPerson()
    {
        var camera=Player().playerCamera;var rt=RenderTexture.GetTemporary(1280,720,24);var previousTarget=camera.targetTexture;
        camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes("Screenshots/PlayerWithGFX-firstperson.png",tex.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=previousTarget;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(tex);return "Captured the PlayerWithGFX camera";
    }
    public static async Task<string> Exercise()
    {
        var p=Player();int before=Time.frameCount;await Task.Delay(200);if(Time.frameCount==before)throw new Exception("Game frames are frozen");
        var oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.EnableDevice(Keyboard.current);InputSystem.EnableDevice(Mouse.current);
        p.body.position=new Vector3(-8.474f,0,-.945f);p.body.linearVelocity=Vector3.zero;await Task.Delay(100);
        var start=p.transform.position;var keyboard=Keyboard.current;
        try
        {
            Cursor.lockState=CursorLockMode.Locked;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Task.Delay(350);
            string walk=Inspect();float distance=Vector3.Distance(start,p.transform.position);float walkSpeed=p.AnimationSpeed;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));await Task.Delay(350);string run=Inspect();float runSpeed=p.AnimationSpeed;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(400);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));await Task.Delay(200);string jump=Inspect();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(1200);
            int shots=p.Shots;InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=Mouse.current.position.ReadValue()}.WithButton(MouseButton.Left));await Task.Delay(20);bool flash=p.muzzleFlash.activeSelf;InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=Mouse.current.position.ReadValue()});
            bool fired=p.Shots>shots;await Task.Delay(400);
            var report=$"frames={before}->{Time.frameCount}, distance={distance:F2}, flash={flash}, fired={fired}\nWALK {walk}\nRUN {run}\nJUMP {jump}\nFINAL {Inspect()}";
            File.WriteAllText("AgentScripts/PlayerWithGFX-playtest.txt",report);
            if(distance<1||walkSpeed<p.walkSpeed-.5f||runSpeed<p.runSpeed-.8f||!fired||!flash)throw new Exception("Input/animation verification failed: "+report);
            return report;
        }
        finally {InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=Mouse.current.position.ReadValue()});InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;InputSystem.settings.backgroundBehavior=oldBackground;}
    }
}
