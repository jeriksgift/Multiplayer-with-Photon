using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class ControlSettingsUI
{
    public static event Action Changed;
    public static readonly string[] Names = { "Joystick", "Sprint", "Jump", "Fire" };
    static readonly Vector2[] Positions = { new Vector2(11, 82), new Vector2(12, 59), new Vector2(91, 59), new Vector2(79, 73) };
    public static float Get(string control, string property, float fallback) => PlayerPrefs.GetFloat("toys.touch." + control + "." + property, fallback);
    public static Vector2 Position(string name)
    {
        int index = Array.IndexOf(Names, name);
        return new Vector2(Get(name, "x", Positions[index].x), Get(name, "y", Positions[index].y));
    }
    public static void Build(VisualElement page)
    {
        page.Clear();
        var title = new Label("Touch controls"); title.AddToClassList("toys-heading"); page.Add(title);
        var help = new Label("Drag the right side to look. Push the joystick fully forward to sprint. Positions are percentages from the top left of the safe screen area."); help.AddToClassList("toys-caption"); page.Add(help);
        Slider(page, "Camera sensitivity", "toys.touch.sensitivity", .1f, 3f, 1f);
        foreach (string name in Names)
        {
            var heading = new Label(name); heading.AddToClassList("toys-heading"); page.Add(heading);
            var position = Position(name);
            string prefix = "toys.touch." + name + ".";
            Slider(page, "Size", prefix + "size", .65f, 1.5f, 1);
            Slider(page, "Horizontal %", prefix + "x", 0, 100, position.x);
            Slider(page, "Vertical %", prefix + "y", 0, 100, position.y);
            Slider(page, "Opacity", prefix + "opacity", .25f, 1, 1);
        }
        var reset = new Button(() =>
        {
            PlayerPrefs.DeleteKey("toys.touch.sensitivity");
            foreach (string name in Names)
                foreach (string property in new[] { "size", "x", "y", "opacity" }) PlayerPrefs.DeleteKey("toys.touch." + name + "." + property);
            PlayerPrefs.Save(); Changed?.Invoke(); Build(page);
        }) { text = "Reset controls" };
        reset.AddToClassList("toys-button"); page.Add(reset);
    }
    static void Slider(VisualElement parent, string title, string key, float min, float max, float fallback)
    {
        var slider = new Slider(title, min, max) { value = PlayerPrefs.GetFloat(key, fallback), showInputField = true };
        slider.AddToClassList("settings-slider");
        slider.RegisterValueChangedCallback(evt => { PlayerPrefs.SetFloat(key, Mathf.Clamp(evt.newValue, min, max)); Changed?.Invoke(); });
        parent.Add(slider);
    }
}
