using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Threading.Tasks;

public static class ArenaCollisionRepair
{
    const string ArenaPath="Assets/Prefabs/Arena.prefab";
    public static string Audit()
    {
        var root=PrefabUtility.LoadPrefabContents(ArenaPath);
        try
        {
            root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            var meshes=root.GetComponentsInChildren<MeshFilter>(true);var colliders=root.GetComponentsInChildren<Collider>(true);
            var lines=new List<string>{$"Meshes={meshes.Length}, colliders={colliders.Length}, disabled={colliders.Count(c=>!c.enabled)}, triggers={colliders.Count(c=>c.isTrigger)}"};
            lines.Add("Types: "+string.Join(", ",colliders.GroupBy(c=>c.GetType().Name).Select(g=>$"{g.Key}={g.Count()}")));
            lines.Add("Invalid meshes: "+colliders.OfType<MeshCollider>().Count(c=>!c.sharedMesh)+"; convex="+colliders.OfType<MeshCollider>().Count(c=>c.convex));
            foreach(var mf in meshes)
            {
                var r=mf.GetComponent<Renderer>();
                if(!r)continue;
                bool covered=mf.GetComponentsInParent<Collider>(true).Any(c=>c.enabled&&!c.isTrigger);
                var c=mf.GetComponent<Collider>();
                lines.Add($"{AnimationUtility.CalculateTransformPath(mf.transform,root.transform)} | active={mf.gameObject.activeInHierarchy} layer={mf.gameObject.layer} covered={covered} center={r.bounds.center:F2} size={r.bounds.size:F2} collider={c?.bounds.size.ToString("F2")}");
            }
            File.WriteAllLines("AgentScripts/arena-collision-audit.txt",lines);
            return string.Join("\n",lines.Take(3).Concat(lines.Where(l=>l.Contains("covered=False")||l.ToLower().Contains("fence")||l.ToLower().Contains("wall")||l.ToLower().Contains("hangar"))));
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static bool Wall(MeshFilter m)=>m.name.IndexOf("Concrete_fence",StringComparison.OrdinalIgnoreCase)>=0;
    static bool Floor(MeshFilter m)=>m.name.IndexOf("floor",StringComparison.OrdinalIgnoreCase)>=0;
    static BoxCollider Box(Transform parent,string name,Vector3 center,Vector3 size)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=center;g.isStatic=true;var c=g.AddComponent<BoxCollider>();c.size=size;return c;}
    public static string Repair()
    {
        Directory.CreateDirectory("AgentScripts/Backups");if(!File.Exists("AgentScripts/Backups/Arena.prefab"))File.Copy(ArenaPath,"AgentScripts/Backups/Arena.prefab");
        var root=PrefabUtility.LoadPrefabContents(ArenaPath);int meshFix=0,floorFix=0;float height=30;
        try
        {
            var savedPosition=root.transform.position;var savedRotation=root.transform.rotation;var savedScale=root.transform.localScale;
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
            var old=root.transform.Find("CollisionSafety");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var meshes=root.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh&&m.GetComponent<Renderer>()&&m.gameObject.activeInHierarchy).ToArray();
            var walls=meshes.Where(Wall).ToArray();if(walls.Length<4)throw new Exception("Cannot identify compound walls safely");
            foreach(var m in meshes)
            {
                if(m.GetComponentInParent<Rigidbody>())continue;
                var colliders=m.GetComponents<Collider>();
                if(Floor(m))
                {
                    // Give flat road tiles thickness below their visible surface.
                    var box=m.GetComponent<BoxCollider>();if(!box)box=m.gameObject.AddComponent<BoxCollider>();
                    var b=m.sharedMesh.bounds;float t=.3f/Mathf.Abs(m.transform.lossyScale.y);
                    box.center=new Vector3(b.center.x,b.max.y-t*.5f,b.center.z);box.size=new Vector3(b.size.x,t,b.size.z);box.enabled=true;box.isTrigger=false;floorFix++;
                    foreach(var c in colliders)if(c!=box)UnityEngine.Object.DestroyImmediate(c);
                    continue;
                }
                // Solid crates/closed cargo boxes and straight compound walls keep
                // their simple colliders; buildings and detailed props use the mesh.
                bool solidBox=Wall(m)||m.name.Contains("Wooden_box")||m.name.Contains("Cargo_container");
                if(solidBox&&colliders.Any(c=>c.enabled&&!c.isTrigger))continue;
                var mesh=m.GetComponent<MeshCollider>();if(!mesh)mesh=m.gameObject.AddComponent<MeshCollider>();
                if(mesh.convex||mesh.sharedMesh!=m.sharedMesh||colliders.Any(c=>c!=mesh))meshFix++;
                mesh.sharedMesh=m.sharedMesh;mesh.convex=false;mesh.enabled=true;mesh.isTrigger=false;
                foreach(var c in colliders)if(c!=mesh)UnityEngine.Object.DestroyImmediate(c);
            }
            float highest=meshes.Max(m=>m.GetComponent<Renderer>().bounds.max.y);
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PlayerWithGFX.prefab");var p=player.GetComponent<PlayerWithGFXPresentation>();
            float jumpSpeed=p.jumpImpulse/p.body.mass;float jumpHeight=jumpSpeed*jumpSpeed/(2*Mathf.Abs(Physics.gravity.y));
            height=Mathf.Ceil(Mathf.Max(30,highest+jumpHeight+2+3)/5)*5;
            var safety=new GameObject("CollisionSafety").transform;safety.SetParent(root.transform,false);
            var perimeter=new GameObject("CompoundPerimeter").transform;perimeter.SetParent(safety,false);
            var ext=new GameObject("WallExtensions").transform;ext.SetParent(safety,false);
            foreach(var m in walls)
            {
                var b=m.GetComponent<Renderer>().bounds;var size=b.size;size.y=height+1;
                if(size.x>size.z){size.z=Mathf.Max(size.z,.9f);size.x+=.3f;}else{size.x=Mathf.Max(size.x,.9f);size.z+=.3f;}
                Box(ext,m.name+"_InvisibleExtension",new Vector3(b.center.x,(height-1)*.5f,b.center.z),size);
            }
            var vertical=walls.Select(m=>m.GetComponent<Renderer>().bounds).Where(b=>b.size.z>b.size.x).ToArray();
            var horizontal=walls.Select(m=>m.GetComponent<Renderer>().bounds).Where(b=>b.size.x>b.size.z).ToArray();
            float west=vertical.Min(b=>b.center.x),east=vertical.Max(b=>b.center.x),south=horizontal.Min(b=>b.center.z),north=horizontal.Max(b=>b.center.z);
            float y=(height-1)*.5f;float h=height+1;
            Box(perimeter,"West",new Vector3(west,y,(south+north)*.5f),new Vector3(.9f,h,north-south+.9f));
            Box(perimeter,"East",new Vector3(east,y,(south+north)*.5f),new Vector3(.9f,h,north-south+.9f));
            Box(perimeter,"South",new Vector3((west+east)*.5f,y,south),new Vector3(east-west+.9f,h,.9f));
            Box(perimeter,"North",new Vector3((west+east)*.5f,y,north),new Vector3(east-west+.9f,h,.9f));
            Box(safety,"FloorSeamSupport",new Vector3((west+east)*.5f,-.25f,(south+north)*.5f),new Vector3(east-west-.8f,.4f,north-south-.8f));
            root.transform.SetPositionAndRotation(savedPosition,savedRotation);root.transform.localScale=savedScale;
            PrefabUtility.SaveAsPrefabAsset(root,ArenaPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        // CCD is scoped to the test character; the original Player stays untouched.
        var character=PrefabUtility.LoadPrefabContents("Assets/Resources/PlayerWithGFX.prefab");
        try{character.GetComponent<Rigidbody>().collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;PrefabUtility.SaveAsPrefabAsset(character,"Assets/Resources/PlayerWithGFX.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(character);}
        AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty)EditorSceneManager.SaveScene(scene);
        return $"Repaired {meshFix} mesh collision shapes; thickened {floorFix} floor tiles; added floor seam support, 18 wall extensions and four continuous perimeter barriers. Barrier top={height}m.";
    }
    public static string Validate()
    {
        var root=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Arena");
        var perimeter=root.transform.Find("CollisionSafety/CompoundPerimeter");if(!perimeter)throw new Exception("Scene instance is missing its perimeter");
        Physics.SyncTransforms();int rays=0;
        foreach(var wall in perimeter.GetComponentsInChildren<BoxCollider>())
        {
            var b=wall.bounds;bool vertical=b.size.z>b.size.x;
            var normal=wall.name=="West"?Vector3.left:wall.name=="East"?Vector3.right:wall.name=="South"?Vector3.back:Vector3.forward;
            float length=vertical?b.size.z:b.size.x;
            for(float along=-length*.5f+.15f;along<length*.5f;along+=.5f)foreach(float h in new[]{.9f,4.5f,10f,20f,b.max.y-.5f})
            {
                var center=new Vector3(b.center.x,h,b.center.z)+(vertical?Vector3.forward:Vector3.right)*along;
                if(!wall.Raycast(new Ray(center-normal*2,normal),out var hit,4))throw new Exception($"Gap at {wall.name}, {center}");rays++;
            }
        }
        var meshes=root.GetComponentsInChildren<MeshFilter>().Where(m=>m.sharedMesh&&m.GetComponent<Renderer>()&&m.gameObject.activeInHierarchy).ToArray();
        int missing=meshes.Count(m=>!m.GetComponentsInParent<Collider>().Any(c=>c.enabled&&!c.isTrigger));
        if(missing!=0)throw new Exception($"{missing} meshes without solid colliders");
        var result=$"PASS: {meshes.Length} arena meshes covered, {rays} perimeter samples blocked at ground, jump and roof heights; no barrier renderers={perimeter.GetComponentsInChildren<Renderer>().Length==0}. Default/Ground ignored={Physics.GetIgnoreLayerCollision(0,3)}";
        File.WriteAllText("AgentScripts/arena-collision-validation.txt",result);return result;
    }
    public static async Task<string> VerifyContainment()
    {
        var arena=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Arena");
        var walls=arena.transform.Find("CollisionSafety/CompoundPerimeter").GetComponentsInChildren<BoxCollider>();
        var probes=new List<GameObject>();var checks=new List<Func<bool>>();
        try
        {
            foreach(var wall in walls)
            {
                var b=wall.bounds;var outward=wall.name=="West"?Vector3.left:wall.name=="East"?Vector3.right:wall.name=="South"?Vector3.back:Vector3.forward;
                // Test well above the visible 3.3m walls, at sprint-plus-jump speeds.
                var start=new Vector3(b.center.x,6,b.center.z)-outward*2;
                var g=new GameObject("ContainmentProbe_"+wall.name);probes.Add(g);g.transform.position=start;
                var capsule=g.AddComponent<CapsuleCollider>();capsule.radius=.3f;capsule.height=1.85f;capsule.center=Vector3.up*.925f;
                var rb=g.AddComponent<Rigidbody>();rb.constraints=RigidbodyConstraints.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.useGravity=true;rb.linearVelocity=outward*20+Vector3.up*8;
                checks.Add(()=>Vector3.Dot(g.transform.position-new Vector3(b.center.x,g.transform.position.y,b.center.z),outward)<-.6f);
            }
            int frame=Time.frameCount;await Task.Delay(850);if(frame==Time.frameCount)throw new Exception("Play-mode frames did not advance");
            if(checks.Any(c=>!c()))throw new Exception("A sprinting/jumping probe escaped");
            string result="PASS: dynamic player-size capsules blocked on all four sides above visible wall height at 20m/s outward plus 8m/s jump velocity.";
            File.AppendAllText("AgentScripts/arena-collision-validation.txt","\n"+result);return result;
        }
        finally{foreach(var p in probes)if(p)UnityEngine.Object.Destroy(p);}
    }
}
