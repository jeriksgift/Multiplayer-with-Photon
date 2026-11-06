using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class ControlSettingsUI
{
    public static event Action Changed;
    public static readonly string[] Names = { "Joystick", "Sprint", "Jump", "Fire" };
    static readonly Vector2[] Defaults = { new Vector2(11,82), new Vector2(12,59), new Vector2(91,59), new Vector2(79,73) };
    public static float Get(string name, string property, float fallback) => PlayerPrefs.GetFloat("toys.touch." + name + "." + property, fallback);
    public static Vector2 Position(string name) { var p = Defaults[Array.IndexOf(Names,name)]; return new Vector2(Get(name,"x",p.x),Get(name,"y",p.y)); }
    public static Rect SafeRect(float width, float height)
    {
        var s = Screen.safeArea;
        return new Rect(s.x / Mathf.Max(1,Screen.width)*width, (Screen.height-s.yMax)/Mathf.Max(1,Screen.height)*height,
            s.width/Mathf.Max(1,Screen.width)*width, s.height/Mathf.Max(1,Screen.height)*height);
    }
    public static Rect ControlRect(string name, Rect safe, Vector2 position, float scale)
    {
        float size = Mathf.Min((name=="Joystick" ? 220 : name=="Fire" ? 160 : 130)*scale, safe.width*.4f, safe.height*.4f);
        return new Rect(safe.x+Mathf.Clamp(safe.width*position.x/100-size/2,0,safe.width-size),
            safe.y+Mathf.Clamp(safe.height*position.y/100-size/2,0,safe.height-size),size,size);
    }
    public static void Build(VisualElement page, VisualElement root)
    {
        page.Clear();
        var help = new Label("Drag controls into place. Select one to adjust its size and opacity. Save your layout when it feels right.");
        help.AddToClassList("toys-caption"); page.Add(help);
        var button = new Button(()=>OpenEditor(root)) {text="Customize HUD",name="CustomizeHudButton"};
        button.AddToClassList("toys-button"); page.Add(button);
        var sensitivity = new Slider("Look sensitivity", .1f, 3f) { name = "HudLookSensitivity", showInputField = true };
        sensitivity.AddToClassList("settings-slider");
        sensitivity.SetValueWithoutNotify(PlayerPrefs.GetFloat("toys.touch.sensitivity", 1f));
        sensitivity.RegisterValueChangedCallback(evt => { PlayerPrefs.SetFloat("toys.touch.sensitivity", evt.newValue); PlayerPrefs.Save(); Changed?.Invoke(); });
        page.Add(sensitivity);
        root.Q("HudLayoutEditor")?.RemoveFromHierarchy();
        var editor = new LayoutEditor(root.Q("SettingsOverlay").parent); editor.Overlay.userData=editor;
    }
    public static void OpenEditor(VisualElement root) => (root.Q("HudLayoutEditor")?.userData as LayoutEditor)?.Open();
    public static bool CloseEditor(VisualElement root)
    {
        var editor = root?.Q("HudLayoutEditor")?.userData as LayoutEditor;
        if(editor==null || !editor.IsOpen) return false;
        editor.Close(); return true;
    }
    sealed class LayoutEditor
    {
        public readonly VisualElement Overlay;
        readonly VisualElement canvas, inspector;
        readonly Label heading;
        readonly Slider size, opacity;
        readonly Dictionary<string,VisualElement> controls=new Dictionary<string,VisualElement>();
        readonly Dictionary<string,Vector2> positions=new Dictionary<string,Vector2>();
        readonly Dictionary<string,float> sizes=new Dictionary<string,float>(), opacities=new Dictionary<string,float>();
        string selected;
        VisualElement dragged;
        int pointer=-1;
        Vector2 offset;
        public bool IsOpen => !Overlay.ClassListContains("toys-hidden");
        public LayoutEditor(VisualElement root)
        {
            Overlay=new VisualElement {name="HudLayoutEditor"};
            Overlay.AddToClassList("layout-editor"); Overlay.AddToClassList("toys-ui"); Overlay.AddToClassList("toys-hidden"); root.Add(Overlay);
            canvas=new VisualElement {name="HudLayoutCanvas"}; canvas.AddToClassList("layout-canvas"); Overlay.Add(canvas);
            Context("ROOM / DEATHMATCH","layout-room"); Context("0    20:00    0","layout-score");
            Context("+","layout-crosshair"); Context("HEALTH  100","layout-health"); Context("07 / 07","layout-ammo");
            foreach(string name in Names)
            {
                var control=new VisualElement {name="Layout"+name,tooltip="Drag "+name,focusable=true};
                control.AddToClassList("layout-control"); control.AddToClassList("layout-"+name.ToLowerInvariant());
                var icon=new VisualElement {pickingMode=PickingMode.Ignore};
                icon.AddToClassList(name=="Joystick" ? "layout-stick-knob":"layout-control-icon");
                icon.AddToClassList("layout-icon-"+name.ToLowerInvariant()); control.Add(icon);
                controls.Add(name,control); canvas.Add(control);
                control.RegisterCallback<PointerDownEvent>(evt=>{
                    if(pointer>=0 || evt.button!=0) return;
                    Select(name); dragged=control; pointer=evt.pointerId;
                    offset=canvas.WorldToLocal(evt.position)-new Vector2(control.resolvedStyle.left,control.resolvedStyle.top);
                    control.CapturePointer(pointer); evt.StopPropagation();
                });
                control.RegisterCallback<PointerMoveEvent>(evt=>{
                    if(pointer!=evt.pointerId || dragged!=control) return;
                    Rect safe=SafeRect(canvas.layout.width,canvas.layout.height);
                    if(safe.width<=0 || safe.height<=0) return;
                    float diameter=control.resolvedStyle.width;
                    Vector2 p=canvas.WorldToLocal(evt.position)-offset;
                    p.x=Mathf.Clamp(p.x,safe.xMin,safe.xMax-diameter); p.y=Mathf.Clamp(p.y,safe.yMin,safe.yMax-diameter);
                    positions[name]=new Vector2((p.x+diameter/2-safe.x)/safe.width*100,(p.y+diameter/2-safe.y)/safe.height*100);
                    control.style.translate=new Translate(p.x-control.resolvedStyle.left,p.y-control.resolvedStyle.top);
                    evt.StopPropagation();
                });
                control.RegisterCallback<PointerUpEvent>(evt=>{if(evt.pointerId==pointer){EndDrag();evt.StopPropagation();}});
                control.RegisterCallback<PointerCancelEvent>(evt=>{if(evt.pointerId==pointer)EndDrag();});
                control.RegisterCallback<PointerCaptureOutEvent>(evt=>{if(evt.pointerId==pointer)EndDrag();});
                control.RegisterCallback<KeyDownEvent>(evt=>{if(evt.keyCode==KeyCode.Return || evt.keyCode==KeyCode.Space){Select(name);evt.StopPropagation();}});
            }
            var toolbar=new VisualElement(); toolbar.AddToClassList("layout-toolbar"); Overlay.Add(toolbar);
            var hint=new Label("CUSTOM HUD · Drag / tap to edit"); hint.AddToClassList("layout-hint"); toolbar.Add(hint);
            Button(toolbar,"Reset",Reset,"ResetHudLayout"); Button(toolbar,"Cancel",Close,"CancelHudLayout"); Button(toolbar,"Save",Save,"SaveHudLayout");
            inspector=new VisualElement {name="HudControlInspector"}; inspector.AddToClassList("layout-inspector"); Overlay.Add(inspector);
            heading=new Label("Select a control"); heading.AddToClassList("toys-heading"); inspector.Add(heading);
            size=Slider(inspector,"Size %",65,150,"HudControlSize");
            opacity=Slider(inspector,"Opacity %",25,100,"HudControlOpacity");
            size.RegisterValueChangedCallback(evt=>{if(selected!=null){sizes[selected]=Mathf.Clamp(evt.newValue,65,150)/100;Layout();}});
            opacity.RegisterValueChangedCallback(evt=>{if(selected!=null){opacities[selected]=Mathf.Clamp(evt.newValue,25,100)/100;Layout();}});
            Button(inspector,"Hide panel",()=>inspector.AddToClassList("toys-hidden"),"HideHudInspector");
            canvas.RegisterCallback<GeometryChangedEvent>(evt=>{if(IsOpen)Layout();});
            Overlay.RegisterCallback<DetachFromPanelEvent>(evt=>EndDrag());
        }
        void Context(string text,string style){var label=new Label(text){pickingMode=PickingMode.Ignore};label.AddToClassList(style);canvas.Add(label);}
        static void Button(VisualElement parent,string text,Action action,string name){var b=new Button(action){text=text,name=name};b.AddToClassList("toys-button");parent.Add(b);}
        static Slider Slider(VisualElement parent,string text,float min,float max,string name){var s=new Slider(text,min,max){name=name,showInputField=true};s.AddToClassList("settings-slider");parent.Add(s);return s;}
        public void Open()
        {
            foreach(string name in Names){positions[name]=Position(name);sizes[name]=Get(name,"size",1);opacities[name]=Get(name,"opacity",1);}
            selected=null; inspector.AddToClassList("toys-hidden"); Overlay.RemoveFromClassList("toys-hidden"); Overlay.BringToFront(); Layout();
        }
        public void Close(){EndDrag();Overlay.AddToClassList("toys-hidden");}
        void Select(string name){selected=name;heading.text=name;size.SetValueWithoutNotify(sizes[name]*100);opacity.SetValueWithoutNotify(opacities[name]*100);inspector.RemoveFromClassList("toys-hidden");Layout();}
        void EndDrag()
        {
            var c=dragged;int id=pointer;pointer=-1;dragged=null;
            if(c!=null){c.style.translate=new Translate(0,0);if(c.HasPointerCapture(id))c.ReleasePointer(id);}
            if(IsOpen)Layout();
        }
        void Layout()
        {
            Rect safe=SafeRect(canvas.layout.width,canvas.layout.height);
            if(float.IsNaN(safe.width)||safe.width<=0||safe.height<=0)return;
            foreach(string name in Names)
            {
                if(!positions.ContainsKey(name))continue;
                var c=controls[name];Rect r=ControlRect(name,safe,positions[name],sizes[name]);
                c.style.left=r.x;c.style.top=r.y;c.style.width=r.width;c.style.height=r.height;c.style.opacity=opacities[name];
                c.EnableInClassList("layout-selected",selected==name);
            }
        }
        void Reset(){EndDrag();for(int i=0;i<Names.Length;i++){positions[Names[i]]=Defaults[i];sizes[Names[i]]=opacities[Names[i]]=1;}if(selected!=null)Select(selected);else Layout();}
        void Save()
        {
            EndDrag();
            foreach(string name in Names){string p="toys.touch."+name+".";PlayerPrefs.SetFloat(p+"x",positions[name].x);PlayerPrefs.SetFloat(p+"y",positions[name].y);PlayerPrefs.SetFloat(p+"size",sizes[name]);PlayerPrefs.SetFloat(p+"opacity",opacities[name]);}
            PlayerPrefs.Save();Changed?.Invoke();Close();
        }
    }
}
