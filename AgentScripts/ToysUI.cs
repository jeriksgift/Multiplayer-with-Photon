using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

public static class ToysUI
{
    const string Dir="Assets/UI/ToysDeathMatch/";
    static readonly Color Ink=Hex("171B1D"), Paper=Hex("1B2023"), White=Hex("F2F0E8"), Muted=Hex("A1AAA9"), Rule=Hex("41494D"), Gold=Hex("E6B744"), GoldInk=Hex("E6B744"), Blue=Hex("529DCC"), Red=Hex("EE7061");
    static TMP_FontAsset display,body,mono;
    static Color Hex(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
    static T Ensure<T>(Transform t) where T:Component => t.GetComponent<T>()??t.gameObject.AddComponent<T>();
    static Transform Node(Transform parent,string name)
    {
        var t=parent.Find(name);if(!t){t=new GameObject(name,typeof(RectTransform)).transform;t.SetParent(parent,false);}t.gameObject.SetActive(true);return t;
    }
    static void Box(Transform t,Vector2 anchor,Vector2 pivot,float x,float y,float w,float h)
    {
        var r=(RectTransform)t;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
    }
    static void Top(Transform t,float x,float y,float w,float h)=>Box(t,new Vector2(0,1),new Vector2(0,1),x,-y,w,h);
    static void Stretch(Transform t,Vector2 min,Vector2 max,float l=0,float b=0,float r=0,float top=0)
    {
        var rect=(RectTransform)t;rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=new Vector2(l,b);rect.offsetMax=new Vector2(-r,-top);rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
    }
    static void Full(Transform t)=>Stretch(t,Vector2.zero,Vector2.one);
    static UnityEngine.UI.Image Paint(Transform t,Color c,bool hit=false)
    {
        // Neutral light surfaces become graphite; sprite artwork keeps its tint.
        if (c != Color.white && c.r>.65f && c.g>.65f && c.b>.65f)
            c=new Color(.115f,.14f,.155f,c.a);
        var i=Ensure<UnityEngine.UI.Image>(t);i.sprite=null;i.type=UnityEngine.UI.Image.Type.Simple;i.color=c;i.raycastTarget=hit;return i;
    }
    static Transform Rect(Transform p,string n,float x,float y,float w,float h,Color c)
    {
        var t=Node(p,n);Top(t,x,y,w,h);Paint(t,c);return t;
    }
    static TMP_Text Type(Transform t,float size,Color c,TMP_FontAsset font=null,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        if (c==Ink)
        {
            var surface=t.parent?t.parent.GetComponent<UnityEngine.UI.Image>():null;
            if (!surface || surface.color!=Gold) c=White;
        }
        var text=Ensure<TextMeshProUGUI>(t);text.font=font??display;text.fontSize=size;text.enableAutoSizing=false;text.fontStyle=FontStyles.Normal;text.color=c;text.alignment=size>=70&&alignment==TextAlignmentOptions.Left?TextAlignmentOptions.TopLeft:alignment;text.characterSpacing=0;text.lineSpacing=0;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=size>=70?TextOverflowModes.Overflow:TextOverflowModes.Truncate;text.margin=Vector4.zero;text.raycastTarget=false;return text;
    }
    static Transform Text(Transform p,string n,string value,float x,float y,float w,float h,float size,Color c,TMP_FontAsset font=null,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        var t=Node(p,n);Top(t,x,y,w,h);Type(t,size,c,font,alignment).text=value;return t;
    }
    static void Borders(Transform t,Color c,float weight=1)
    {
        var top=Node(t,"FrameTop");Stretch(top,new Vector2(0,1),Vector2.one,0,-weight,0,0);Paint(top,c);
        var bottom=Node(t,"FrameBottom");Stretch(bottom,Vector2.zero,new Vector2(1,0),0,0,0,-weight);Paint(bottom,c);
        var left=Node(t,"FrameLeft");Stretch(left,Vector2.zero,new Vector2(0,1),0,0,-weight,0);Paint(left,c);
        var right=Node(t,"FrameRight");Stretch(right,new Vector2(1,0),Vector2.one,-weight,0,0,0);Paint(right,c);
    }
    static void Action(Transform t,string caption,Color fill,Color text,UnityAction call=null)
    {
        var button=Ensure<UnityEngine.UI.Button>(t);button.targetGraphic=Paint(t,fill,true);var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.08f,1.08f,1.04f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.8f,.8f,.75f);colors.disabledColor=new Color(.7f,.7f,.65f,.55f);colors.fadeDuration=.08f;button.colors=colors;
        var label=t.GetComponentInChildren<TMP_Text>(true);if(!label)label=Text(t,"Label",caption,24,0,500,64,27,text).GetComponent<TMP_Text>();
        Type(label.transform,27,text);label.text=caption;Stretch(label.transform,Vector2.zero,Vector2.one,24,0,24,0);label.alignment=TextAlignmentOptions.Left;
        if(call!=null){while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);UnityEventTools.AddPersistentListener(button.onClick,call);}
    }
    static void Fonts()
    {
        display=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir+"Fonts/Display.asset");body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir+"Fonts/Body.asset");mono=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir+"Fonts/Mono.asset");if(!display||!body||!mono)throw new Exception("Run ToysUI.Prepare first");
    }
    public static string Prepare()
    {
        AssetDatabase.Refresh();
        foreach(var name in new[]{"Display","Body","Mono"})
        {
            string path=Dir+"Fonts/"+name+".asset";var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font){font.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{TMP_Settings.defaultFontAsset};font.TryAddCharacters(string.Concat(Enumerable.Range(32,95).Select(i=>(char)i)));EditorUtility.SetDirty(font);continue;}
            var source=AssetDatabase.LoadAssetAtPath<Font>(Dir+"Fonts/"+name+".ttf");if(!source)throw new Exception("Missing "+name);
            font=TMP_FontAsset.CreateFontAsset(source,90,9,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);font.name="Toys "+name;
            AssetDatabase.CreateAsset(font,path);font.material.name=name+" Material";AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures){texture.name=name+" Atlas";AssetDatabase.AddObjectToAsset(texture,font);}
            font.TryAddCharacters(string.Concat(Enumerable.Range(32,95).Select(i=>(char)i)));font.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{TMP_Settings.defaultFontAsset};EditorUtility.SetDirty(font);
        }
        var veil=new Texture2D(512,8,TextureFormat.RGBA32,false);
        for(int y=0;y<8;y++)for(int x=0;x<512;x++){float u=x/511f;veil.SetPixel(x,y,new Color(.025f,.032f,.035f,Mathf.Lerp(.88f,.60f,Mathf.SmoothStep(0,1,u))));}veil.Apply();File.WriteAllBytes(Dir+"DaylightVeil.png",veil.EncodeToPNG());UnityEngine.Object.DestroyImmediate(veil);AssetDatabase.ImportAsset(Dir+"DaylightVeil.png");var importer=(TextureImporter)AssetImporter.GetAtPath(Dir+"DaylightVeil.png");importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();AssetDatabase.SaveAssets();Fonts();return "Fonts verified and dark menu overlay imported.";
    }
    static void Backdrop(Transform root,float veilAlpha=1)
    {
        Paint(root,Paper);var bg=Node(root,"Atmosphere");Full(bg);Paint(bg,Color.white).sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ModernArena/IndustrialMenu.png");bg.SetAsFirstSibling();
        var veil=Node(root,"DaylightVeil");Full(veil);Paint(veil,Color.white).sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Dir+"DaylightVeil.png");veil.SetSiblingIndex(1);
    }
    static void Scale(Transform t)
    {
        var s=Ensure<UnityEngine.UI.CanvasScaler>(t);s.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
    }
    static Transform Header(Transform root,string section)
    {
        var h=Node(root,"ToysHeader");Stretch(h,new Vector2(0,1),Vector2.one,0,-132,0,0);Paint(h,new Color(.97f,.96f,.92f,.8f));
        var icon=Rect(h,"Brick",64,45,44,36,Gold);Rect(icon,"StudA",6,-5,10,5,Gold);Rect(icon,"StudB",27,-5,10,5,Gold);Rect(icon,"Inset",9,12,26,7,Ink);
        Text(h,"Title","TOYS",124,31,170,42,33,Ink);var name=Text(h,"Subtitle","DEATH MATCH",125,77,190,20,12,Ink,mono);name.GetComponent<TMP_Text>().characterSpacing=1;
        Rect(h,"Divider",324,42,1,54,Rule);Text(h,"Section",section,360,48,900,42,15,Ink,mono);
        var network=Text(h,"Network","CONNECTING",0,0,400,30,14,Ink,mono,TextAlignmentOptions.Right);Box(network,new Vector2(1,1),new Vector2(1,1),-64,-53,400,30);return h;
    }
    static void InputField(Transform t,string placeholder)
    {
        var input=t.GetComponent<TMP_InputField>();input.targetGraphic=Paint(t,White,true);input.caretColor=GoldInk;input.selectionColor=new Color(.9f,.7f,.2f,.3f);input.characterLimit=t.name=="NickNameInput"?16:24;
        var area=t.Find("Text Area");Stretch(area,Vector2.zero,Vector2.one,20,8,20,8);Full(input.textComponent.transform);Type(input.textComponent.transform,24,Ink,body);
        if(input.placeholder){Full(input.placeholder.transform);Type(input.placeholder.transform,23,Muted,body).text=placeholder;}
        Borders(t,Rule);
        var obsolete=t.Find("FieldLine");if(obsolete)obsolete.gameObject.SetActive(false);
    }
    static void Bind(UnityEngine.Object target,string field,UnityEngine.Object value)
    {
        var so=new SerializedObject(target);var p=so.FindProperty(field);if(p==null)throw new Exception("Missing field "+field);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void PersistOverrides(Transform root)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(PrefabUtility.IsPartOfPrefabInstance(t.gameObject))PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            foreach(var c in t.GetComponents<Component>())if(c&&PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
    }
    static Transform Confirmation(Transform parent,ToysGameUI ui,bool match)
    {
        var overlay=Node(parent,"ExitConfirmation");Full(overlay);Paint(overlay,new Color(.08f,.10f,.09f,.56f),true);
        var panel=Node(overlay,"Dialog");Box(panel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,0,840,450);Paint(panel,Paper,true);Borders(panel,Hex("85897D"));Rect(panel,"DangerLine",0,0,840,4,Red);
        Text(panel,"Warning","!  DISCONNECT REQUEST",48,38,700,28,14,Red,mono);
        Text(panel,"Title",match?"LEAVE THIS MATCH?":"QUIT TO DESKTOP?",48,94,760,66,47,Ink);
        var description=Text(panel,"Description",match?"You will leave the current match and return to the lobby.\nYour team will continue without you.":"Close Toys Death Match and return to your desktop?",48,175,730,68,23,Muted,body);description.GetComponent<TMP_Text>().textWrappingMode=TextWrappingModes.Normal;
        var info=Rect(panel,"MatchInfo",48,264,744,46,Hex("E3E3D8"));ui.exitRoomText=Text(info,"Room","TOYS DEATH MATCH",18,0,704,46,14,Ink,mono).GetComponent<TextMeshProUGUI>();
        var cancel=Node(panel,"Cancel");Top(cancel,48,342,360,64);Action(cancel,"CANCEL",White,Ink,ui.CancelExit);Borders(cancel,Rule);
        var confirm=Node(panel,"Confirm");Top(confirm,428,342,364,64);Action(confirm,"CONFIRM EXIT",Red,White,ui.ConfirmExit);
        if(match){var note=Text(overlay,"LiveNotice","LIVE MATCH / GAMEPLAY CONTINUES WHILE THIS MENU IS OPEN",0,0,1400,28,13,White,mono,TextAlignmentOptions.Center);Box(note,new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,-290,1400,28);}
        ui.exitOverlay=overlay.gameObject;overlay.gameObject.SetActive(false);return overlay;
    }
    public static string Lobby()
    {
        Fonts();if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first");var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Lobby.unity")scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
        var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;Scale(canvas);var menu=canvas.Find("Menu");var lobby=UnityEngine.Object.FindAnyObjectByType<LobbyManager>();var ui=Ensure<ToysGameUI>(canvas);ui.matchInterface=false;
        var nickname=menu.GetComponentsInChildren<TMP_InputField>(true).Single(x=>x.name=="NickNameInput").transform;var room=menu.GetComponentsInChildren<TMP_InputField>(true).Single(x=>x.name=="RoomNameInput").transform;
        var enter=menu.Find("CreateJoinRoomBtn")??menu.Find("DeploymentTerminal/CreateJoinRoomBtn");var quit=menu.Find("ExitBtn")??menu.Find("DeploymentTerminal/ExitBtn");
        foreach(Transform child in menu)if(child!=nickname&&child!=room&&child!=enter&&child!=quit)child.gameObject.SetActive(false);
        foreach(var layout in menu.GetComponents<UnityEngine.UI.LayoutGroup>())layout.enabled=false;Backdrop(menu);var header=Header(menu,"MULTIPLAYER / LOBBY");ui.networkText=header.Find("Network").GetComponent<TextMeshProUGUI>();
        Text(menu,"GameTitle","TOYS",104,294,920,130,120,Ink);Text(menu,"GameTitleSecond","DEATH MATCH",104,414,920,134,112,Ink);
        Rect(menu,"TitleAccent",110,574,80,5,GoldInk);Text(menu,"GameMode","4 VS 4 / TEAM DEATHMATCH",110,614,900,35,18,Ink,mono);
        var rotation=Rect(menu,"MapBrief",110,817,710,114,new Color(.97f,.96f,.92f,.83f));Rect(rotation,"Line",0,0,4,114,GoldInk);Text(rotation,"Caption","CURRENT MAP",25,16,650,23,13,Muted,mono);Text(rotation,"Map","INDUSTRIAL YARD",25,48,650,37,29,Ink);Text(rotation,"Rules","FIRST TO 10 KILLS / 20 MINUTES",25,88,650,19,12,Muted,mono);
        var terminal=Node(menu,"DeploymentTerminal");Box(terminal,new Vector2(1,.5f),new Vector2(1,.5f),-88,-10,704,738);Paint(terminal,new Color(.95f,.94f,.9f,.97f),true);Borders(terminal,Hex("A7AA9E"));Rect(terminal,"Accent",0,0,704,4,GoldInk);
        Text(terminal,"Step","01 / DEPLOYMENT TERMINAL",40,34,620,24,14,GoldInk,mono);Text(terminal,"Heading","ENTER THE ARENA",40,78,620,65,46,Ink);Text(terminal,"Help","Set your name. Create or join a room.",40,153,620,34,21,Muted,body);
        nickname.SetParent(terminal,false);nickname.gameObject.SetActive(true);Top(nickname,40,238,624,64);InputField(nickname,"Player name");Text(terminal,"NameCaption","PLAYER NAME",40,207,620,22,13,Muted,mono);
        room.SetParent(terminal,false);room.gameObject.SetActive(true);Top(room,40,365,624,64);InputField(room,"Room name (required)");Text(terminal,"RoomCaption","ROOM NAME / REQUIRED",40,334,620,22,13,Muted,mono);
        var status=Text(terminal,"LobbyStatus","",40,442,624,30,16,Red,body);Bind(lobby,"lobbyStatusText",status.GetComponent<TMP_Text>());
        enter.SetParent(terminal,false);enter.gameObject.SetActive(true);Top(enter,40,486,624,64);Action(enter,"CREATE ROOM",Gold,Ink,lobby.CreateRoomBtn);Text(enter,"ActionGlyph","+",566,0,32,64,25,Ink,mono,TextAlignmentOptions.Center);
        var join=Node(terminal,"JoinRoomBtn");Top(join,40,566,624,64);Action(join,"JOIN ROOM",Ink,White,lobby.JoinRoomBtn);Text(join,"ActionGlyph",">",566,0,32,64,23,White,mono,TextAlignmentOptions.Center);
        Rect(terminal,"QuitRule",40,654,624,1,Rule);quit.SetParent(terminal,false);quit.gameObject.SetActive(true);Top(quit,40,667,624,50);Action(quit,"QUIT GAME",Color.clear,Muted,ui.ShowExit);Type(quit.GetComponentInChildren<TMP_Text>().transform,22,Muted);Text(quit,"EscapeKey","ESC",540,0,60,50,13,Muted,mono,TextAlignmentOptions.Right);
        var footer=Text(menu,"GameFooter","TOYS DEATH MATCH / MULTIPLAYER",0,0,1100,25,12,Ink,mono);Box(footer,new Vector2(0,0),Vector2.zero,64,28,1100,25);
        Room(canvas,ui,lobby);Confirmation(canvas,ui,false);EditorUtility.SetDirty(ui);PersistOverrides(canvas);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Reference lobby and team selection applied with bright arena, condensed typography and separate create/join actions.";
    }
    static void Room(Transform canvas,ToysGameUI ui,LobbyManager lobby)
    {
        var room=canvas.Find("Room");foreach(Transform child in room)if(!new[]{"GridLayout","ReadyBtn","StartBtn","ExitBtn","RoomNameText"}.Contains(child.name))child.gameObject.SetActive(false);Backdrop(room,.75f);var header=Header(room,"MULTIPLAYER / TEAM SELECTION");ui.roomCountText=header.Find("Network").GetComponent<TextMeshProUGUI>();
        Text(room,"RoomStep","02 / SQUAD ASSIGNMENT",112,171,1050,27,14,GoldInk,mono);Text(room,"SelectionTitle","CHOOSE YOUR SIDE",112,214,1100,75,59,Ink);Text(room,"SelectionHelp","Select an open slot. Ready up when your squad is set.",112,300,1150,30,22,Muted,body);
        var roomName=room.Find("RoomNameText");Box(roomName,new Vector2(1,1),new Vector2(1,1),-112,-215,700,58);Type(roomName,38,Ink,display,TextAlignmentOptions.Right);var caption=Text(room,"RoomCaption","ROOM NAME",0,0,600,24,13,Muted,mono,TextAlignmentOptions.Right);Box(caption,new Vector2(1,1),new Vector2(1,1),-112,-178,600,24);
        for(int side=0;side<2;side++)
        {
            var h=Node(room,side==0?"BlueHeading":"RedHeading");Stretch(h,new Vector2(side==0?.05833f:.5625f,.603f),new Vector2(side==0?.4375f:.94167f,.68f));
            Text(h,"Team",side==0?"BLUE TEAM":"RED TEAM",0,0,560,58,44,side==0?Blue:Red);Text(h,"Squad",side==0?"SQUAD ALPHA":"SQUAD BRAVO",0,58,560,22,12,Muted,mono);
            var count=Text(h,"Count","0 / 4",0,0,160,58,32,Ink,display,TextAlignmentOptions.Right);Box(count,new Vector2(1,1),new Vector2(1,1),0,0,160,58);if(side==0)ui.blueCountText=count.GetComponent<TextMeshProUGUI>();else ui.redCountText=count.GetComponent<TextMeshProUGUI>();
            var line=Node(h,"Line");Stretch(line,Vector2.zero,new Vector2(1,0),0,0,0,-2);Paint(line,side==0?Blue:Red);
        }
        var grid=room.Find("GridLayout");Stretch(grid,new Vector2(.05833f,.245f),new Vector2(.94167f,.58f));Paint(grid,Color.clear);var layout=grid.GetComponent<UnityEngine.UI.GridLayoutGroup>();layout.spacing=new Vector2(240,12);layout.padding=new RectOffset();layout.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;layout.enabled=true;grid.GetComponent<GridChildCellSizeSetter>().enabled=true;
        foreach(var slot in grid.GetComponentsInChildren<RoomSlot>(true))
        {
            var t=slot.transform;foreach(Transform child in t)if(child.name!="PlayerName")child.gameObject.SetActive(false);var name=t.Find("PlayerName");Stretch(name,new Vector2(0,.42f),new Vector2(1,1),86,0,180,0);Type(name,29,Ink);var status=Node(t,"Readiness");Stretch(status,new Vector2(0,0),new Vector2(1,.42f),86,10,20,0);Type(status,13,Muted,mono).text="CLICK TO JOIN";
            var badge=Node(t,"Badge");Box(badge,new Vector2(0,.5f),new Vector2(0,.5f),22,0,42,42);Paint(badge,new Color(.85f,.86f,.81f,.6f));Borders(badge,slot.Team==DataTypes.Team.Blue?Blue:Red);var symbol=Text(badge,"Symbol","+",0,0,42,42,27,slot.Team==DataTypes.Team.Blue?Blue:Red,mono,TextAlignmentOptions.Center);
            Paint(t,White,true);Borders(t,Hex("B8BEB1"));var colors=t.GetComponent<UnityEngine.UI.Button>().colors;colors.normalColor=Color.white;colors.highlightedColor=Hex("FFF4D2");colors.selectedColor=Color.white;colors.pressedColor=Hex("E3D8B9");t.GetComponent<UnityEngine.UI.Button>().colors=colors;
            var so=new SerializedObject(slot);so.FindProperty("statusText").objectReferenceValue=status.GetComponent<TMP_Text>();so.FindProperty("occupiedTextColor").colorValue=White;so.FindProperty("emptyTextColor").colorValue=Muted;so.FindProperty("emptyColor").colorValue=Hex("22282C");so.FindProperty("blueColor").colorValue=Hex("203440");so.FindProperty("redColor").colorValue=Hex("3D292A");so.FindProperty("readyColor").colorValue=Hex("28392E");so.ApplyModifiedPropertiesWithoutUndo();
        }
        var versus=Text(room,"Versus","VS",0,0,150,80,47,Muted,display,TextAlignmentOptions.Center);Box(versus,new Vector2(.5f,.43f),new Vector2(.5f,.5f),0,0,150,80);
        var ready=room.Find("ReadyBtn");Stretch(ready,new Vector2(.05833f,.152f),new Vector2(.4375f,.212f));Action(ready,"READY UP",Gold,Ink);var deploy=room.Find("StartBtn");Stretch(deploy,new Vector2(.5625f,.152f),new Vector2(.94167f,.212f));Action(deploy,"DEPLOY",Ink,White);
        var leave=room.Find("ExitBtn");Box(leave,new Vector2(0,0),Vector2.zero,112,80,350,45);Action(leave,"LEAVE ROOM",Color.clear,Ink,lobby.LeaveRoomBtn);Type(leave.GetComponentInChildren<TMP_Text>().transform,16,Ink,mono);
        var rules=Text(room,"Rules","10 KILLS / 20:00 TIME LIMIT",0,0,600,30,13,Ink,mono,TextAlignmentOptions.Right);Box(rules,new Vector2(1,0),new Vector2(1,0),-112,88,600,30);
        ui.teamSelectionPanel=room.gameObject;ui.lobbyManager=lobby;
    }
    public static string Loading()
    {
        Fonts();var scene=SceneManager.GetActiveScene();var manager=UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);if(!manager)throw new Exception("Open Lobby first");
        var so=new SerializedObject(manager);var panel=((RectTransform)so.FindProperty("loadingPanel").objectReferenceValue).transform;var status=((TMP_Text)so.FindProperty("loadingText").objectReferenceValue).transform;
        foreach(Transform child in panel)child.gameObject.SetActive(false);Backdrop(panel,.5f);var header=Header(panel,"MULTIPLAYER / MATCH CONNECTION");header.Find("Network").gameObject.SetActive(false);
        Text(panel,"TitleFirst","TOYS",112,283,1100,130,110,Ink);Text(panel,"TitleSecond","DEATH MATCH",112,400,1100,130,110,Ink);Rect(panel,"TitleLine",112,563,82,5,GoldInk);Text(panel,"MatchMode","4 VS 4 / TEAM DEATHMATCH",112,608,1000,36,18,Ink,mono);
        var map=Node(panel,"MapInfo");Box(map,new Vector2(1,.5f),new Vector2(1,.5f),-112,-80,525,150);Paint(map,new Color(.96f,.95f,.91f,.9f));Rect(map,"Accent",0,0,3,150,GoldInk);Text(map,"MapCaption","COMBAT ZONE",25,18,480,26,14,GoldInk,mono);Text(map,"MapName","INDUSTRIAL YARD",25,55,480,50,41,Ink);Text(map,"MapType","COVER / LANES / CLOSE QUARTERS",25,112,480,24,13,Muted,mono);
        var phase=Text(panel,"Phase","PREPARING YOUR MATCH",0,0,1100,28,14,GoldInk,mono);Box(phase,new Vector2(0,0),Vector2.zero,112,225,1100,28);
        status.SetParent(panel,false);status.gameObject.SetActive(true);Box(status,new Vector2(0,0),Vector2.zero,112,183,1200,34);Type(status,22,Ink,body);
        var bar=Node(panel,"ProgressTrack");Stretch(bar,new Vector2(.05833f,.15f),new Vector2(.94167f,.15f),0,0,0,-5);Paint(bar,new Color(.15f,.18f,.16f,.2f));var fill=Node(bar,"ProgressFill");Stretch(fill,Vector2.zero,new Vector2(.22f,1));Paint(fill,GoldInk);so.FindProperty("progressFill").objectReferenceValue=fill.GetComponent<UnityEngine.UI.Image>();
        var value=Text(panel,"ProgressValue","PLEASE WAIT",0,0,400,45,24,Ink,mono,TextAlignmentOptions.Right);Box(value,new Vector2(1,0),new Vector2(1,0),-112,191,400,45);so.FindProperty("progressText").objectReferenceValue=value.GetComponent<TMP_Text>();so.ApplyModifiedPropertiesWithoutUndo();
        var tip=Text(panel,"Tip","FIELD NOTE / Keep moving. Use cover between engagements.",0,0,1500,30,18,Ink,body);Box(tip,new Vector2(0,0),Vector2.zero,112,110,1500,30);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Loading layout follows reference; real scene progress or indeterminate connection indicator, bright map artwork and corrected game title.";
    }
    static void StatRow(Transform row,bool heading)
    {
        var name=row.Find("Name");var kills=row.Find("Kills");var damage=row.Find("Kills (1)");
        if(name){Stretch(name,new Vector2(0,0),new Vector2(.57f,1),22,0,0,0);Type(name,heading?14:27,heading?Muted:Ink,heading?mono:display);if(heading)name.GetComponent<TMP_Text>().text="PLAYER";}
        if(kills){Stretch(kills,new Vector2(.57f,0),new Vector2(.73f,1));Type(kills,heading?14:30,heading?Muted:Ink,heading?mono:display,TextAlignmentOptions.Center);if(heading)kills.GetComponent<TMP_Text>().text="KILLS";}
        if(damage){Stretch(damage,new Vector2(.73f,0),Vector2.one,0,0,22,0);Type(damage,heading?14:28,heading?Muted:Ink,heading?mono:display,TextAlignmentOptions.Right);if(heading)damage.GetComponent<TMP_Text>().text="DAMAGE";}
    }
    public static string FinalizeAssets()
    {
        PlayerSettings.productName="Toys Death Match";
        foreach(var name in new[]{"PlayerWithGFX","PlayerWithGFX_Red"})
        {
            var root=Resources.Load<GameObject>(name);if(!root.GetComponent<ToysGameUI>())throw new Exception("Missing interface on "+name);
            var ui=root.GetComponent<ToysGameUI>();if(!ui.exitOverlay||!ui.healthValueText||!ui.playerNameText||!ui.targetScoreText)throw new Exception("Incomplete interface wiring on "+name);
            if(ui.exitOverlay.activeSelf)throw new Exception("Exit dialog must start hidden");
            var so=new SerializedObject(root.GetComponent<PlayerUI>());foreach(var field in new[]{"healthBarFill","blueScoreText","redScoreText","timerText","matchEndPanel","exitToLobbyButton"})if(!so.FindProperty(field).objectReferenceValue)throw new Exception("Missing "+field+" on "+name);
        }
        AssetDatabase.SaveAssets();return "Product name set to Toys Death Match; both variants have complete HUD and hidden confirmation-dialog wiring.";
    }
    public static string HUD()
    {
        Fonts();if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first");const string path="Assets/Resources/PlayerWithGFX.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var canvas=root.transform.Find("Canvas");Scale(canvas);var hud=canvas.Find("PlayerHUD");var timer=hud.Find("TimerBG")??hud.Find("KillCount/TimerBG");timer.SetParent(hud,false);
            foreach(Transform child in hud)if(!new[]{"RoomName","KillCount","HealthBarBG","TimerBG","Image","MatchEndPanel"}.Contains(child.name))child.gameObject.SetActive(false);
            var ui=Ensure<ToysGameUI>(root.transform);ui.matchInterface=true;
            var exit=Node(hud,"ExitMatchButton");Top(exit,64,48,228,54);Action(exit,"EXIT GAME",Paper,Ink,ui.ShowExit);Borders(exit,Rule);Stretch(exit.GetComponentInChildren<TMP_Text>().transform,Vector2.zero,Vector2.one,71,0,12,0);Type(exit.GetComponentInChildren<TMP_Text>().transform,23,Ink);Text(exit,"EscapeKey","ESC",17,0,45,54,13,Muted,mono);Rect(exit,"Divider",58,15,1,24,Rule);
            var map=Text(hud,"ArenaName","INDUSTRIAL YARD",64,126,480,37,27,White);var mapShadow=Ensure<UnityEngine.UI.Shadow>(map);mapShadow.effectColor=new Color(.05f,.07f,.06f,.85f);mapShadow.effectDistance=new Vector2(1,-1);
            var room=hud.Find("RoomName");Top(room,64,168,600,26);Type(room,15,White,mono);var shadow=Ensure<UnityEngine.UI.Shadow>(room);shadow.effectColor=new Color(.05f,.07f,.06f,.85f);shadow.effectDistance=new Vector2(1,-1);
            var score=hud.Find("KillCount");foreach(var layout in score.GetComponents<UnityEngine.UI.LayoutGroup>())layout.enabled=false;Box(score,new Vector2(.5f,1),new Vector2(.5f,1),0,-40,548,88);Paint(score,Color.clear);
            var left=score.Find("Bg");var right=score.Find("Bg (1)");Top(left,0,0,219,88);Top(right,329,0,219,88);
            for(int side=0;side<2;side++)
            {
                var team=side==0?left:right;foreach(Transform child in team)if(child.name!=(side==0?"BlueScore":"RedScore"))child.gameObject.SetActive(false);Paint(team,new Color(.97f,.96f,.92f,.96f));Rect(team,"TeamStripe",0,0,219,3,side==0?Blue:Red);Text(team,"TeamTitle",side==0?"BLUE TEAM":"RED TEAM",21,17,140,29,23,side==0?Blue:Red);Text(team,"KillsCaption","KILLS",21,51,120,20,11,Muted,mono);
                var number=team.Find(side==0?"BlueScore":"RedScore");Top(number,143,8,62,72);Type(number,50,Ink,display,TextAlignmentOptions.Center);
            }
            var target=Node(score,"Target");Top(target,223,0,102,88);Paint(target,Ink);ui.targetScoreText=Text(target,"TargetScore",GameManager.WIN_SCORE.ToString(),0,9,102,43,32,White,display,TextAlignmentOptions.Center).GetComponent<TextMeshProUGUI>();Text(target,"TargetCaption","TO WIN",0,52,102,22,11,Hex("BEBFB3"),mono,TextAlignmentOptions.Center);
            var caption=Node(hud,"ScoreMode");Box(caption,new Vector2(.5f,1),new Vector2(.5f,1),0,-142,720,25);Type(caption,13,White,mono,TextAlignmentOptions.Center).text="TEAM DEATHMATCH";var capShadow=Ensure<UnityEngine.UI.Shadow>(caption);capShadow.effectColor=new Color(0,0,0,.8f);capShadow.effectDistance=new Vector2(1,-1);
            Box(timer,new Vector2(1,1),new Vector2(1,1),-64,-48,216,76);Paint(timer,Paper);Borders(timer,Rule);var time=timer.Find("Text (TMP)");Top(time,30,25,166,48);Type(time,39,Ink,display,TextAlignmentOptions.Center);Text(timer,"TimerLabel","TIME REMAINING",19,9,180,17,11,Muted,mono,TextAlignmentOptions.Center);
            var cross=hud.Find("Image");Box(cross,new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,0,3,3);Paint(cross,Color.white);var crossShadow=Ensure<UnityEngine.UI.Shadow>(cross);crossShadow.effectColor=new Color(0,0,0,.75f);crossShadow.effectDistance=new Vector2(1,-1);
            var health=Node(hud,"HealthCard");Box(health,Vector2.zero,Vector2.zero,64,60,440,104);Paint(health,Paper);Borders(health,Rule);Text(health,"Plus","+",19,17,26,38,27,GoldInk,mono,TextAlignmentOptions.Center);
            ui.playerNameText=Text(health,"Player","PLAYER",57,15,260,34,26,Ink).GetComponent<TextMeshProUGUI>();Text(health,"HealthLabel","HEALTH",58,52,240,18,11,Muted,mono);ui.healthValueText=Text(health,"HealthValue","100",313,9,59,52,40,Ink,display,TextAlignmentOptions.Right).GetComponent<TextMeshProUGUI>();Text(health,"Maximum","/ 100",375,28,53,27,22,Muted);
            var healthBG=hud.Find("HealthBarBG")??health.Find("HealthBarBG");healthBG.SetParent(health,false);Top(healthBG,20,79,400,6);Paint(healthBG,Hex("D7D9CD"));var fill=healthBG.Find("HealthBarFill");Full(fill);var fillImage=Paint(fill,GoldInk);fillImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ModernArena/Panel.png");fillImage.type=UnityEngine.UI.Image.Type.Filled;fillImage.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fillImage.fillOrigin=0;fillImage.fillAmount=1;
            var weapon=Node(hud,"WeaponReadout");Box(weapon,new Vector2(1,0),new Vector2(1,0),-64,60,304,104);Paint(weapon,Ink);Text(weapon,"Rifle","ASSAULT RIFLE",22,18,260,36,28,White);Text(weapon,"Type","PRIMARY WEAPON",22,61,260,22,13,Hex("BEBFB3"),mono);
            var brand=Text(hud,"GameName","TOYS DEATH MATCH",0,0,650,22,12,White,mono,TextAlignmentOptions.Right);Box(brand,new Vector2(1,0),new Vector2(1,0),-64,22,650,22);var brandShadow=Ensure<UnityEngine.UI.Shadow>(brand);brandShadow.effectColor=new Color(0,0,0,.7f);brandShadow.effectDistance=new Vector2(1,-1);
            var results=hud.Find("MatchEndPanel");Full(results);Backdrop(results,.8f);foreach(Transform child in results)if(!new[]{"Atmosphere","DaylightVeil","WinnerText","BlueTeamPanel","RedTeamPanel","ExitToLobbyButton"}.Contains(child.name))child.gameObject.SetActive(false);
            Text(results,"ResultPhase","MATCH COMPLETE",0,0,1600,30,16,GoldInk,mono,TextAlignmentOptions.Center);Box(results.Find("ResultPhase"),new Vector2(.5f,1),new Vector2(.5f,1),0,-77,1600,30);
            var winner=results.Find("WinnerText");Box(winner,new Vector2(.5f,1),new Vector2(.5f,1),0,-130,1680,85);Type(winner,69,Ink,display,TextAlignmentOptions.Center).fontStyle=FontStyles.UpperCase;
            for(int side=0;side<2;side++)
            {
                var team=results.Find(side==0?"BlueTeamPanel":"RedTeamPanel");Stretch(team,new Vector2(side==0?.0583f:.515f,.22f),new Vector2(side==0?.485f:.9417f,.69f));Paint(team,Paper);Borders(team,Rule);var title=team.Find("TeamTitle");Top(title,24,17,500,44);Type(title,33,side==0?Blue:Red).text=side==0?"BLUE TEAM":"RED TEAM";var accent=team.Find("Accent");Top(accent,0,0,3,68);Paint(accent,side==0?Blue:Red);
                var total=team.Find("EndScoreText");Box(total,new Vector2(1,1),new Vector2(1,1),-24,-20,200,42);Type(total,29,Ink,display,TextAlignmentOptions.Right);var heading=team.Find(side==0?"TableTitle":"TableTitle (1)");Paint(heading,Hex("E3E3D8"));StatRow(heading,true);
                var players=team.Find("Players");Paint(players,Color.clear);int i=0;foreach(Transform row in players){Paint(row,i++%2==0?White:Hex("E9E9DF"));StatRow(row,false);}
            }
            var back=results.Find("ExitToLobbyButton");Box(back,new Vector2(.5f,0),new Vector2(.5f,0),0,96,420,70);Action(back,"RETURN TO LOBBY",Gold,Ink);results.SetAsLastSibling();Confirmation(hud,ui,true);
            var world=root.transform.Find("WorldNameCanvas/NameText");Type(world,27,White,display,TextAlignmentOptions.Center);
            foreach(var c in root.GetComponentsInChildren<Component>(true))if(c&&PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);EditorUtility.SetDirty(ui);PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        return "Reference HUD, live numeric health, score target, timer, match results and non-pausing exit dialog saved to shared player prefab.";
    }
}
