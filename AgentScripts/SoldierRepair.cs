using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class SoldierRepair
{
    const string Folder = "Assets/Models/Generated/Soldier/";
    const string Model = Folder + "WarZoneSoldier.fbx";
    public static string Inspect()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var root = UnityEngine.Object.Instantiate(model);
        try
        {
            var lines = root.GetComponentsInChildren<Transform>(true).Select(t =>
                $"{t.name} | parent={t.parent?.name} | pos={t.position:F4} | local={t.localPosition:F4}").ToList();
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                lines.Add($"MESH {r.name} vertices={r.sharedMesh.vertexCount} materials={string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null"))}");
            foreach(var a in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>())
                lines.Add($"AVATAR {a.name} valid={a.isValid} human={a.isHuman}");
            Directory.CreateDirectory("AgentScripts");
            File.WriteAllLines("AgentScripts/soldier-inspection.txt", lines);
            return string.Join("\n",lines);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
