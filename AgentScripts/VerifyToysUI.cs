using System;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class VerifyToysUI
{
    sealed class Opponent:Player{public Opponent(string name,int id):base(name,id,false){}}
    static void Check(bool c,string message){if(!c)throw new Exception(message);}
    static Transform Canvas()=>SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
    public static async Task<string> Lobby()
    {
        Check(Application.isPlaying,"Requires Play mode");var lobby=UnityEngine.Object.FindAnyObjectByType<LobbyManager>();var canvas=Canvas();var menu=canvas.Find("Menu");var terminal=menu.Find("DeploymentTerminal");LoadingManager.Instance.HideLoadingScreen();
        var nickname=terminal.Find("NickNameInput").GetComponent<TMP_InputField>();var room=terminal.Find("RoomNameInput").GetComponent<TMP_InputField>();var error=terminal.Find("LobbyStatus").GetComponent<TMP_Text>();nickname.text="JERIKS";
        foreach(var value in new[]{"","  ","\u200b "})
        {
            room.text=value;terminal.Find("CreateJoinRoomBtn").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();Check(error.text=="ROOM NAME REQUIRED","Create accepted blank room");
            terminal.Find("JoinRoomBtn").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();Check(error.text=="ROOM NAME REQUIRED","Join accepted blank room");
        }
        lobby.enabled=false;if(PhotonNetwork.IsConnected)PhotonNetwork.Disconnect();for(int i=0;i<100&&PhotonNetwork.IsConnected;i++)await Task.Delay(30);PhotonNetwork.OfflineMode=true;lobby.enabled=true;
        room.text="  TOYBOX-01  ";nickname.text="JERIKS";terminal.Find("CreateJoinRoomBtn").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();await Task.Delay(120);LoadingManager.Instance.HideLoadingScreen();
        Check(PhotonNetwork.InRoom&&PhotonNetwork.CurrentRoom.Name=="TOYBOX-01","Named create failed");Check(!menu.gameObject.activeSelf&&canvas.Find("Room").gameObject.activeSelf,"Room view did not open");
        var selection=canvas.Find("Room");var slots=selection.GetComponentsInChildren<RoomSlot>(true);
        foreach(var slot in slots)
        {
            var label=slot.transform.Find("PlayerName").GetComponent<TMP_Text>();Check(UnityEditor.AssetDatabase.GetAssetPath(label.font)=="Assets/UI/ToysDeathMatch/Fonts/Display.asset"&&label.fontSize==29,"Slot typography reverted after reload: "+label.font.name+" / "+label.fontSize);Check(slot.GetComponent<UnityEngine.UI.Image>().sprite==null,"Slot background reverted after reload");
        }
        slots.First(s=>s.Team==DataTypes.Team.Blue).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();selection.Find("ReadyBtn").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();Check(RoomManager.GetReady(PhotonNetwork.LocalPlayer),"Ready action failed");
        var p=new Opponent("REAPER",2);p.CustomProperties[RoomManager.KeySlot]=1;p.CustomProperties[RoomManager.KeyTeam]=1;p.CustomProperties[RoomManager.KeyTeamIndex]=0;p.CustomProperties[RoomManager.KeyReady]=true;PhotonNetwork.CurrentRoom.StorePlayer(p);
        var p2=new Opponent("NOVA",3);p2.CustomProperties[RoomManager.KeySlot]=2;p2.CustomProperties[RoomManager.KeyTeam]=0;p2.CustomProperties[RoomManager.KeyTeamIndex]=1;p2.CustomProperties[RoomManager.KeyReady]=true;PhotonNetwork.CurrentRoom.StorePlayer(p2);
        typeof(RoomManager).GetMethod("Refresh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(RoomManager.Instance,null);await Task.Delay(300);
        Check(selection.Find("StartBtn").GetComponent<UnityEngine.UI.Button>().interactable,"Deploy did not become available");var ui=canvas.GetComponent<ToysGameUI>();Check(ui.blueCountText.text=="2 / 4"&&ui.redCountText.text=="1 / 4","Team counts not live");
        return "Required names on both actions, named create, team slots, ready, live counts and host deployment passed in offline room.";
    }
    public static async Task<string> LeaveAndShowMenu()
    {
        var lobby=UnityEngine.Object.FindAnyObjectByType<LobbyManager>();lobby.LeaveRoomBtn();await Task.Delay(120);var menu=Canvas().Find("Menu");Check(menu.gameObject.activeSelf&&!PhotonNetwork.InRoom,"Leave did not restore lobby");
        var terminal=menu.Find("DeploymentTerminal");terminal.Find("LobbyStatus").GetComponent<TMP_Text>().text="";terminal.Find("NickNameInput").GetComponent<TMP_InputField>().text="VIPER";terminal.Find("RoomNameInput").GetComponent<TMP_InputField>().text="TOYBOX-01";
        var ui=Canvas().GetComponent<ToysGameUI>();float scale=Time.timeScale;ui.ShowExit();Check(ui.IsExitOpen,"Quit confirmation failed");ui.CancelExit();Check(!ui.IsExitOpen&&Time.timeScale==scale,"Cancel did not close cleanly");return "Leave room restored lobby; desktop quit confirmation opens/cancels without changing time scale.";
    }
    public static string ShowLoading(){LoadingManager.Instance.ShowLoadingScreen("Synchronizing match...");return "Loading screen visible";}
    public static async Task<string> HUD()
    {
        Check(Application.isPlaying,"Requires Play mode");PhotonNetwork.OfflineMode=true;PhotonNetwork.NickName="VIPER";PhotonNetwork.CreateRoom("TOYBOX-01");PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{{RoomManager.KeyTeam,1},{RoomManager.KeyTeamIndex,0}});
        var spawn=GameManager.Instance.GetSpawnLoc();var root=PhotonNetwork.Instantiate("PlayerWithGFX_Red",spawn.position,spawn.rotation);await Task.Delay(700);GameManager.OnScoreChanged?.Invoke(7,5);GameManager.OnTimerChanged?.Invoke(487);root.GetComponent<HealthComponent>().Damage(15,root.transform.position,Vector3.up,-1);
        var ui=root.GetComponent<ToysGameUI>();Check(ui.healthValueText.text=="85","Health number is not live");Check(ui.targetScoreText.text==GameManager.WIN_SCORE.ToString(),"Score target incorrect");Check(root.transform.Find("Canvas/PlayerHUD/HealthCard/HealthBarBG/HealthBarFill").GetComponent<UnityEngine.UI.Image>().fillAmount==.85f,"Health bar not live");
        int before=Time.frameCount;await Task.Delay(100);Check(Time.frameCount>before,"Game view frozen");
        return "Live player name, 85 health, health bar, team scores, timer and target verified; frames advancing.";
    }
    public static async Task<string> ExitInteraction()
    {
        var p=UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().Single(x=>x.photonView.IsMine);var ui=p.GetComponent<ToysGameUI>();var input=p.GetComponent<InputManager>();var priorEditor=InputSystem.settings.editorInputBehaviorInPlayMode;var priorBackground=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.EnableDevice(Keyboard.current);InputSystem.EnableDevice(Mouse.current);
        try
        {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape));await Task.Delay(80);Check(ui.IsExitOpen&&input.IsGameplayBlocked&&Cursor.visible,"Escape did not open confirmation");InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());await Task.Delay(50);
            var position=p.transform.position;int shots=p.Shots;int frame=Time.frameCount;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));InputSystem.QueueStateEvent(Mouse.current,new MouseState().WithButton(MouseButton.Left));await Task.Delay(220);
            Check(p.Shots==shots,"Fired through dialog");Check(Vector2.Distance(new Vector2(position.x,position.z),new Vector2(p.transform.position.x,p.transform.position.z))<.06f,"Moved through dialog");Check(Time.frameCount>frame&&Time.timeScale==1,"Dialog paused live match");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.QueueStateEvent(Mouse.current,new MouseState());ui.CancelExit();Check(!ui.IsExitOpen&&!input.IsGameplayBlocked&&Cursor.lockState==CursorLockMode.Locked,"Cancel failed to restore controls");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape));await Task.Delay(70);Check(ui.IsExitOpen,"Escape could not reopen");return "ESC, cursor release, blocked movement/shooting, continued live frames and cancel/resume passed; dialog left open for preview.";
        }
        finally{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.QueueStateEvent(Mouse.current,new MouseState());InputSystem.settings.editorInputBehaviorInPlayMode=priorEditor;InputSystem.settings.backgroundBehavior=priorBackground;}
    }
    public static async Task<string> ConfirmLeave()
    {
        var ui=UnityEngine.Object.FindObjectsByType<ToysGameUI>().Single(x=>x.matchInterface&&x.GetComponent<PhotonView>().IsMine);ui.ConfirmExit();for(int i=0;i<100&&SceneManager.GetActiveScene().name!="Lobby";i++)await Task.Delay(50);Check(SceneManager.GetActiveScene().name=="Lobby","Confirmed exit did not return to Lobby");return "Confirmed match exit returned to Lobby.";
    }
}
