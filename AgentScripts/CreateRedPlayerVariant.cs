using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CreateRedPlayerVariant
{
    const string Base="Assets/Resources/PlayerWithGFX.prefab";
    const string Target="Assets/Resources/PlayerWithGFX_Red.prefab";
    const string Dir="Assets/PlayerWithGFX/Red/";
    static Material CopyMaterial(string source,string name)
    {
        string path=Dir+name+".mat";
        if(!AssetDatabase.LoadAssetAtPath<Material>(path)&&!AssetDatabase.CopyAsset(source,path))throw new Exception("Could not create "+path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }
    public static string Create()
    {
        AssetDatabase.Refresh();
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Target))throw new Exception("Red variant already exists; refusing to overwrite it");
        var armor=CopyMaterial("Assets/PlayerWithGFX/SoldierArmor.mat","SoldierArmorRed");
        armor.shader=Shader.Find("PlayerWithGFX/RedArmor");if(!armor.shader)throw new Exception("Red armour shader failed to load");EditorUtility.SetDirty(armor);
        var rifle=CopyMaterial("Assets/PlayerWithGFX/RifleArmorBlue.mat","RifleArmorRed");rifle.color=new Color(.64f,.11f,.085f);EditorUtility.SetDirty(rifle);
        var sight=CopyMaterial("Assets/PlayerWithGFX/RifleSight.mat","RifleSightRed");sight.color=new Color(1,.13f,.06f);sight.SetColor("_EmissionColor",new Color(1.5f,.08f,.025f));EditorUtility.SetDirty(sight);
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base),scene);root.name="PlayerWithGFX_Red";
            int replacements=0;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<mats.Length;i++)
                {
                    var path=AssetDatabase.GetAssetPath(mats[i]);
                    var replacement=path=="Assets/PlayerWithGFX/SoldierArmor.mat"?armor:path=="Assets/PlayerWithGFX/RifleArmorBlue.mat"?rifle:path=="Assets/PlayerWithGFX/RifleSight.mat"?sight:null;
                    if(replacement){mats[i]=replacement;changed=true;replacements++;}
                }
                if(changed){renderer.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
            }
            PrefabUtility.SaveAsPrefabAsset(root,Target);AssetDatabase.SaveAssets();
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Target);var p=asset.GetComponent<PlayerWithGFXPresentation>();var original=AssetDatabase.LoadAssetAtPath<GameObject>(Base).GetComponent<PlayerWithGFXPresentation>();
            if(PrefabUtility.GetPrefabAssetType(asset)!=PrefabAssetType.Variant)throw new Exception("Expected an inherited prefab variant");
            if(p.animator.avatar!=original.animator.avatar||p.animator.runtimeAnimatorController!=original.animator.runtimeAnimatorController||p.body.constraints!=original.body.constraints||p.body.collisionDetectionMode!=original.body.collisionDetectionMode)throw new Exception("Gameplay settings differ from the blue original");
            Selection.activeObject=asset;
            return $"Created inherited red prefab variant; {replacements} material slots changed, matching rig, controller, Rigidbody constraints and CCD.";
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static string Preview()
    {
        var preview=new PreviewRenderUtility();
        try
        {
            int i=0;
            foreach(var path in new[]{Base,Target})
            {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));root.transform.position=new Vector3(i++==0?-.75f:.75f,0,0);
                foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                foreach(var a in root.GetComponentsInChildren<Animator>())a.enabled=false;
                foreach(var c in root.GetComponentsInChildren<Camera>(true))c.gameObject.SetActive(false);
                foreach(var c in root.GetComponentsInChildren<Canvas>(true))c.gameObject.SetActive(false);
                preview.AddSingleGO(root);
            }
            preview.camera.transform.position=new Vector3(2.2f,1.8f,5.8f);preview.camera.transform.LookAt(new Vector3(0,.95f,0));preview.camera.fieldOfView=29;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=30;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.15f,.19f);
            preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);preview.lights[1].intensity=1.1f;preview.lights[1].transform.rotation=Quaternion.Euler(340,200,0);preview.ambientColor=new Color(.5f,.5f,.5f);
            preview.BeginPreview(new Rect(0,0,1400,1000),GUIStyle.none);preview.Render(true);var texture=preview.EndPreview();
            var rt=RenderTexture.GetTemporary(1400,1000,0);Graphics.Blit(texture,rt);var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();Directory.CreateDirectory("Screenshots");File.WriteAllBytes("Screenshots/PlayerWithGFX-blue-and-red.png",image.EncodeToPNG());RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);
            var shader=Shader.Find("PlayerWithGFX/RedArmor");var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").ToArray();
            if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.message)));
            return "Screenshots/PlayerWithGFX-blue-and-red.png; no shader compile errors.";
        }
        finally{preview.Cleanup();}
    }
}
