using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class FixPlayerYaw
{
    public static string Apply()
    {
        const string path="Assets/Resources/PlayerWithGFX.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        string before;
        try
        {
            var rb=root.GetComponent<Rigidbody>();before=rb.constraints.ToString();
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach(var instance in scene.GetRootGameObjects().Where(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==path))
        {
            var rb=instance.GetComponent<Rigidbody>();
            if((rb.constraints&RigidbodyConstraints.FreezeRotation)!=RigidbodyConstraints.FreezeRotation)
            {
                Undo.RecordObject(rb,"Prevent player physics spin");rb.constraints|=RigidbodyConstraints.FreezeRotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rb);EditorSceneManager.MarkSceneDirty(scene);
            }
        }
        if(scene.isDirty)EditorSceneManager.SaveScene(scene);
        return $"Before: {before}; after: FreezeRotation (mouse/script yaw remains enabled).";
    }

    public static async Task<string> Verify()
    {
        var p=UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().First();var rb=p.body;
        var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.EnableDevice(Keyboard.current);InputSystem.EnableDevice(Mouse.current);
        try
        {
            Cursor.lockState=CursorLockMode.Locked;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            // Reproduce the original problem with its previous physics constraints.
            rb.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            float before=rb.rotation.eulerAngles.y;
            rb.AddTorque(Vector3.up*3,ForceMode.VelocityChange);await Task.Delay(350);
            float reproduced=Mathf.Abs(Mathf.DeltaAngle(before,rb.rotation.eulerAngles.y));
            rb.angularVelocity=Vector3.zero;rb.constraints=RigidbodyConstraints.FreezeRotation;
            await Task.Delay(100); // Let the prior free-spin physics step settle before measuring.
            before=rb.rotation.eulerAngles.y;
            rb.AddTorque(Vector3.up*3,ForceMode.Force);await Task.Delay(350);
            float blocked=Mathf.Abs(Mathf.DeltaAngle(before,rb.rotation.eulerAngles.y));
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));await Task.Delay(350);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());await Task.Delay(150);
            before=rb.rotation.eulerAngles.y;await Task.Delay(500);
            float stopped=Mathf.Abs(Mathf.DeltaAngle(before,rb.rotation.eulerAngles.y));
            before=rb.rotation.eulerAngles.y;
            InputSystem.QueueDeltaStateEvent(Mouse.current.delta,new Vector2(60,0));await Task.Delay(100);
            float look=Mathf.Abs(Mathf.DeltaAngle(before,rb.rotation.eulerAngles.y));
            var result=$"Original torque-induced yaw={reproduced:F3}deg; fixed torque-induced yaw={blocked:F3}deg; yaw drift after moving/stopping={stopped:F3}deg; mouse-look yaw={look:F3}deg; angular velocity={rb.angularVelocity}.";
            if(reproduced<1||blocked>.1f||stopped>.1f||look<.01f)throw new Exception(result);
            System.IO.File.WriteAllText("AgentScripts/PlayerWithGFX-yaw-validation.txt",result);
            return result;
        }
        finally
        {
            rb.angularVelocity=Vector3.zero;rb.constraints=RigidbodyConstraints.FreezeRotation;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=Mouse.current.position.ReadValue()});
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;
        }
    }
}
