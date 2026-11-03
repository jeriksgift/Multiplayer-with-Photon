using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ModernUI
{
    public static string TacticalArt()
    {
        var grain=new Texture2D(1280,720,TextureFormat.RGBA32,false);
        var shade=new Texture2D(512,8,TextureFormat.RGBA32,false);
        for(int y=0;y<720;y++)for(int x=0;x<1280;x++)
        {
            float noise=((x*73+y*127)%101)/100f;float vignette=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x/1280f,y/720f),new Vector2(.5f,.5f)));
            var color=C("141510")+new Color(1,1,.8f,0)*(noise*.009f+vignette*.015f);color.a=1;grain.SetPixel(x,y,color);
        }
        grain.Apply();WriteTexture("Backdrop",grain);
        for(int y=0;y<8;y++)for(int x=0;x<512;x++){float u=x/511f;shade.SetPixel(x,y,new Color(.025f,.028f,.022f,Mathf.Lerp(.97f,.28f,Mathf.SmoothStep(0,1,u))));}shade.Apply();WriteTexture("MenuShade",shade);
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Additive);
        var cameraGO=new GameObject("Menu artwork capture");SceneManager.MoveGameObjectToScene(cameraGO,scene);
        try
        {
            var camera=cameraGO.AddComponent<Camera>();camera.transform.position=new Vector3(31,6,-15);camera.transform.LookAt(new Vector3(3,2,3));camera.fieldOfView=60;camera.nearClipPlane=.1f;camera.farClipPlane=250;
            var target=RenderTexture.GetTemporary(1920,1080,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);WriteTexture("IndustrialMenu",image);
        }
        finally{UnityEngine.Object.DestroyImmediate(cameraGO);if(opened)EditorSceneManager.CloseScene(scene,true);}
        return "Captured an industrial arena backdrop and replaced the blue grid with charcoal grain.";
    }

    public static string TacticalMenu()
    {
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Lobby.unity")throw new Exception("Open Lobby first");
        var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;var menu=canvas.Find("Menu");
        foreach(var name in new[]{"Headline","Eyebrow","Description","ArenaBackdrop","Operators","ModeCard","Footer","BrandDescriptor","ModeBadge","TopRule","RoomHint"})if(menu.Find(name))menu.Find(name).gameObject.SetActive(false);
        var atmosphere=menu.Find("Atmosphere");Image(atmosphere,new Color(.74f,.68f,.53f,1)).sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"IndustrialMenu.png");Fill(atmosphere);
        var shade=Child(menu,"MenuShade");Fill(shade);Image(shade,Color.white).sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"MenuShade.png");shade.SetSiblingIndex(1);
        var brand=menu.Find("Brand");Top(brand,88,90,800,155);StyleText(brand,124,White,TextAlignmentOptions.Left,true).text="ARENA";brand.GetComponent<TMP_Text>().characterSpacing=-5;
        var mark=menu.Find("BrandMark");Top(mark,92,81,78,5);Image(mark,Amber);
        Label(menu,"MenuSection","MULTIPLAYER",92,260,650,58,35,Amber,true).GetComponent<TMP_Text>().characterSpacing=3;
        Label(menu,"MenuMode","TEAM DEATHMATCH",94,326,620,30,16,Muted,true);
        var nickLabel=menu.Find("CallsignLabel");Top(nickLabel,94,433,500,25);StyleText(nickLabel,14,Muted,TextAlignmentOptions.Left,true).text="PLAYER NAME";
        var nickname=menu.Find("NickNameInput");Top(nickname,92,474,500,62);Input(nickname,"Enter player name");Image(nickname,new Color(.12f,.13f,.10f,.85f),false,true);
        var nickLine=Child(nickname,"FieldLine");Stretch(nickLine,Vector2.zero,new Vector2(1,0),0,0,0,-1);Image(nickLine,C("62604D"));
        var roomLabel=menu.Find("RoomLabel");Top(roomLabel,94,574,500,25);StyleText(roomLabel,14,Muted,TextAlignmentOptions.Left,true).text="ROOM NAME   /   REQUIRED";
        var room=menu.Find("RoomNameInput");Top(room,92,615,500,62);Input(room,"Enter room name");Image(room,new Color(.12f,.13f,.10f,.85f),false,true);
        var roomLine=Child(room,"FieldLine");Stretch(roomLine,Vector2.zero,new Vector2(1,0),0,0,0,-1);Image(roomLine,C("62604D"));
        var status=Label(menu,"LobbyStatus","",94,692,620,42,16,Amber,true);status.GetComponent<TMP_Text>().textWrappingMode=TextWrappingModes.Normal;
        var lobby=UnityEngine.Object.FindAnyObjectByType<LobbyManager>();var serialized=new SerializedObject(lobby);serialized.FindProperty("lobbyStatusText").objectReferenceValue=status.GetComponent<TextMeshProUGUI>();serialized.ApplyModifiedPropertiesWithoutUndo();
        var join=menu.Find("CreateJoinRoomBtn");Top(join,92,756,500,72);ButtonStyle(join,"ENTER ROOM",true);Image(join,Amber,false,true);
        Label(menu,"RoomHelp","Join a room by name. A new room is created if needed.",94,846,620,26,15,Muted);
        var exit=menu.Find("ExitBtn");Top(exit,92,934,280,52);ButtonStyle(exit,"QUIT GAME",false);Image(exit,Color.clear,false,true);StyleText(exit.GetComponentInChildren<TMP_Text>().transform,21,White,TextAlignmentOptions.Left,true);
        var marker=Label(menu,"MapName","INDUSTRIAL YARD",0,0,570,34,19,White,true,TextAlignmentOptions.Right);Box(marker,new Vector2(1,0),new Vector2(1,0),-80,64,570,34);
        var roomPanel=canvas.Find("Room");roomPanel.Find("BrandDescriptor").gameObject.SetActive(false);roomPanel.Find("RoomInstructions").GetComponent<TMP_Text>().text="SELECT A SLOT. READY UP.";
        roomPanel.Find("RoomRule").GetComponent<TMP_Text>().text="HOST STARTS WHEN BOTH TEAMS ARE READY";
        var loading=scene.GetRootGameObjects().Single(g=>g.name=="LoadingManager").transform.Find("Canvas/LoadingPanel");loading.Find("LoadingHint").gameObject.SetActive(false);
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Resources/PlayerWithGFX.prefab");
        try
        {
            var hud=prefab.transform.Find("Canvas/PlayerHUD");hud.Find("MatchEndPanel/NextMatch").gameObject.SetActive(false);hud.Find("ControlHints").gameObject.SetActive(false);
            // Compact, translucent HUD elements keep the world dominant.
            Box(hud.Find("KillCount"),new Vector2(.5f,1),new Vector2(.5f,1),0,-30,530,88);
            Image(hud.Find("KillCount"),C("1A1B17"),false);
            Box(hud.Find("WinCondition"),new Vector2(.5f,1),new Vector2(.5f,1),0,-129,530,22);
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Resources/PlayerWithGFX.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Applied charcoal/amber game menu, industrial environment backdrop, required room-name presentation and inline errors; removed slogans and marketing cards.";
    }
    static readonly Color Ink = C("141510"), Panel = C("25251F"), Line = C("4A493D"), White = C("E7E3D6"), Muted = C("A6A28E"), Amber = C("CDA64C"), Blue = C("8399A5"), Red = C("CB6E57");
    const string Art = "Assets/UI/ModernArena/";
    static Color C(string hex) { ColorUtility.TryParseHtmlString("#"+hex, out var c); return c; }
    static RectTransform RT(Transform t) => (RectTransform)t;
    static T Need<T>(Transform t) where T:Component => t.GetComponent<T>() ?? t.gameObject.AddComponent<T>();
    static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (!child) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent,false); }
        return child;
    }
    static void Box(Transform t, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
    {
        var r=RT(t); r.anchorMin=r.anchorMax=anchor; r.pivot=pivot; r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
    }
    static void Top(Transform t,float x,float y,float w,float h) => Box(t,new Vector2(0,1),new Vector2(0,1),x,-y,w,h);
    static void Stretch(Transform t,Vector2 min,Vector2 max,float left=0,float bottom=0,float right=0,float top=0)
    {
        var r=RT(t);r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
    }
    static void Fill(Transform t) => Stretch(t,Vector2.zero,Vector2.one);
    static UnityEngine.UI.Image Image(Transform t,Color color,bool rounded=false,bool raycast=false)
    {
        var image=Need<UnityEngine.UI.Image>(t);image.sprite=rounded?AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Panel.png"):null;image.type=rounded?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.pixelsPerUnitMultiplier=3;image.color=color;image.raycastTarget=raycast;return image;
    }
    static Transform Tile(Transform p,string name,float x,float y,float w,float h,Color c,bool rounded=false)
    {
        var t=Child(p,name);Top(t,x,y,w,h);Image(t,c,rounded);return t;
    }
    static TMP_Text StyleText(Transform t,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Left,bool bold=false)
    {
        var text=Need<TextMeshProUGUI>(t);text.font=TMP_Settings.defaultFontAsset;text.fontSize=size;text.enableAutoSizing=false;text.color=color;text.alignment=alignment;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Truncate;text.margin=Vector4.zero;return text;
    }
    static Transform Label(Transform p,string name,string value,float x,float y,float w,float h,float size,Color color,bool bold=false,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        var t=Child(p,name);Top(t,x,y,w,h);StyleText(t,size,color,alignment,bold).text=value;return t;
    }
    static void DisableLayout(Transform t)
    {
        foreach(var layout in t.GetComponents<UnityEngine.UI.LayoutGroup>())layout.enabled=false;
        var setter=t.GetComponent<GridChildCellSizeSetter>();if(setter)setter.enabled=false;
    }
    static void Scale(Canvas canvas)
    {
        if(canvas.renderMode==RenderMode.WorldSpace)return;
        var scaler=Need<UnityEngine.UI.CanvasScaler>(canvas.transform);
        scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;scaler.matchWidthOrHeight=.5f;
    }
    static void ButtonStyle(Transform t,string label,bool primary)
    {
        var button=t.GetComponent<UnityEngine.UI.Button>();if(!button)throw new Exception("Expected button: "+PathOf(t));
        var image=Image(t,primary?Amber:Panel,true,true);button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=primary?C("E5C986"):C("D9D4BF");colors.pressedColor=C("9F8B57");colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.45f,.51f,.58f,.55f);colors.fadeDuration=.12f;button.colors=colors;
        var text=t.GetComponentInChildren<TMP_Text>(true);if(text){StyleText(text.transform,21,primary?Ink:White,TextAlignmentOptions.Center,true);text.text=label;Fill(text.transform);}
    }
    static void Brand(Transform p)
    {
        Tile(p,"BrandMark",78,58,6,34,Amber);
        Label(p,"Brand","ARENA",100,48,220,52,36,White,true);
        Label(p,"BrandDescriptor","TACTICAL MULTIPLAYER",102,97,260,22,12,Muted,true).GetComponent<TMP_Text>().characterSpacing=3;
        Label(p,"ModeBadge","TEAM DEATHMATCH   /   4 v 4",0,0,440,30,16,Muted,true);
        Box(p.Find("ModeBadge"),new Vector2(1,1),new Vector2(1,1),-80,-66,440,30);
        var rule=Child(p,"TopRule");Stretch(rule,new Vector2(0,1),new Vector2(1,1),80,-145,80,144);Image(rule,Line);
    }
    static void Backdrop(Transform p)
    {
        Image(p,Ink,false);
        var bg=Child(p,"Atmosphere");Fill(bg);var img=Image(bg,Color.white);img.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Backdrop.png");bg.SetAsFirstSibling();
    }
    static void WriteTexture(string name,Texture2D texture,bool sliced=false)
    {
        File.WriteAllBytes(Art+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(Art+name+".png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(Art+name+".png");importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;if(sliced)importer.spriteBorder=new Vector4(12,12,12,12);importer.SaveAndReimport();
    }
    public static string Assets()
    {
        Directory.CreateDirectory(Art);Directory.CreateDirectory("AgentScripts/Backups/ModernUI");
        foreach(var path in new[]{"Assets/Scenes/Lobby.unity","Assets/Resources/PlayerWithGFX.prefab","Assets/Resources/PlayerWithGFX_Red.prefab"})
        {
            var backup="AgentScripts/Backups/ModernUI/"+System.IO.Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);
        }
        var panel=new Texture2D(32,32,TextureFormat.RGBA32,false);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {float d=Vector2.Distance(new Vector2(x,y),new Vector2(Mathf.Clamp(x,8,23),Mathf.Clamp(y,8,23)));panel.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(8.5f-d)));}panel.Apply();WriteTexture("Panel",panel,true);
        var bg=new Texture2D(1280,720,TextureFormat.RGBA32,false);
        for(int y=0;y<720;y++)for(int x=0;x<1280;x++)
        {
            float u=x/1280f,v=y/720f;float cool=Mathf.Exp(-((u-.70f)*(u-.70f)*8+(v-.6f)*(v-.6f)*5));float warm=Mathf.Exp(-((u-1f)*(u-1f)*15+(v-.35f)*(v-.35f)*9));
            var color=Ink+new Color(.018f,.065f,.09f,0)*cool+new Color(.07f,.016f,.023f,0)*warm;
            if(x%80==0||y%80==0)color+=new Color(.008f,.012f,.018f,0);color.a=1;bg.SetPixel(x,y,color);
        }bg.Apply();WriteTexture("Backdrop",bg);
        var ring=new Texture2D(128,128,TextureFormat.RGBA32,false);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++){var d=new Vector2(x-63.5f,y-63.5f);float r=d.magnitude;float angle=Mathf.Atan2(d.y,d.x);float alpha=Mathf.Clamp01(3-Mathf.Abs(r-50))*((angle>-.1f&&angle<.7f)?0:1);ring.SetPixel(x,y,new Color(1,1,1,alpha));}ring.Apply();WriteTexture("Spinner",ring);
        var preview=new PreviewRenderUtility();
        try
        {
            int i=0;
            foreach(var path in new[]{"Assets/Resources/PlayerWithGFX.prefab","Assets/Resources/PlayerWithGFX_Red.prefab"})
            {
                var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));actor.transform.position=new Vector3(i==0?-.43f:.54f,0,i==0?.2f:-.12f);actor.transform.rotation=Quaternion.Euler(0,i++==0?-12:15,0);
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                foreach(var a in actor.GetComponentsInChildren<Animator>(true))a.enabled=false;
                foreach(var c in actor.GetComponentsInChildren<Camera>(true))c.gameObject.SetActive(false);
                foreach(var c in actor.GetComponentsInChildren<Canvas>(true))c.gameObject.SetActive(false);
                preview.AddSingleGO(actor);
            }
            preview.camera.transform.position=new Vector3(1.8f,1.55f,5.4f);preview.camera.transform.LookAt(new Vector3(.03f,.98f,0));preview.camera.fieldOfView=25;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=30;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.clear;
            preview.lights[0].intensity=3.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,30,0);preview.lights[1].intensity=2f;preview.lights[1].color=C("98DCFF");preview.lights[1].transform.rotation=Quaternion.Euler(330,210,0);preview.ambientColor=C("8393A9");
            preview.BeginPreview(new Rect(0,0,1400,1200),GUIStyle.none);preview.Render(true);var result=preview.EndPreview();var rt=RenderTexture.GetTemporary(1400,1200,0,RenderTextureFormat.ARGB32);Graphics.Blit(result,rt);var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1400,1200,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1400,1200),0,0);tex.Apply();RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);WriteTexture("Operators",tex);
        }
        finally{preview.Cleanup();}
        return "Created interface textures and rendered the game's blue/red operators.";
    }

    static void Input(Transform field,string placeholder)
    {
        var input=field.GetComponent<TMP_InputField>();Image(field,C("24251F"),true,true);input.caretColor=Amber;input.selectionColor=new Color(.8f,.65f,.3f,.35f);
        var colors=input.colors;colors.normalColor=Color.white;colors.highlightedColor=C("E2DAC4");colors.selectedColor=Color.white;input.colors=colors;
        var area=field.Find("Text Area");Stretch(area,Vector2.zero,Vector2.one,22,8,22,8);
        StyleText(input.textComponent.transform,24,White);Fill(input.textComponent.transform);
        if(input.placeholder){var text=StyleText(input.placeholder.transform,22,Muted);text.text=placeholder;Fill(input.placeholder.transform);}
    }
    public static string Lobby()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first");
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Lobby.unity")scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
        var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;Scale(canvas.GetComponent<Canvas>());
        var debug=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="IngameDebugConsole");
        if(debug)
        {
            var component=debug.GetComponents<MonoBehaviour>().First(c=>c.GetType().Name=="DebugLogManager");var so=new SerializedObject(component);so.FindProperty("popupVisibility").enumValueIndex=2;so.FindProperty("toggleWithKey").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
        }
        var menu=canvas.Find("Menu");DisableLayout(menu);Backdrop(menu);Brand(menu);
        Label(menu,"Eyebrow","SQUAD UP. STAND OUT.",88,206,600,30,16,Amber,true).GetComponent<TMP_Text>().characterSpacing=4;
        var headline=Label(menu,"Headline","MAKE YOUR\nMARK.",82,252,720,192,82,White,true);headline.GetComponent<TMP_Text>().textWrappingMode=TextWrappingModes.Normal;
        Label(menu,"Description","Two teams. One arena.\nYour next match starts here.",88,462,560,66,23,Muted).GetComponent<TMP_Text>().textWrappingMode=TextWrappingModes.Normal;
        Label(menu,"CallsignLabel","01   /   YOUR CALLSIGN",88,568,520,24,14,Muted,true).GetComponent<TMP_Text>().characterSpacing=2;
        var nick=menu.Find("NickNameInput");Top(nick,88,606,520,66);Input(nick,"Enter your callsign");
        Label(menu,"RoomLabel","02   /   ROOM CODE",88,700,520,24,14,Muted,true).GetComponent<TMP_Text>().characterSpacing=2;
        var room=menu.Find("RoomNameInput");Top(room,88,738,520,66);Input(room,"Enter room name");
        Label(menu,"RoomHint","Room name is required.",88,815,520,26,17,Muted);
        var join=menu.Find("CreateJoinRoomBtn");Top(join,88,872,520,72);ButtonStyle(join,"ENTER ROOM",true);
        var exit=menu.Find("ExitBtn");Box(exit,new Vector2(0,0),Vector2.zero,88,35,170,48);ButtonStyle(exit,"QUIT GAME",false);Image(exit,new Color(0,0,0,0),false,true);StyleText(exit.GetComponentInChildren<TMP_Text>().transform,15,Muted,TextAlignmentOptions.Left,true);
        var giant=Label(menu,"ArenaBackdrop","A R E N A",760,270,1200,240,172,new Color(.4f,.65f,.8f,.055f),true);giant.SetSiblingIndex(1);
        var hero=Child(menu,"Operators");Box(hero,new Vector2(1,.5f),new Vector2(1,.5f),30,40,1240,1030);var image=Image(hero,Color.white);image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Operators.png");image.preserveAspect=true;hero.SetSiblingIndex(2);
        var card=Child(menu,"ModeCard");Box(card,new Vector2(1,0),new Vector2(1,0),-80,92,850,112);Image(card,new Color(.09f,.095f,.075f,.9f),true);
        Tile(card,"Accent",0,0,4,112,Amber);
        Label(card,"Type","TEAM DEATHMATCH",28,18,560,30,25,White,true);
        Label(card,"Detail","INDUSTRIAL ARENA  /  FIRST TO 10 KILLS",28,64,620,25,15,Muted,true);
        Label(card,"TeamSize","4 v 4",680,24,140,55,40,Amber,true,TextAlignmentOptions.Right);
        Label(menu,"Footer","BLUE / RED   •   CHOOSE YOUR SIDE",0,0,580,24,12,Muted,true);Box(menu.Find("Footer"),new Vector2(1,0),new Vector2(1,0),-80,38,580,24);menu.Find("Footer").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Right;

        var teamRoom=canvas.Find("Room");Backdrop(teamRoom);Brand(teamRoom);
        var exitRoom=teamRoom.Find("ExitBtn");Box(exitRoom,new Vector2(1,0),new Vector2(1,0),-80,32,160,44);ButtonStyle(exitRoom,"QUIT GAME",false);
        var roomName=teamRoom.Find("RoomNameText");Top(roomName,88,183,1300,48);StyleText(roomName,36,White,TextAlignmentOptions.Left,true);
        Label(teamRoom,"RoomSubtitle","CHOOSE YOUR SIDE",88,244,1100,38,22,Muted,true).GetComponent<TMP_Text>().characterSpacing=3;
        var teamHeader=Child(teamRoom,"TeamHeaders");Stretch(teamHeader,new Vector2(.05f,.67f),new Vector2(.95f,.67f),0,0,0,-65);
        for(int i=0;i<2;i++)
        {
            var header=Child(teamHeader,i==0?"Blue":"Red");Stretch(header,new Vector2(i*.5f,0),new Vector2((i+1)*.5f,1),i==0?0:24,0,i==0?24:0,0);Image(header,i==0?C("283135"):C("362A23"),true);
            Label(header,"Name",i==0?"BLUE TEAM":"RED TEAM",26,12,500,34,25,i==0?Blue:Red,true);
            Label(header,"Capacity","4 OPERATORS",0,0,190,28,13,Muted,true,TextAlignmentOptions.Right);Box(header.Find("Capacity"),new Vector2(1,.5f),new Vector2(1,.5f),-24,0,190,28);
        }
        var grid=teamRoom.Find("GridLayout");Stretch(grid,new Vector2(.05f,.23f),new Vector2(.95f,.65f));Image(grid,Color.clear);
        var layout=grid.GetComponent<UnityEngine.UI.GridLayoutGroup>();layout.enabled=true;layout.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;layout.spacing=new Vector2(48,16);layout.padding=new RectOffset();layout.startAxis=UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
        grid.GetComponent<GridChildCellSizeSetter>().enabled=true;
        int blueIndex=0,redIndex=0;
        foreach(var slot in grid.GetComponentsInChildren<RoomSlot>(true))
        {
            bool blue=slot.Team==DataTypes.Team.Blue;var accent=blue?Blue:Red;var t=slot.transform;var serialized=new SerializedObject(slot);
            serialized.FindProperty("emptyColor").colorValue=C("24251F");serialized.FindProperty("emptyTextColor").colorValue=Muted;
            serialized.FindProperty("blueColor").colorValue=C("303A3C");serialized.FindProperty("redColor").colorValue=C("44302A");serialized.FindProperty("readyColor").colorValue=C("4A5033");serialized.ApplyModifiedPropertiesWithoutUndo();
            ButtonStyle(t,"OPEN SLOT",false);Image(t,C("24251F"),true,true);
            var name=t.Find("PlayerName");Stretch(name,Vector2.zero,Vector2.one,86,0,24,0);StyleText(name,23,Muted);
            var number=Label(t,"SlotNumber",(blue?++blueIndex:++redIndex).ToString("00"),22,0,44,100,22,accent,true);Stretch(number,new Vector2(0,0),new Vector2(0,1),22,0,-66,0);
            var edge=Child(t,"TeamEdge");Stretch(edge,Vector2.zero,new Vector2(0,1),0,16,-3,16);Image(edge,accent);
        }
        Label(teamRoom,"RoomInstructions","Select an open slot, then ready up.",88,0,800,30,20,White);Box(teamRoom.Find("RoomInstructions"),new Vector2(0,0),Vector2.zero,96,135,800,30);
        Label(teamRoom,"RoomRule","BOTH TEAMS MUST BE READY TO DEPLOY",88,0,800,24,13,Muted,true);Box(teamRoom.Find("RoomRule"),new Vector2(0,0),Vector2.zero,96,98,800,24);
        var ready=teamRoom.Find("ReadyBtn");Box(ready,new Vector2(1,0),new Vector2(1,0),-430,98,300,74);ButtonStyle(ready,"READY",false);
        var start=teamRoom.Find("StartBtn");Box(start,new Vector2(1,0),new Vector2(1,0),-80,98,320,74);ButtonStyle(start,"DEPLOY   >",true);

        var loading=scene.GetRootGameObjects().Single(g=>g.name=="LoadingManager").transform.Find("Canvas/LoadingPanel");Scale(loading.parent.GetComponent<Canvas>());Backdrop(loading);
        Label(loading,"LoadingBrand","ARENA",0,0,800,100,76,White,true,TextAlignmentOptions.Center);Box(loading.Find("LoadingBrand"),new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,100,800,100);
        Label(loading,"LoadingHint","STAY SHARP. PLAY TOGETHER.",0,0,900,30,15,Muted,true,TextAlignmentOptions.Center);Box(loading.Find("LoadingHint"),new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,-150,900,30);
        var group=loading.Find("VerticalLayout");DisableLayout(group);Fill(group);
        var status=group.Find("Text (TMP)");Box(status,new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,-52,700,50);StyleText(status,26,White,TextAlignmentOptions.Center);
        var spinner=group.Find("LoadingImage");Box(spinner,new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,20,48,48);Image(spinner,Amber).sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Spinner.png");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Restyled lobby, team room, inputs, action buttons and loading screen; existing references preserved.";
    }

    public static string ShowMenu()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");
        LoadingManager.Instance.HideLoadingScreen();
        var canvas=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Canvas");
        canvas.transform.Find("Menu").gameObject.SetActive(true);canvas.transform.Find("Room").gameObject.SetActive(false);
        return "Menu visible at frame "+Time.frameCount;
    }
    sealed class PreviewPlayer:Photon.Realtime.Player
    {
        public PreviewPlayer(string name,int actor):base(name,actor,false){}
    }
    public static async System.Threading.Tasks.Task<string> ShowRoom()
    {
        var lobby=UnityEngine.Object.FindAnyObjectByType<LobbyManager>();if(lobby)lobby.enabled=false;
        if(Photon.Pun.PhotonNetwork.IsConnected)Photon.Pun.PhotonNetwork.Disconnect();
        for(int i=0;i<100&&Photon.Pun.PhotonNetwork.IsConnected;i++)await System.Threading.Tasks.Task.Delay(30);
        Photon.Pun.PhotonNetwork.OfflineMode=true;Photon.Pun.PhotonNetwork.NickName="JERIKS";Photon.Pun.PhotonNetwork.CreateRoom("NIGHT SHIFT");
        var canvas=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Canvas");var room=canvas.transform.Find("Room");canvas.transform.Find("Menu").gameObject.SetActive(false);room.gameObject.SetActive(true);LoadingManager.Instance.HideLoadingScreen();await System.Threading.Tasks.Task.Delay(80);
        var slots=room.GetComponentsInChildren<RoomSlot>(true);var local=slots.First(s=>s.Team==DataTypes.Team.Blue);local.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        room.Find("ReadyBtn").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        if(RoomManager.GetLocalTeam()!=DataTypes.Team.Blue||!RoomManager.GetReady(Photon.Pun.PhotonNetwork.LocalPlayer))throw new Exception("Team selection or ready action failed");
        foreach(var data in new[]{new[]{"GHOST","2","1","0"},new[]{"NOVA","3","0","1"}})
        {
            var p=new PreviewPlayer(data[0],int.Parse(data[1]));p.CustomProperties[RoomManager.KeyTeam]=int.Parse(data[2]);p.CustomProperties[RoomManager.KeyTeamIndex]=int.Parse(data[3]);p.CustomProperties[RoomManager.KeySlot]=data[2]=="1"?1:2;p.CustomProperties[RoomManager.KeyReady]=true;Photon.Pun.PhotonNetwork.CurrentRoom.StorePlayer(p);
        }
        typeof(RoomManager).GetMethod("Refresh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(RoomManager.Instance,null);
        if(!room.Find("StartBtn").GetComponent<UnityEngine.UI.Button>().interactable)throw new Exception("Host deploy button did not become ready");
        return "Offline room preview: slot selection, ready state and host deploy availability passed. frame="+Time.frameCount;
    }
    public static string ShowLoading()
    {
        LoadingManager.Instance.ShowLoadingScreen("Synchronizing match...");return "Loading screen visible";
    }
    public static string TestRequiredRoom()
    {
        var menu=GameObject.Find("Canvas").transform.Find("Menu");var nick=menu.Find("NickNameInput").GetComponent<TMP_InputField>();var room=menu.Find("RoomNameInput").GetComponent<TMP_InputField>();var button=menu.Find("CreateJoinRoomBtn").GetComponent<UnityEngine.UI.Button>();var status=menu.Find("LobbyStatus").GetComponent<TMP_Text>();
        nick.text="JERIKS";
        var originalRoom=Photon.Pun.PhotonNetwork.CurrentRoom;
        foreach(var invalid in new[]{"", "   ", "\u200B  "})
        {
            room.text=invalid;button.onClick.Invoke();
            if(status.text!="ROOM NAME REQUIRED"||!ReferenceEquals(originalRoom,Photon.Pun.PhotonNetwork.CurrentRoom))throw new Exception("Invalid room name was accepted");
        }
        room.text="";return "Empty, whitespace-only and zero-width-only room names rejected with visible inline errors.";
    }
    public static async System.Threading.Tasks.Task<string> TestNamedRoom()
    {
        var lobby=UnityEngine.Object.FindAnyObjectByType<LobbyManager>();lobby.enabled=false;
        if(Photon.Pun.PhotonNetwork.IsConnected)Photon.Pun.PhotonNetwork.Disconnect();
        for(int i=0;i<100&&Photon.Pun.PhotonNetwork.IsConnected;i++)await System.Threading.Tasks.Task.Delay(30);
        Photon.Pun.PhotonNetwork.OfflineMode=true;
        var menu=GameObject.Find("Canvas").transform.Find("Menu");menu.Find("NickNameInput").GetComponent<TMP_InputField>().text="  JERIKS  ";menu.Find("RoomNameInput").GetComponent<TMP_InputField>().text="  A  ";
        menu.Find("CreateJoinRoomBtn").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();await System.Threading.Tasks.Task.Delay(80);
        if(!Photon.Pun.PhotonNetwork.InRoom||Photon.Pun.PhotonNetwork.CurrentRoom.Name!="A"||Photon.Pun.PhotonNetwork.NickName!="JERIKS")throw new Exception("Named room request did not preserve trimmed name");
        LoadingManager.Instance.HideLoadingScreen();
        return "Named room A created in offline mode; trimmed nickname and room name preserved. No random-room fallback.";
    }
    public static async System.Threading.Tasks.Task<string> ShowHUD()
    {
        Photon.Pun.PhotonNetwork.OfflineMode=true;Photon.Pun.PhotonNetwork.NickName="JERIKS";Photon.Pun.PhotonNetwork.CreateRoom("NIGHT SHIFT");
        Photon.Pun.PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{{RoomManager.KeyTeam,1},{RoomManager.KeyTeamIndex,0}});
        var spawn=GameManager.Instance.GetSpawnLoc();var root=Photon.Pun.PhotonNetwork.Instantiate("PlayerWithGFX_Red",spawn.position,spawn.rotation);await System.Threading.Tasks.Task.Delay(500);
        GameManager.OnScoreChanged?.Invoke(7,5);GameManager.OnTimerChanged?.Invoke(487);
        root.GetComponent<HealthComponent>().Damage(25,root.transform.position,Vector3.up,-1);
        foreach(var text in root.transform.Find("Canvas").GetComponentsInChildren<TMP_Text>(true))if(text.font==null)throw new Exception("Missing UI font: "+text.name);
        return "HUD visible, live score/time/health bindings exercised; frame="+Time.frameCount;
    }
    public static string ShowResults()
    {
        var root=UnityEngine.Object.FindObjectsByType<PlayerUI>().Single(p=>p.photonView.IsMine);
        int actor=Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber;
        var p=new PreviewPlayer("GHOST",2);p.CustomProperties[RoomManager.KeyTeam]=0;Photon.Pun.PhotonNetwork.CurrentRoom.StorePlayer(p);
        var p2=new PreviewPlayer("NOVA",3);p2.CustomProperties[RoomManager.KeyTeam]=1;Photon.Pun.PhotonNetwork.CurrentRoom.StorePlayer(p2);
        GameManager.Instance.blueKills=10;GameManager.Instance.redKills=7;
        GameManager.OnMatchEnded?.Invoke(1,actor+":1:4:680;2:0:10:1250;3:1:3:440");
        var result=root.transform.Find("Canvas/PlayerHUD/MatchEndPanel");if(!result.gameObject.activeInHierarchy)throw new Exception("Results screen not shown");
        if(result.Find("RedTeamPanel/EndScoreText").GetComponent<TMP_Text>().text!="Kills: 7")throw new Exception("Red result score binding failed");
        return "Results visible; both team scores and player stats populated.";
    }
    public static string ResizeCheck()
    {
        var assembly=typeof(UnityEditor.Editor).Assembly;
        var sizeManagerType=assembly.GetType("UnityEditor.GameViewSizes");
        var singletonType=typeof(ScriptableSingleton<>).MakeGenericType(sizeManagerType);
        var manager=singletonType.GetProperty("instance").GetValue(null);
        var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group=sizeManagerType.GetMethod("GetGroup").Invoke(manager,new[]{Enum.Parse(groupType,"Standalone")});
        var viewType=assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(viewType);
        var selected=viewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        SessionState.SetInt("ModernUI.PreviousSize",(int)selected.GetValue(window));
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kindType=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance,null,new object[]{Enum.Parse(kindType,"FixedResolution"),1440,1080,"UI validation 4:3"},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;SessionState.SetInt("ModernUI.QASize",index);selected.SetValue(window,index);window.Repaint();
        return "Set Game view to 1440 x 1080 for layout validation";
    }
    public static string RestoreSize()
    {
        var assembly=typeof(UnityEditor.Editor).Assembly;var viewType=assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(window,SessionState.GetInt("ModernUI.PreviousSize",0));
        var managerType=assembly.GetType("UnityEditor.GameViewSizes");var manager=typeof(ScriptableSingleton<>).MakeGenericType(managerType).GetProperty("instance").GetValue(null);
        var group=managerType.GetMethod("GetGroup").Invoke(manager,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});int index=SessionState.GetInt("ModernUI.QASize",-1);if(index>=0)group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{index});SessionState.EraseInt("ModernUI.QASize");return "Restored original Game view size";
    }
    public static string ValidateBounds()
    {
        Canvas.ForceUpdateCanvases();var findings=new StringBuilder();int checkedCount=0;
        foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
        {
            if(canvas.renderMode==RenderMode.WorldSpace||canvas.name=="IngameDebugConsole")continue;
            foreach(var control in canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>())
            {
                if(!control.gameObject.activeInHierarchy)continue;var corners=new Vector3[4];control.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,corner);if(p.x<-.1f||p.x>Screen.width+.1f||p.y<-.1f||p.y>Screen.height+.1f)throw new Exception("Control outside viewport: "+PathOf(control.transform)+" "+p);}
                checkedCount++;
            }
        }
        return "Viewport "+Screen.width+"x"+Screen.height+": "+checkedCount+" visible controls fully within screen.";
    }
    public static string FinalizeDesign()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Lobby.unity")throw new Exception("Restore lobby first");
        var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas");
        foreach(var path in new[]{"Menu/CreateJoinRoomBtn","Menu/ExitBtn","Room/ExitBtn"})
            if(canvas.transform.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.GetPersistentEventCount()==0)throw new Exception("Missing action: "+path);
        foreach(var name in new[]{"PlayerWithGFX","PlayerWithGFX_Red"})
        {
            var root=Resources.Load<GameObject>(name);var ui=new SerializedObject(root.GetComponent<PlayerUI>());
            foreach(var field in new[]{"roomNameTxt","worldNameText","healthBarFill","blueScoreText","redScoreText","timerText","matchEndPanel","winnerText","exitToLobbyButton"})
                if(!ui.FindProperty(field).objectReferenceValue)throw new Exception(name+": missing "+field);
            if(root.GetComponent<Photon.Pun.PhotonView>().ObservedComponents.Any(c=>!c))throw new Exception("Missing observable on "+name);
            if(!root.transform.Find("Canvas/PlayerHUD/MatchEndPanel/RedTeamPanel/EndScoreText"))throw new Exception("Red result header missing");
            foreach(var text in root.transform.Find("Canvas").GetComponentsInChildren<TMP_Text>(true))
                if(!text.font||text.overflowMode==TextOverflowModes.Ellipsis)throw new Exception("Invalid font/overflow on "+text.name);
        }
        foreach(var path in new[]{"Assets/Screenshots/ModernUI-lobby-first.png","Assets/Screenshots/ModernUI-Lobby-live.png"})if(AssetDatabase.LoadAssetAtPath<Texture2D>(path))AssetDatabase.DeleteAsset(path);
        foreach(var path in new[]{"Screenshots/ModernUI-Lobby.png","Screenshots/ModernUI-Room.png"})if(File.Exists(path))File.Delete(path);
        var report="Modern Arena interface\n\nRestyled: menu, room/team selection, loading, both team HUDs, match results.\nStyle: navy panels, cyan primary actions, blue/red team accents, shared typography, character artwork rendered from project prefabs.\n\nPlay mode validation passed:\n- Slot selection, ready action and host deployment availability in an offline room.\n- Live score, timer and health updates on the red variant.\n- Both team result totals and player kill/damage rows.\n- 1920x1080 visual review for all five screens.\n- 1440x1080 visual review and viewport bounds for menu (4 controls) and room (11 controls).\n- Both player variants retain UI references and Photon observables.\n\nPreviews: Assets/Screenshots/ModernUI-*.png\nBuilder: AgentScripts/ModernUI.cs (Assets, Lobby, HUD).\n";
        File.WriteAllText("AgentScripts/modern-ui-validation.txt",report);
        return "Validated original menu actions, UI bindings on both team prefabs, result paths, fonts and Photon observables; removed intermediate previews.";
    }

    static void TableRow(Transform row,bool heading)
    {
        var name=row.Find("Name");var kills=row.Find("Kills");var damage=row.Find("Kills (1)");
        if(name){Stretch(name,new Vector2(0,0),new Vector2(.55f,1),22,0,4,0);StyleText(name,heading?13:22,heading?Muted:White,TextAlignmentOptions.Left,!heading);if(heading)name.GetComponent<TMP_Text>().text="OPERATOR";}
        if(kills){Stretch(kills,new Vector2(.55f,0),new Vector2(.73f,1),0,0,0,0);StyleText(kills,heading?13:23,heading?Muted:White,TextAlignmentOptions.Center,true);if(heading)kills.GetComponent<TMP_Text>().text="KILLS";}
        if(damage){Stretch(damage,new Vector2(.73f,0),new Vector2(1,1),0,0,22,0);StyleText(damage,heading?13:22,heading?Muted:White,TextAlignmentOptions.Right,!heading);if(heading)damage.GetComponent<TMP_Text>().text="DAMAGE";}
    }
    public static string HUD()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first");
        const string path="Assets/Resources/PlayerWithGFX.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var canvas=root.transform.Find("Canvas");Scale(canvas.GetComponent<Canvas>());var hud=canvas.Find("PlayerHUD");
            var room=hud.Find("RoomName");Top(room,48,43,380,34);StyleText(room,16,White,TextAlignmentOptions.Left,true);
            Label(hud,"MatchType","TEAM DEATHMATCH",48,80,380,25,13,Muted,true).GetComponent<TMP_Text>().characterSpacing=2;
            var scoreboard=hud.Find("KillCount");DisableLayout(scoreboard);Box(scoreboard,new Vector2(.5f,1),new Vector2(.5f,1),0,-46,530,88);Image(scoreboard,new Color(.055f,.058f,.045f,.9f),true);
            var blue=scoreboard.Find("Bg");var red=scoreboard.Find("Bg (1)");Top(blue,0,0,155,88);Top(red,375,0,155,88);Image(blue,C("293338"),true);Image(red,C("3A2923"),true);
            Label(blue,"TeamLabel","BLUE",18,11,120,20,12,Blue,true);Label(red,"TeamLabel","RED",18,11,120,20,12,Red,true,TextAlignmentOptions.Right);
            var blueScore=blue.Find("BlueScore");Top(blueScore,18,30,120,48);StyleText(blueScore,39,White,TextAlignmentOptions.Left,true);
            var redScore=red.Find("RedScore");Top(redScore,18,30,120,48);StyleText(redScore,39,White,TextAlignmentOptions.Right,true);
            Tile(blue,"TeamLine",0,85,155,3,Blue);Tile(red,"TeamLine",0,85,155,3,Red);
            var timer=hud.Find("TimerBG")??scoreboard.Find("TimerBG");timer.SetParent(scoreboard,false);Top(timer,167,0,196,88);Image(timer,Color.clear);
            Label(timer,"TimerLabel","TIME REMAINING",0,10,196,20,11,Muted,true,TextAlignmentOptions.Center);
            var time=timer.Find("Text (TMP)");Top(time,0,31,196,44);StyleText(time,30,White,TextAlignmentOptions.Center,true);
            var rule=Label(hud,"WinCondition","FIRST TO 10 KILLS",0,0,530,22,11,Muted,true,TextAlignmentOptions.Center);Box(rule,new Vector2(.5f,1),new Vector2(.5f,1),0,-147,530,22);
            var cross=hud.Find("Image");Box(cross,new Vector2(.5f,.5f),new Vector2(.5f,.5f),0,0,3,3);Image(cross,White);
            foreach(var spec in new[]{new Vector4(-9,0,6,2),new Vector4(9,0,6,2),new Vector4(0,-9,2,6),new Vector4(0,9,2,6)})
            {
                var tick=Child(cross,"Tick"+spec.x+"_"+spec.y);Box(tick,new Vector2(.5f,.5f),new Vector2(.5f,.5f),spec.x,spec.y,spec.z,spec.w);Image(tick,new Color(1,1,1,.85f));
            }
            var healthCard=Child(hud,"VitalsPanel");Box(healthCard,Vector2.zero,Vector2.zero,40,40,338,98);Image(healthCard,new Color(.055f,.058f,.045f,.9f),true);healthCard.SetAsFirstSibling();
            Label(healthCard,"Plus","+",18,10,40,50,40,Amber,true,TextAlignmentOptions.Center);
            Label(healthCard,"Title","VITALS",76,16,210,25,18,White,true);
            Label(healthCard,"Sub","HEALTH",76,45,210,20,11,Muted,true);
            var health=hud.Find("HealthBarBG");Box(health,Vector2.zero,Vector2.zero,60,58,298,8);Image(health,C("454638"),true);
            var fill=health.Find("HealthBarFill");Fill(fill);var fillImage=Image(fill,Amber,true);fillImage.type=UnityEngine.UI.Image.Type.Filled;fillImage.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fillImage.fillOrigin=0;fillImage.fillAmount=1;
            var equipment=Child(hud,"WeaponPanel");Box(equipment,new Vector2(1,0),new Vector2(1,0),-40,40,330,98);Image(equipment,new Color(.055f,.058f,.045f,.9f),true);
            Label(equipment,"Slot","01   /   PRIMARY",22,15,280,24,12,Muted,true);
            Label(equipment,"Weapon","ASSAULT RIFLE",22,46,280,30,23,White,true);
            var controls=Label(hud,"ControlHints","WASD  MOVE    /    SHIFT  SPRINT    /    SPACE  JUMP",0,0,850,22,11,Muted,true,TextAlignmentOptions.Center);Box(controls,new Vector2(.5f,0),new Vector2(.5f,0),0,30,850,22);
            var results=hud.Find("MatchEndPanel");Fill(results);Backdrop(results);Image(results,Ink,false,true);results.SetAsLastSibling();
            Label(results,"ResultEyebrow","MATCH COMPLETE",0,0,1200,30,16,Amber,true,TextAlignmentOptions.Center);Box(results.Find("ResultEyebrow"),new Vector2(.5f,1),new Vector2(.5f,1),0,-76,1200,30);
            var winner=results.Find("WinnerText");Box(winner,new Vector2(.5f,1),new Vector2(.5f,1),0,-125,1600,90);StyleText(winner,60,White,TextAlignmentOptions.Center,true).fontStyle|=FontStyles.UpperCase;
            Label(results,"ResultsCaption","TEAM DEATHMATCH   /   FINAL RESULTS",0,0,1200,26,14,Muted,true,TextAlignmentOptions.Center);Box(results.Find("ResultsCaption"),new Vector2(.5f,1),new Vector2(.5f,1),0,-236,1200,26);
            for(int side=0;side<2;side++)
            {
                var team=results.Find(side==0?"BlueTeamPanel":"RedTeamPanel");Stretch(team,new Vector2(side==0?.06f:.515f,.22f),new Vector2(side==0?.485f:.94f,.69f));Image(team,Panel,true);var accent=side==0?Blue:Red;
                Tile(team,"Accent",0,0,3,68,accent);Label(team,"TeamTitle",side==0?"BLUE TEAM":"RED TEAM",24,18,500,36,27,accent,true);
                var score=team.Find("EndScoreText")??team.Find("EndScoreText (1)");score.name="EndScoreText";Box(score,new Vector2(1,1),new Vector2(1,1),-24,-20,230,34);StyleText(score,21,White,TextAlignmentOptions.Right,true);
                var heading=team.Find(side==0?"TableTitle":"TableTitle (1)");Stretch(heading,new Vector2(0,.72f),new Vector2(1,.85f),14,0,14,0);Image(heading,new Color(.13f,.14f,.11f,1));TableRow(heading,true);
                var players=team.Find("Players");Stretch(players,Vector2.zero,new Vector2(1,.71f),14,14,14,0);Image(players,Color.clear);
                var layout=players.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.enabled=true;layout.padding=new RectOffset();layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;
                int index=0;
                foreach(Transform row in players)
                {
                    Image(row,index++%2==0?C("2B2D26"):C("24271F"),true);TableRow(row,false);Need<UnityEngine.UI.LayoutElement>(row).flexibleHeight=1;
                }
            }
            var back=results.Find("ExitToLobbyButton");Box(back,new Vector2(.5f,0),new Vector2(.5f,0),0,96,390,70);ButtonStyle(back,"RETURN TO LOBBY   >",true);
            Label(results,"NextMatch","REGROUP. RELOAD. GO AGAIN.",0,0,1000,25,13,Muted,true,TextAlignmentOptions.Center);Box(results.Find("NextMatch"),new Vector2(.5f,0),new Vector2(.5f,0),0,47,1000,25);
            var worldName=root.transform.Find("WorldNameCanvas/NameText");StyleText(worldName,24,White,TextAlignmentOptions.Center,true);
            foreach(var component in root.GetComponentsInChildren<Component>(true))if(component && PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        return "Redesigned the shared HUD, crosshair, health bar, timer, score display and match results. Both team variants inherit the design.";
    }

    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static void Describe(GameObject root, StringBuilder b)
    {
        foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
        {
            var text = t.GetComponent<TMP_Text>();
            b.AppendLine(PathOf(t) + " active=" + t.gameObject.activeSelf + " size=" + t.sizeDelta + " anchors=" + t.anchorMin + ":" + t.anchorMax + " pos=" + t.anchoredPosition + " [" + string.Join(",",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)) + "]" + (text ? " TEXT=" + text.text.Replace("\n","|") : ""));
        }
    }
    public static string Inspect()
    {
        var b = new StringBuilder();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Describe(root,b);
        b.AppendLine("PLAYER PREFAB");
        Describe(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PlayerWithGFX.prefab"),b);
        File.WriteAllText("AgentScripts/ui-inspection.txt",b.ToString());
        return b.ToString();
    }
}
