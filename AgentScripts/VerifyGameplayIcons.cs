UnityEditor.AssetDatabase.Refresh();
var results = new System.Collections.Generic.List<string>();
foreach (string icon in new[] { "fire", "jump", "sprint", "settings", "leave", "health", "ammo", "reload", "close", "target" })
{
    string path = "Assets/UI Toolkit/MyUIToolkits/Icons/" + icon + ".svg";
    var asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
    if (!asset) throw new System.Exception("Icon import failed: " + icon);
    results.Add(icon + ":" + asset.GetType().Name);
}
return string.Join(", ", results);
