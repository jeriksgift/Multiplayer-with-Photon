var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI Toolkit/MyUIToolkits/game-hud.uxml");
var root = tree.CloneTree();
foreach (var name in new[] { "GameHud", "ScoreboardOverlay", "ScoreArea", "AmmoArea", "CloseScoreboard" })
    if (UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, name) == null) throw new System.Exception("Missing HUD element: " + name);
return "HUD UXML imported; scoreboard, score touch target, ammo target and close button present.";
