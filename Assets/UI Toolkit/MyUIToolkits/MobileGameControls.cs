using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class MobileGameControls : IDisposable
{
    readonly InputManager input;
    readonly VisualElement root, layer, stick, knob, look;
    readonly Dictionary<string, VisualElement> controls = new Dictionary<string, VisualElement>();
    int movePointer = -1, lookPointer = -1, sprintPointer = -1;
    Vector2 lastLook, lookDelta;
    bool sprint;
    public MobileGameControls(VisualElement root, InputManager input)
    {
        this.root = root; this.input = input;
        layer = new VisualElement { name = "MobileControls", pickingMode = PickingMode.Ignore };
        layer.AddToClassList("mobile-controls");
        root.Q("GameHud").Insert(0, layer);
        look = new VisualElement { name = "LookArea" }; look.AddToClassList("look-area"); layer.Add(look);
        stick = Add("Joystick", "", "touch-joystick");
        knob = new VisualElement { pickingMode = PickingMode.Ignore }; knob.AddToClassList("joystick-knob"); stick.Add(knob);
        var sprintButton = Add("Sprint", "SPRINT", "touch-sprint");
        var jumpButton = Add("Jump", "JUMP", "touch-jump");
        var fireButton = Add("Fire", "FIRE", "touch-fire");
        fireButton.RegisterCallback<PointerDownEvent>(e => { if (!input.IsGameplayBlocked) input.TouchFireFrame = Time.frameCount; e.StopPropagation(); });
        stick.RegisterCallback<PointerDownEvent>(e => { if (movePointer >= 0 || input.IsGameplayBlocked) return; movePointer = e.pointerId; stick.CapturePointer(e.pointerId); Move(e.position); e.StopPropagation(); });
        stick.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == movePointer) Move(e.position); });
        stick.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == movePointer) { stick.ReleasePointer(e.pointerId); ClearMove(); } });
        stick.RegisterCallback<PointerCancelEvent>(e => ClearMove());
        stick.RegisterCallback<PointerCaptureOutEvent>(e => ClearMove());
        look.RegisterCallback<PointerDownEvent>(e => { if (lookPointer >= 0 || input.IsGameplayBlocked) return; lookPointer = e.pointerId; lastLook = e.position; look.CapturePointer(e.pointerId); e.StopPropagation(); });
        look.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId != lookPointer) return; Vector2 p = e.position; lookDelta += p - lastLook; lastLook = p; });
        look.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == lookPointer) { look.ReleasePointer(e.pointerId); ClearLook(); } });
        look.RegisterCallback<PointerCancelEvent>(e => ClearLook());
        look.RegisterCallback<PointerCaptureOutEvent>(e => ClearLook());
        sprintButton.RegisterCallback<PointerDownEvent>(e => { if (input.IsGameplayBlocked || sprintPointer >= 0) return; sprintPointer = e.pointerId; sprint = true; sprintButton.CapturePointer(e.pointerId); e.StopPropagation(); });
        sprintButton.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != sprintPointer) return; sprintButton.ReleasePointer(e.pointerId); sprintPointer = -1; sprint = false; });
        sprintButton.RegisterCallback<PointerCaptureOutEvent>(e => { sprintPointer = -1; sprint = false; });
        sprintButton.RegisterCallback<PointerCancelEvent>(e => { sprintPointer = -1; sprint = false; });
        jumpButton.RegisterCallback<PointerDownEvent>(e => { if (!input.IsGameplayBlocked) input.TouchJumpFrame = Time.frameCount; e.StopPropagation(); });
        layer.style.display = Application.isMobilePlatform ? DisplayStyle.Flex : DisplayStyle.None;
    }
    VisualElement Add(string name, string text, string position)
    {
        var element = new VisualElement { name = name }; element.AddToClassList("touch-control"); element.AddToClassList(position);
        if (text.Length > 0) element.Add(new Label(text) { pickingMode = PickingMode.Ignore });
        layer.Add(element); controls.Add(name, element); return element;
    }
    void Move(Vector2 position)
    {
        Vector2 delta = stick.WorldToLocal(position) - stick.layout.size * .5f;
        float radius = Mathf.Max(1, stick.layout.width * .32f);
        delta = Vector2.ClampMagnitude(delta / radius, 1);
        input.TouchMove = new Vector2(delta.x, -delta.y);
        knob.style.left = Length.Percent(50 + delta.x * 32); knob.style.top = Length.Percent(50 + delta.y * 32);
    }
    void ClearMove() { movePointer = -1; input.TouchMove = Vector2.zero; knob.style.left = Length.Percent(50); knob.style.top = Length.Percent(50); }
    void ClearLook() { lookPointer = -1; lookDelta = Vector2.zero; input.TouchLook = Vector2.zero; }
    public void Tick()
    {
        if (input.IsGameplayBlocked) { Reset(); return; }
        input.TouchSprint = sprint || (input.TouchMove.y > .9f && Mathf.Abs(input.TouchMove.x) < .25f);
        float sensitivity = PlayerPrefs.GetFloat("toys.touch.sensitivity", 1f);
        input.TouchLook = new Vector2(lookDelta.x, -lookDelta.y) * sensitivity / Mathf.Max(Time.deltaTime, .001f);
        lookDelta = Vector2.zero;
        if (Application.isMobilePlatform && root.panel != null)
        {
            Rect safe = Screen.safeArea;
            var hud = root.Q("GameHud");
            float scale = root.worldBound.width / Mathf.Max(1, Screen.width);
            hud.style.paddingLeft = 40 + safe.xMin * scale;
            hud.style.paddingRight = 40 + (Screen.width - safe.xMax) * scale;
            hud.style.paddingBottom = 32 + safe.yMin * scale;
            hud.style.paddingTop = 32 + (Screen.height - safe.yMax) * scale;
        }
    }
    public void Reset()
    {
        if (movePointer >= 0) stick.ReleasePointer(movePointer);
        if (lookPointer >= 0) look.ReleasePointer(lookPointer);
        if (sprintPointer >= 0) controls["Sprint"].ReleasePointer(sprintPointer);
        sprintPointer = -1; sprint = false; ClearMove(); ClearLook(); input.TouchSprint = false;
        input.TouchJumpFrame = input.TouchFireFrame = -1;
    }
    public void Dispose() { Reset(); layer.RemoveFromHierarchy(); }
}
