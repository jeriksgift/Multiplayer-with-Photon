using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class LoadingScreen : MonoBehaviour
{
    private VisualElement panel;
    private VisualElement fill;
    private Label status;
    private Label progress;
    private float animationStart;
    public bool IsVisible { get; private set; }

    private void Bind()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        panel = root.Q("LoadingPanel");
        fill = root.Q("LoadingFill");
        status = root.Q<Label>("LoadingStatus");
        progress = root.Q<Label>("LoadingProgress");
    }

    public void Show(string message)
    {
        Bind();
        status.text = message;
        animationStart = Time.unscaledTime;
        IsVisible = true;
        panel.RemoveFromClassList("loading-hidden");
        SetProgress(0f);
    }

    public void Hide()
    {
        Bind();
        IsVisible = false;
        panel.AddToClassList("loading-hidden");
    }

    public void SetProgress(float value)
    {
        if (!IsVisible) return;
        bool determinate = value > 0f && value <= 1f;
        // These animated dimensions are driven by network progress, not layout styling.
        fill.style.left = Length.Percent(determinate ? 0f : Mathf.PingPong((Time.unscaledTime - animationStart) * 40f, 78f));
        fill.style.width = Length.Percent(determinate ? Mathf.Clamp01(value) * 100f : 22f);
        progress.text = determinate ? Mathf.RoundToInt(value * 100f) + "%" : string.Empty;
    }
}
