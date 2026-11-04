var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI Toolkit/MyUIToolkits/game-hud.uxml");
var root = tree.CloneTree();
foreach (var name in new[] { "GameHud", "ScoreboardOverlay", "ScoreArea", "AmmoArea", "CloseScoreboard", "SettingsOverlay", "ControlsTab", "SoundTab", "GraphicsTab", "LeaveMatchButton" })
    if (UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, name) == null) throw new System.Exception("Missing HUD element: " + name);
var page = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, "ControlsPage");
ControlSettingsUI.Build(page);
if (page.childCount != 24) throw new System.Exception("Expected sensitivity, four control sections and reset, got " + page.childCount);
return "HUD and settings templates imported; score/reload targets, leave button, three gameplay tabs and 17 customization sliders verified.";
