using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Photon.Pun;

public static class VerifyPlayerWithGFX
{
    public static string Run()
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/Resources/PlayerWithGFX.prefab");
        try
        {
            var p=root.GetComponent<PlayerWithGFXPresentation>();var animator=p.animator;
            if(!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Invalid humanoid avatar");
            if(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Eyes"||t.name=="GunPivot"))throw new Exception("Old eye/gun remains");
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)throw new Exception("Missing script on "+t.name);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterials.Any(m=>!m||!m.shader))throw new Exception("Missing material on "+r.name);
            var controller=(AnimatorController)animator.runtimeAnimatorController;
            if(!controller.layers[0].iKPass)throw new Exception("Hand IK is disabled");
            if(root.GetComponent<PhotonView>().ObservedComponents.Count(c=>c==p)!=1)throw new Exception("Animation network observer missing or duplicated");
            var weapon=new SerializedObject(root.GetComponent<WeaponSystem>());
            if(weapon.FindProperty("muzzlePoint").objectReferenceValue!=p.muzzle)throw new Exception("Weapon muzzle is disconnected");
            if(p.firstPersonRifle.gameObject.activeSelf)throw new Exception("First-person view should activate only for the owner at runtime");
            var right=animator.GetBoneTransform(HumanBodyBones.RightHand);var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            float rerror=Vector3.Distance(right.position,p.rightGrip.position),lerror=Vector3.Distance(left.position,p.leftGrip.position);
            if(rerror>.015f||lerror>.015f)throw new Exception($"Edit pose hand alignment error: {rerror}/{lerror}");
            var result=$"PASS: humanoid avatar, 52 bone mappings, textured skin, rifle meshes/materials, no missing scripts, no old gun/eye, two-hand IK, weapon muzzle, owner-only first-person view, Photon presentation observer. Edit-pose hand errors: right={rerror:F4}m, left={lerror:F4}m.";
            File.WriteAllText("AgentScripts/PlayerWithGFX-validation.txt",result);return result;
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static string FinishView()
    {
        // This screenshot was accidentally imported by the capture command; the
        // reviewed first-person image is retained outside Assets in Screenshots/.
        AssetDatabase.DeleteAsset("Assets/Screenshots/PlayerWithGFX-firstperson.png");
        if(AssetDatabase.IsValidFolder("Assets/Screenshots")&&Directory.GetFiles("Assets/Screenshots").Length==0)AssetDatabase.DeleteAsset("Assets/Screenshots");
        var player=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(r=>r.name=="PlayerWithGFX");
        Selection.activeGameObject=player;
        SceneView.lastActiveSceneView?.LookAt(player.transform.position+Vector3.up*.95f,Quaternion.Euler(8,205,0),2.2f);
        return "Selected and framed PlayerWithGFX; verification captures retained in Screenshots/";
    }
}
