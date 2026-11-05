using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
public static class VerifyHudLayout
{
    public static async Task<string> Preview()
    {
        var root=GameObject.Find("HUD Layout Verification").GetComponent<UIDocument>().rootVisualElement;
        var c=root.Q("LayoutJoystick");
        using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=c.worldBound.center}))c.SendEvent(e);
        using(var e=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=c.worldBound.center}))c.SendEvent(e);
        await Task.Delay(100);
        ScreenCapture.CaptureScreenshot("AgentScripts/Verification/HudLayoutSelected.png");
        return "Selected joystick inspector captured.";
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Click(Button b){typeof(Clickable).GetMethod("SimulateSingleClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(b.clickable,new object[]{null,0});}
    public static async Task<string> Run()
    {
        var go=new GameObject("HUD Layout Verification");
        var doc=go.AddComponent<UIDocument>();
        doc.panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/MyUIToolkits/GameHudPanelSettings.asset");
        doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/MyUIToolkits/Settings/settings.uxml");
        await Task.Delay(300);
        var root=doc.rootVisualElement;
        ControlSettingsUI.Build(root.Q("ControlsPage"),root);
        ControlSettingsUI.OpenEditor(root);
        await Task.Delay(300);
        var c=root.Q("LayoutJoystick"); var canvas=root.Q("HudLayoutCanvas");
        Check(c.worldBound.width>0 && canvas.worldBound.width>500,"Editor did not fill screen");
        string key="toys.touch.Joystick.size"; bool existed=PlayerPrefs.HasKey(key); float old=PlayerPrefs.GetFloat(key,1);
        var backups=new Dictionary<string,float>(); var absent=new List<string>();
        foreach(string name in ControlSettingsUI.Names)foreach(string prop in new[]{"x","y","size","opacity"}){
            string k="toys.touch."+name+"."+prop;if(PlayerPrefs.HasKey(k))backups[k]=PlayerPrefs.GetFloat(k);else absent.Add(k);
        }
        try{
            Vector2 start=c.worldBound.center;
            using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=start}))c.SendEvent(e);
            await Task.Delay(80);
            Check(!root.Q("HudControlInspector").ClassListContains("toys-hidden"),"Selection did not show inspector");
            using(var e=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseDrag,button=0,mousePosition=start+new Vector2(180,-100)}))c.SendEvent(e);
            using(var e=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=start+new Vector2(180,-100)}))c.SendEvent(e);
            root.Q<Slider>("HudControlSize").value=120;
            await Task.Delay(100);
            Check(Mathf.Abs(c.worldBound.center.x-start.x)>100,"Drag did not move joystick");
            Check(PlayerPrefs.GetFloat(key,1)==old,"Draft changed saved settings");
            Click(root.Q<Button>("SaveHudLayout"));
            Check(Mathf.Abs(ControlSettingsUI.Get("Joystick","size",1)-1.2f)<.001f,"Save failed");
            ControlSettingsUI.OpenEditor(root);await Task.Delay(80);
            using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=c.worldBound.center}))c.SendEvent(e);
            using(var e=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=c.worldBound.center}))c.SendEvent(e);
            root.Q<Slider>("HudControlSize").value=70;
            Click(root.Q<Button>("CancelHudLayout"));
            Check(Mathf.Abs(ControlSettingsUI.Get("Joystick","size",1)-1.2f)<.001f,"Cancel changed saved layout");
            ControlSettingsUI.OpenEditor(root);await Task.Delay(100);
            ScreenCapture.CaptureScreenshot("AgentScripts/Verification/HudLayoutEditor.png");
            return "PASS: full-screen HUD, pointer drag, selection, draft isolation, save, reopen and cancel.";
        } finally {
            foreach(var pair in backups)PlayerPrefs.SetFloat(pair.Key,pair.Value);foreach(string k in absent)PlayerPrefs.DeleteKey(k);PlayerPrefs.Save();
        }
    }
}
