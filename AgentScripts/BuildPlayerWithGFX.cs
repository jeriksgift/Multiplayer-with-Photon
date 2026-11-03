using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Photon.Pun;

public static class BuildPlayerWithGFX
{
    const string Prefab="Assets/Resources/PlayerWithGFX.prefab";
    const string Source="Assets/Models/Generated/Soldier/WarZoneSoldier.fbx";
    const string Dir="Assets/PlayerWithGFX";
    static Transform Find(Transform r,string n)=>r.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
    public static string BuildRig()
    {
        Directory.CreateDirectory(Dir);Directory.CreateDirectory("AgentScripts/Backups");
        if(!File.Exists("AgentScripts/Backups/PlayerWithGFX.prefab"))File.Copy(Prefab,"AgentScripts/Backups/PlayerWithGFX.prefab");
        AssetDatabase.Refresh();
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var gfx=root.transform.Find("GFX");
            foreach(Transform c in gfx.Cast<Transform>().ToArray())
                if(c.name.StartsWith("Visual")||c.name=="SoldierVisual")UnityEngine.Object.DestroyImmediate(c.gameObject);
            var soldier=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
            soldier.name="SoldierVisual";
            soldier.transform.position=Vector3.zero;
            soldier.transform.rotation=Quaternion.identity;
            soldier.transform.localScale=Vector3.one;
            var facing=Quaternion.Euler(0,90,0);
            foreach(Transform child in soldier.transform){child.position=facing*child.position;child.rotation=facing*child.rotation;}
            // The generated root is at the feet. Move its pivot to the pelvis while
            // preserving every child bone's world pose and the skin's rest geometry.
            var hips=Find(soldier.transform,"tripo::Root");
            var children=hips.Cast<Transform>().ToArray();
            var positions=children.Select(t=>t.position).ToArray();
            hips.position=(Find(soldier.transform,"tripo::1_Left_Limb_0").position+Find(soldier.transform,"tripo::0_Right_Limb_0").position)*.5f;
            for(int i=0;i<children.Length;i++)children[i].position=positions[i];
            var skin=soldier.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh=UnityEngine.Object.Instantiate(skin.sharedMesh);mesh.name="SoldierRepairedSkin";
            var binds=mesh.bindposes;for(int i=0;i<skin.bones.Length;i++)if(skin.bones[i]==hips)binds[i]=hips.worldToLocalMatrix*skin.transform.localToWorldMatrix;
            mesh.bindposes=binds;
            Save(mesh,Dir+"/SoldierRepairedSkin.asset");skin.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"/SoldierRepairedSkin.asset");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Generated/Soldier/Textures/WarZoneSoldier_BaseColor.jpg");
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="SoldierArmor",color=Color.white,mainTexture=texture};
            material.SetFloat("_Smoothness",.28f);Save(material,Dir+"/SoldierArmor.mat");skin.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/SoldierArmor.mat");
            var map=new Dictionary<string,string>{
                {"Hips","tripo::Root"},{"Spine","tripo::0_Left_Limb_0"},{"Chest","tripo::0_Left_Limb_1"},{"UpperChest","tripo::0_Left_Limb_2"},
                {"Neck","tripo::Head_0"},{"Head","tripo::Head_1"},
                {"LeftShoulder","tripo::Spine_0"},{"LeftUpperArm","bone_24"},{"LeftLowerArm","bone_25"},{"LeftHand","bone_26"},
                {"RightShoulder","tripo::0_Left_Limb_3"},{"RightUpperArm","tripo::0_Left_Limb_4"},{"RightLowerArm","tripo::0_Left_Limb_5"},{"RightHand","tripo::0_Left_Limb_6"},
                {"LeftUpperLeg","tripo::1_Left_Limb_0"},{"LeftLowerLeg","tripo::1_Left_Limb_1"},{"LeftFoot","tripo::1_Left_Limb_2"},{"LeftToes","tripo::1_Left_Limb_3"},
                {"RightUpperLeg","tripo::0_Right_Limb_0"},{"RightLowerLeg","tripo::0_Right_Limb_1"},{"RightFoot","tripo::0_Right_Limb_2"},{"RightToes","tripo::0_Right_Limb_3"}};
            // Tripo's generated side labels are reversed after its coordinate conversion.
            map=map.ToDictionary(kv=>kv.Key.StartsWith("Left")?"Right"+kv.Key.Substring(4):kv.Key.StartsWith("Right")?"Left"+kv.Key.Substring(5):kv.Key,kv=>kv.Value);
            Action<string,string,Vector3> aim=(a,b,dir)=>{var t=Find(soldier.transform,a);var end=Find(soldier.transform,b);t.rotation=Quaternion.FromToRotation(end.position-t.position,dir)*t.rotation;};
            // Reference pose faces +Z with both arms extended along X.
            foreach(var side in new[]{"Left","Right"})
            {
                var dir=Vector3.right*(side=="Left"?-1:1);
                aim(map[side+"UpperArm"],map[side+"LowerArm"],dir);
                aim(map[side+"LowerArm"],map[side+"Hand"],dir);
                aim(map[side+"Hand"],side=="Left"?"bone_11":"bone_30",dir);
            }
            // Include fingers so gripping poses can be animated by the humanoid avatar.
            AddFingers(map,"Right",new[]{"bone_39","bone_40","bone_41","bone_27","bone_28","bone_29","bone_30","bone_31","bone_32","bone_36","bone_37","bone_38","bone_33","bone_34","bone_35"});
            AddFingers(map,"Left",new[]{"tripo::0_Left_Limb_7","tripo::0_Left_Limb_8","tripo::0_Left_Limb_9","bone_8","bone_9","bone_10","bone_11","bone_12","bone_13","bone_17","bone_18","bone_19","bone_14","bone_15","bone_16"});
            var desc=new HumanDescription {
                human=map.Select(kv=>new HumanBone{humanName=kv.Key,boneName=kv.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),
                skeleton=soldier.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),
                upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f};
            var avatar=AvatarBuilder.BuildHumanAvatar(soldier,desc);avatar.name="SoldierHumanoid";
            if(!avatar.isValid||!avatar.isHuman)throw new Exception("Repaired avatar did not validate");
            Save(avatar,Dir+"/SoldierHumanoid.asset");
            var animator=soldier.GetComponent<Animator>()??soldier.AddComponent<Animator>();
            animator.avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Dir+"/SoldierHumanoid.asset");animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            soldier.transform.SetParent(gfx,false);soldier.transform.localPosition=new Vector3(0,-1,0);
            var presentation=root.GetComponent<PlayerWithGFXPresentation>();
            if(presentation){presentation.animator=animator;animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir+"/RifleLocomotion.controller");soldier.AddComponent<SoldierRifleIK>().presentation=presentation;
                presentation.rifleAim.localPosition=new Vector3(.21f,.36f,.25f);
                presentation.rightGrip.rotation=GripRotation(Find(soldier.transform,"bone_26"),Find(soldier.transform,"bone_30"),Find(soldier.transform,"bone_39"),Vector3.down,Vector3.forward);
                presentation.leftGrip.rotation=GripRotation(Find(soldier.transform,"tripo::0_Left_Limb_6"),Find(soldier.transform,"bone_11"),Find(soldier.transform,"tripo::0_Left_Limb_7"),Vector3.right,Vector3.forward);
                presentation.playerCamera.transform.localRotation=Quaternion.identity;
                foreach(var r in soldier.GetComponentsInChildren<Renderer>())r.gameObject.layer=LayerMask.NameToLayer("Ignore Raycast");}
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            return $"Avatar valid={animator.avatar.isValid}, humanoid={animator.avatar.isHuman}, bones={map.Count}, texture={texture!=null}";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void AddFingers(Dictionary<string,string> map,string side,string[] names)
    {int i=0;foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"})foreach(var segment in new[]{"Proximal","Intermediate","Distal"})map.Add(side+finger+segment,names[i++]);}
    static void Save(UnityEngine.Object obj,string path)
    {var old=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);if(old){EditorUtility.CopySerialized(obj,old);UnityEngine.Object.DestroyImmediate(obj);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(obj,path);AssetDatabase.SaveAssets();}
    static AnimationClip Clip(string name,bool loop)
    {
        string path="Assets/Photon/PhotonUnityNetworking/Demos/Shared Assets/Animations/"+name+".fbx";
        var clip=UnityEngine.Object.Instantiate(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview")));
        clip.name=name.Replace("Humanoid", "Rifle");
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=loop;settings.keepOriginalPositionXZ=true;settings.keepOriginalPositionY=true;settings.keepOriginalOrientation=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        // These bundled clips store the actor's absolute capture-space root pose.
        // Remove its translation/yaw so retargeting stays centred on this player.
        foreach(string axis in new[]{"x","z"})clip.SetCurve("",typeof(Animator),"RootT."+axis,AnimationCurve.Constant(0,clip.length,0));
        if(name=="HumanoidMidAir")clip.SetCurve("",typeof(Animator),"RootT.y",AnimationCurve.Constant(0,clip.length,.977f));
        var qBindings=new[]{"x","y","z","w"}.Select(a=>EditorCurveBinding.FloatCurve("",typeof(Animator),"RootQ."+a)).ToArray();
        var qs=qBindings.Select(b=>AnimationUtility.GetEditorCurve(clip,b)).ToArray();
        if(qs.All(q=>q!=null))
        {
            var first=new Quaternion(qs[0].Evaluate(0),qs[1].Evaluate(0),qs[2].Evaluate(0),qs[3].Evaluate(0));
            var yaw=Quaternion.Inverse(Quaternion.Euler(0,first.eulerAngles.y,0));var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            for(int i=0;i<=Mathf.CeilToInt(clip.length*30);i++){float time=Mathf.Min(clip.length,i/30f);var q=yaw*new Quaternion(qs[0].Evaluate(time),qs[1].Evaluate(time),qs[2].Evaluate(time),qs[3].Evaluate(time));curves[0].AddKey(time,q.x);curves[1].AddKey(time,q.y);curves[2].AddKey(time,q.z);curves[3].AddKey(time,q.w);}
            for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(clip,qBindings[i],curves[i]);
        }
        Save(clip,Dir+"/"+name+".anim");return AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir+"/"+name+".anim");
    }
    static Material Mat(string n,Color c,float metallic=.0f)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=n,color=c};m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.35f);Save(m,Dir+"/"+n+".mat");return AssetDatabase.LoadAssetAtPath<Material>(Dir+"/"+n+".mat");}
    static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 p,Vector3 scale,Material m,Vector3 rot=default)
    {var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.transform.localEulerAngles=rot;g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
    // Extruded octagonal profile gives the receiver and handguard chamfered edges.
    static GameObject Chamfer(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
    {
        var mesh=new Mesh{name=name};var v=new List<Vector3>();float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f,b=Mathf.Min(x,y)*.3f;
        var ring=new[]{new Vector2(-x+b,-y),new Vector2(x-b,-y),new Vector2(x,-y+b),new Vector2(x,y-b),new Vector2(x-b,y),new Vector2(-x+b,y),new Vector2(-x,y-b),new Vector2(-x,-y+b)};
        foreach(float depth in new[]{-z,z})foreach(var a in ring)v.Add(new Vector3(a.x,a.y,depth));
        var t=new List<int>();for(int i=0;i<8;i++){int j=(i+1)%8;t.AddRange(new[]{i,j,j+8,i,j+8,i+8});}for(int i=1;i<7;i++){t.AddRange(new[]{0,i+1,i,8,8+i,8+i+1});}
        mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();Save(mesh,Dir+"/"+name+".asset");
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=p;g.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"/"+name+".asset");g.GetComponent<Renderer>().sharedMaterial=mat;return g;
    }
    public static string BuildEquipment()
    {
        var idle=Clip("HumanoidIdle",true);var walk=Clip("HumanoidWalk",true);var run=Clip("HumanoidRun",true);var air=Clip("HumanoidMidAir",true);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir+"/RifleLocomotion.controller");
        if(controller)throw new Exception("Controller already exists; use validation rather than overwriting authored changes.");
        controller=AnimatorController.CreateAnimatorControllerAtPath(Dir+"/RifleLocomotion.controller");
        controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("Grounded",AnimatorControllerParameterType.Bool);controller.AddParameter("Playback",AnimatorControllerParameterType.Float);
        var ps=controller.parameters;foreach(var p in ps){if(p.name=="Grounded")p.defaultBool=true;if(p.name=="Playback")p.defaultFloat=1;}controller.parameters=ps;
        var sm=controller.layers[0].stateMachine;var locomotion=sm.AddState("Rifle Locomotion");sm.defaultState=locomotion;
        var blend=new BlendTree{name="Idle Walk Run",blendParameter="Speed",blendType=BlendTreeType.Simple1D,useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,controller);
        blend.AddChild(idle,0);blend.AddChild(walk,3);blend.AddChild(run,6);locomotion.motion=blend;locomotion.speedParameter="Playback";locomotion.speedParameterActive=true;
        var jump=sm.AddState("Jump and Fall");jump.motion=air;
        var toAir=locomotion.AddTransition(jump);toAir.hasExitTime=false;toAir.duration=.12f;toAir.AddCondition(AnimatorConditionMode.IfNot,0,"Grounded");
        var toGround=jump.AddTransition(locomotion);toGround.hasExitTime=false;toGround.duration=.16f;toGround.AddCondition(AnimatorConditionMode.If,0,"Grounded");
        var layers=controller.layers;layers[0].iKPass=true;controller.layers=layers;
        // Finger-only layer keeps a two-handed rifle grip over the retargeted motion.
        var gripClip=new AnimationClip{name="Rifle Grip",frameRate=30};
        foreach(var side in new[]{"Left","Right"})foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"})for(int i=1;i<=3;i++)
            gripClip.SetCurve("",typeof(Animator),side+"Hand."+finger+"."+i+" Stretched",AnimationCurve.Constant(0,1,finger=="Index"&&side=="Right"?-.35f:-.7f));
        Save(gripClip,Dir+"/RifleGrip.anim");var mask=new AvatarMask{name="FingersOnly"};for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,true);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,true);Save(mask,Dir+"/FingersOnly.mask");
        controller.AddLayer("Rifle Grip");layers=controller.layers;layers[1].defaultWeight=1;layers[1].avatarMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(Dir+"/FingersOnly.mask");controller.layers=layers;
        var gripState=layers[1].stateMachine.AddState("Hold Grip");gripState.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir+"/RifleGrip.anim");layers[1].stateMachine.defaultState=gripState;
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var gfx=root.transform.Find("GFX");
            var eye=gfx.Find("Eyes");if(eye)UnityEngine.Object.DestroyImmediate(eye.gameObject);
            var oldGun=gfx.Find("GunPivot");if(oldGun)UnityEngine.Object.DestroyImmediate(oldGun.gameObject);
            var aim=new GameObject("RifleAim").transform;aim.SetParent(gfx,false);aim.localPosition=new Vector3(-.21f,.36f,.25f);
            var rifle=new GameObject("Sentinel_Rifle").transform;rifle.SetParent(aim,false);
            var dark=Mat("RifleGraphite",new Color(.065f,.085f,.105f),.65f);var blue=Mat("RifleArmorBlue",new Color(.25f,.46f,.58f),.5f);var steel=Mat("RifleSteel",new Color(.23f,.28f,.3f),.85f);var rubber=Mat("RifleRubber",new Color(.025f,.03f,.035f));var glow=Mat("RifleSight",new Color(.1f,.85f,1));
            glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(.1f,1,1.5f));EditorUtility.SetDirty(glow);
            Chamfer(rifle,"RifleReceiver",new Vector3(0,0,.035f),new Vector3(.105f,.14f,.32f),blue);
            Chamfer(rifle,"RifleHandguard",new Vector3(0,.005f,.3f),new Vector3(.085f,.105f,.24f),dark);
            Chamfer(rifle,"RifleStock",new Vector3(0,-.015f,-.25f),new Vector3(.07f,.13f,.25f),dark);
            Part(rifle,"ButtPad",PrimitiveType.Cube,new Vector3(0,-.015f,-.39f),new Vector3(.085f,.16f,.035f),rubber);
            Part(rifle,"CheekRest",PrimitiveType.Cube,new Vector3(0,.065f,-.25f),new Vector3(.085f,.035f,.18f),rubber);
            Part(rifle,"PistolGrip",PrimitiveType.Cube,new Vector3(0,-.13f,-.07f),new Vector3(.065f,.16f,.075f),rubber,new Vector3(-16,0,0));
            Part(rifle,"Magazine",PrimitiveType.Cube,new Vector3(0,-.15f,.12f),new Vector3(.065f,.19f,.095f),steel,new Vector3(12,0,0));
            for(int i=0;i<3;i++)Part(rifle,"MagazineRib"+i,PrimitiveType.Cube,new Vector3(0,-.095f-i*.045f,.113f),new Vector3(.072f,.012f,.105f),dark,new Vector3(12,0,0));
            Part(rifle,"Barrel",PrimitiveType.Cylinder,new Vector3(0,.025f,.48f),new Vector3(.034f,.1f,.034f),steel,new Vector3(90,0,0));
            Part(rifle,"MuzzleBrake",PrimitiveType.Cylinder,new Vector3(0,.025f,.59f),new Vector3(.055f,.04f,.055f),dark,new Vector3(90,0,0));
            Part(rifle,"MuzzleBore",PrimitiveType.Cylinder,new Vector3(0,.025f,.631f),new Vector3(.027f,.001f,.027f),rubber,new Vector3(90,0,0));
            for(int i=0;i<8;i++)Part(rifle,"TopRail"+i,PrimitiveType.Cube,new Vector3(0,.08f,-.05f+i*.05f),new Vector3(.075f,.017f,.024f),steel);
            for(int i=0;i<4;i++)foreach(float side in new[]{-1f,1f})Part(rifle,"Vent",PrimitiveType.Cube,new Vector3(side*.044f,.009f,.23f+i*.045f),new Vector3(.004f,.032f,.019f),steel);
            Part(rifle,"SightBase",PrimitiveType.Cube,new Vector3(0,.11f,-.015f),new Vector3(.06f,.04f,.09f),dark);
            foreach(float side in new[]{-1f,1f})Part(rifle,"SightFrame",PrimitiveType.Cube,new Vector3(side*.034f,.155f,-.015f),new Vector3(.014f,.07f,.035f),dark);
            Part(rifle,"SightTop",PrimitiveType.Cube,new Vector3(0,.19f,-.015f),new Vector3(.08f,.014f,.035f),dark);
            Part(rifle,"SightDot",PrimitiveType.Sphere,new Vector3(0,.155f,.005f),new Vector3(.009f,.009f,.009f),glow);
            Part(rifle,"ChargingHandle",PrimitiveType.Cube,new Vector3(-.07f,.025f,-.075f),new Vector3(.055f,.025f,.035f),steel);
            Part(rifle,"AccentStripe",PrimitiveType.Cube,new Vector3(-.054f,.025f,.05f),new Vector3(.003f,.017f,.12f),glow);
            var muzzle=new GameObject("MuzzlePoint").transform;muzzle.SetParent(rifle,false);muzzle.localPosition=new Vector3(0,.025f,.65f);
            var flash=Part(muzzle,"MuzzleFlash",PrimitiveType.Sphere,Vector3.zero,new Vector3(.065f,.065f,.16f),Mat("MuzzleFlash",new Color(1,.65f,.12f)));
            var fm=flash.GetComponent<Renderer>().sharedMaterial;fm.EnableKeyword("_EMISSION");fm.SetColor("_EmissionColor",new Color(5,2,.25f));EditorUtility.SetDirty(fm);flash.SetActive(false);
            var soldier=gfx.Find("SoldierVisual");var animator=soldier.GetComponent<Animator>();animator.runtimeAnimatorController=controller;
            var presentation=root.AddComponent<PlayerWithGFXPresentation>();presentation.animator=animator;presentation.rifleAim=aim;presentation.muzzle=muzzle;presentation.muzzleFlash=flash;
            presentation.body=root.GetComponent<Rigidbody>();presentation.footPoint=gfx.Find("FootPoint");presentation.playerCamera=gfx.GetComponentInChildren<Camera>();
            var movement=new SerializedObject(root.GetComponent<PlayerMovement>());presentation.groundMask=movement.FindProperty("groundLayer").intValue;presentation.walkSpeed=movement.FindProperty("walkSpeed").floatValue;presentation.runSpeed=movement.FindProperty("runSpeed").floatValue;presentation.jumpImpulse=movement.FindProperty("jumpForce").floatValue;presentation.mouseSensitivity=movement.FindProperty("mouseSensitivity").floatValue;
            var right=Grip(rifle,"RightHandGrip",new Vector3(0,-.095f,-.075f));var left=Grip(rifle,"LeftHandGrip",new Vector3(0,-.045f,.3f));
            right.rotation=GripRotation(animator.GetBoneTransform(HumanBodyBones.RightHand),Find(soldier,"bone_11"),Find(soldier,"tripo::0_Left_Limb_7"),-root.transform.up,root.transform.forward);
            left.rotation=GripRotation(animator.GetBoneTransform(HumanBodyBones.LeftHand),Find(soldier,"bone_30"),Find(soldier,"bone_39"),-root.transform.right,root.transform.forward);
            presentation.rightGrip=right;presentation.leftGrip=left;soldier.gameObject.AddComponent<SoldierRifleIK>().presentation=presentation;
            var weapon=new SerializedObject(root.GetComponent<WeaponSystem>());weapon.FindProperty("muzzlePoint").objectReferenceValue=muzzle;weapon.ApplyModifiedPropertiesWithoutUndo();
            var pv=root.GetComponent<PhotonView>();if(!pv.ObservedComponents.Contains(presentation))pv.ObservedComponents.Add(presentation);
            // Character is 1.85m, feet at the root and facing the movement/camera +Z.
            var camera=presentation.playerCamera;camera.transform.localPosition=new Vector3(0,.66f,.06f);camera.nearClipPlane=.03f;
            // Keep the animated body visible to other players, but outside the owner's view.
            int bodyLayer=LayerMask.NameToLayer("Ignore Raycast");foreach(var r in soldier.GetComponentsInChildren<Renderer>())r.gameObject.layer=bodyLayer;
            camera.cullingMask &= ~(1<<bodyLayer);
            presentation.footPoint.localPosition=new Vector3(0,-.96f,0);
            var collider=root.GetComponent<CapsuleCollider>();collider.height=1.85f;collider.radius=.3f;collider.center=new Vector3(0,.925f,0);
            presentation.body.constraints=RigidbodyConstraints.FreezeRotation;
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            return $"Equipment built; clips human={idle.humanMotion}/{walk.humanMotion}/{run.humanMotion}/{air.humanMotion}; speed={presentation.walkSpeed}/{presentation.runSpeed}; ground={presentation.groundMask.value}";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static Transform Grip(Transform r,string n,Vector3 p){var g=new GameObject(n).transform;g.SetParent(r,false);g.localPosition=p;return g;}
    static Quaternion GripRotation(Transform hand,Transform middle,Transform thumb,Vector3 direction,Vector3 up)
    {return Quaternion.LookRotation(direction,up)*Quaternion.Inverse(Quaternion.LookRotation(middle.position-hand.position,thumb.position-hand.position))*hand.rotation;}
    public static string Render(bool animate=true)
    {
        var p=new PreviewRenderUtility();var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=b is SoldierRifleIK;
        foreach(var c in root.GetComponentsInChildren<Canvas>(true))c.gameObject.SetActive(false);
        foreach(var c in root.GetComponentsInChildren<Camera>())c.gameObject.SetActive(false);
        p.AddSingleGO(root);var animator=root.GetComponentInChildren<Animator>();
        if(animate&&animator.runtimeAnimatorController){animator.Rebind();animator.Update(.1f);}
        var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();skin.updateWhenOffscreen=true;
        var baked=new Mesh();skin.BakeMesh(baked);
        File.WriteAllText("AgentScripts/preview-diagnostics.txt",$"skin bounds {skin.bounds} baked {baked.bounds}\n"+string.Join("\n",root.GetComponentsInChildren<Transform>().Where(t=>t==animator.transform||t.name=="tripo::Root"||t.name=="tripo::Head_1"||t.name=="bone_26").Select(t=>$"{t.name} position={t.position} rotation={t.rotation}")));UnityEngine.Object.DestroyImmediate(baked);
        p.camera.transform.position=new Vector3(3,1.9f,4);p.camera.transform.LookAt(new Vector3(0,.95f,0));p.camera.fieldOfView=29;
        p.camera.nearClipPlane=.1f;p.camera.farClipPlane=30;p.camera.backgroundColor=new Color(.12f,.15f,.19f);p.camera.clearFlags=CameraClearFlags.SolidColor;
        p.lights[0].intensity=1.5f;p.lights[0].transform.rotation=Quaternion.Euler(40,30,0);p.lights[1].intensity=1.1f;p.lights[1].transform.rotation=Quaternion.Euler(340,200,0);p.ambientColor=new Color(.5f,.5f,.5f);
        p.BeginPreview(new Rect(0,0,1000,1000),GUIStyle.none);p.Render(true);var texture=p.EndPreview();
        var rt=RenderTexture.GetTemporary(1000,1000,0);Graphics.Blit(texture,rt);var previous=RenderTexture.active;RenderTexture.active=rt;var img=new Texture2D(1000,1000,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1000,1000),0,0);img.Apply();
        Directory.CreateDirectory("Screenshots");File.WriteAllBytes("Screenshots/PlayerWithGFX-preview.png",img.EncodeToPNG());RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(img);p.Cleanup();return "Screenshots/PlayerWithGFX-preview.png";
    }
    public static string InspectPrefab()
    {
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try{return string.Join("\n",root.GetComponentsInChildren<Transform>(true).Where(t=>t.GetComponent<Renderer>()||t.GetComponent<Camera>()||t==root.transform||t.name=="GFX").Select(t=>$"{AnimationUtility.CalculateTransformPath(t,root.transform)} pos={t.position:F3} rot={t.eulerAngles:F1} scale={t.lossyScale:F2}"));}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static string InspectClips()
    {var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir+"/HumanoidIdle.anim");return string.Join("\n",AnimationUtility.GetCurveBindings(c).Select(b=>{var v=AnimationUtility.GetEditorCurve(c,b);return $"{b.path} {b.type.Name} {b.propertyName} first={v.keys[0].value} last={v.keys[v.length-1].value}";}));}
    public static string FixClips(){foreach(var n in new[]{"HumanoidIdle","HumanoidWalk","HumanoidRun","HumanoidMidAir"})Clip(n,true);return "Centred all locomotion clips";}
    public static string Configure()
    {
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var p=root.GetComponent<PlayerWithGFXPresentation>();
            p.groundMask=Physics.DefaultRaycastLayers;
            p.rifleAim.localPosition=new Vector3(.14f,.36f,.18f);
            p.leftGrip.localPosition=new Vector3(-.035f,-.045f,.17f);
            var input=root.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            input.defaultActionMap=input.actions.FindAction("Move").actionMap.name;
            var movement=new SerializedObject(root.GetComponent<PlayerMovement>());movement.FindProperty("groundLayer").intValue=Physics.DefaultRaycastLayers;movement.ApplyModifiedPropertiesWithoutUndo();
            p.animator.GetComponentInChildren<SkinnedMeshRenderer>().updateWhenOffscreen=true;
            var controller=(AnimatorController)p.animator.runtimeAnimatorController;var tree=(BlendTree)controller.layers[0].stateMachine.defaultState.motion;
            var children=tree.children;children[1].threshold=p.walkSpeed;children[2].threshold=p.runSpeed;tree.children=children;EditorUtility.SetDirty(tree);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            return $"Input map={input.defaultActionMap}, ground={p.groundMask.value}, blend speeds={p.walkSpeed}/{p.runSpeed}";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void Solve(Transform upper,Transform lower,Transform hand,Transform target,Vector3 hint)
    {
        var a=upper.position;float l1=Vector3.Distance(a,lower.position),l2=Vector3.Distance(lower.position,hand.position);
        var direction=(target.position-a).normalized;float d=Mathf.Clamp(Vector3.Distance(a,target.position),.01f,l1+l2-.001f);
        float along=(l1*l1-l2*l2+d*d)/(2*d);float height=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        var pole=Vector3.ProjectOnPlane(hint-a,direction).normalized;var elbow=a+direction*along+pole*height;
        upper.rotation=Quaternion.FromToRotation(lower.position-a,elbow-a)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target.position-lower.position)*lower.rotation;hand.rotation=target.rotation;
    }
    public static string PoseAndAlign()
    {
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var p=root.GetComponent<PlayerWithGFXPresentation>();var a=p.animator;a.Rebind();a.Update(.1f);
            Solve(a.GetBoneTransform(HumanBodyBones.RightUpperArm),a.GetBoneTransform(HumanBodyBones.RightLowerArm),a.GetBoneTransform(HumanBodyBones.RightHand),p.rightGrip,root.transform.TransformPoint(.36f,1.05f,.02f));
            Solve(a.GetBoneTransform(HumanBodyBones.LeftUpperArm),a.GetBoneTransform(HumanBodyBones.LeftLowerArm),a.GetBoneTransform(HumanBodyBones.LeftHand),p.leftGrip,root.transform.TransformPoint(-.38f,1.12f,.22f));
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var instance=scene.GetRootGameObjects().FirstOrDefault(r=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r)==Prefab);
        if(instance)
        {
            Undo.RecordObject(instance.transform,"Align PlayerWithGFX");instance.transform.rotation=Quaternion.identity;instance.transform.localScale=Vector3.one;
            var floor=Physics.RaycastAll(instance.transform.position+Vector3.up*3,Vector3.down,10,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Where(h=>!h.transform.IsChildOf(instance.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            if(floor.collider){var v=instance.transform.position;v.y=floor.point.y;instance.transform.position=v;}
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=instance;SceneView.lastActiveSceneView?.Frame(new Bounds(instance.transform.position+Vector3.up*.95f,new Vector3(1.5f,2.1f,1.5f)),false);
        }
        return "Saved rifle stance and aligned only the PlayerWithGFX scene instance";
    }
    public static string AddFirstPersonRifle()
    {
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var p=root.GetComponent<PlayerWithGFXPresentation>();var camera=p.playerCamera;camera.cullingMask=~0;
            foreach(var r in p.animator.GetComponentsInChildren<Renderer>())r.gameObject.layer=0;
            foreach(var r in p.rifleAim.GetComponentsInChildren<Renderer>(true))r.gameObject.layer=0;
            var old=camera.transform.Find("FirstPersonRifle");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var view=UnityEngine.Object.Instantiate(p.rifleAim.Find("Sentinel_Rifle").gameObject,camera.transform);view.name="FirstPersonRifle";
            view.transform.localPosition=new Vector3(.22f,-.2f,.45f);view.transform.localRotation=Quaternion.identity;view.transform.localScale=Vector3.one*.8f;
            foreach(var r in view.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
            p.firstPersonRifle=view.transform;p.firstPersonFlash=view.transform.Find("MuzzlePoint/MuzzleFlash").gameObject;view.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);return "Owner-only rifle view added; world characters remain visible to all player cameras";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
