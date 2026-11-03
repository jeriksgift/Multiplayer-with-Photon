using System;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class VerifyLobbyToolkit
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static UIDocument Document()=>SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="LobbyDocument").GetComponent<UIDocument>();
    static void Click(Button button)
    {
        button.Focus();using(var evt=NavigationSubmitEvent.GetPooled())button.SendEvent(evt);
    }
    public static string Show()
    {
        LoadingManager.Instance.HideLoadingScreen();var doc=Document();var r=doc.rootVisualElement;Check(doc.gameObject.activeSelf,"Document is hidden");
        var panel=r.Q("Panel");var form=r.Q("Container");var button=r.Q<Button>("CreateJoinRoomButton");
        return "UIDocument "+r.worldBound+"; panel="+panel.worldBound+"; form="+form.worldBound+"; button="+button.worldBound+"; time="+r.Q<Label>("DateTimeLabel").text;
    }
    public static string State()
    {
        var canvas=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Canvas");var manager=UnityEngine.Object.FindAnyObjectByType<LobbyManager>(FindObjectsInactive.Include);var so=new UnityEditor.SerializedObject(manager);var room=(GameObject)so.FindProperty("roomPanel").objectReferenceValue;
        return "Canvas active="+canvas.activeSelf+"; Canvas/Room active="+canvas.transform.Find("Room").gameObject.activeSelf+"; referenced room="+room.name+" parent="+room.transform.parent.name+" active="+room.activeInHierarchy+"; controller="+manager.enabled;
    }
    public static async Task<string> Clock()
    {
        Check(LobbyScreen.FormatLocalTime(new DateTime(2026,11,3,16,51,0))=="3rd November, 2026 - 16:51","Sample format mismatch");
        foreach(var example in new[]{"1st","2nd","3rd","4th","11th","12th","13th","21st","22nd","23rd","31st"})
        {
            int day=int.Parse(new string(example.TakeWhile(char.IsDigit).ToArray()));Check(LobbyScreen.FormatLocalTime(new DateTime(2026,1,day,0,5,0))==example+" January, 2026 - 00:05","Ordinal or 24-hour clock incorrect: "+example);
        }
        var label=Document().rootVisualElement.Q<Label>("DateTimeLabel");string before=LobbyScreen.FormatLocalTime(DateTime.Now);label.text="STALE CLOCK";await Task.Delay(1300);string after=LobbyScreen.FormatLocalTime(DateTime.Now);
        Check(label.text==before||label.text==after,"Clock did not refresh in real time");return "Exact sample format, ordinal suffixes, midnight and live scheduler refresh passed: "+label.text;
    }
    public static async Task<string> Flow()
    {
        var doc=Document();var root=doc.rootVisualElement;var manager=UnityEngine.Object.FindAnyObjectByType<LobbyManager>();var nickname=root.Q<TextField>("NickNameTextField");var room=root.Q<TextField>("RoomNameTextField");var button=root.Q<Button>("CreateJoinRoomButton");var status=root.Q<Label>("LobbyStatusLabel");
        nickname.value="";room.value="";Click(button);Check(status.text.Contains("at least 2"),"Button not wired to validation");
        nickname.value="VIPER";
        foreach(var invalid in new[]{"","   ","\u200B "}){room.value=invalid;Click(button);Check(status.text=="ROOM NAME REQUIRED"&&!status.ClassListContains("lobby-status--hidden"),"Required room validation failed");}
        manager.enabled=false;if(PhotonNetwork.IsConnected)PhotonNetwork.Disconnect();for(int i=0;i<100&&PhotonNetwork.IsConnected;i++)await Task.Delay(30);PhotonNetwork.OfflineMode=true;manager.enabled=true;
        for(int pass=0;pass<2;pass++)
        {
            root=doc.rootVisualElement;nickname=root.Q<TextField>("NickNameTextField");room=root.Q<TextField>("RoomNameTextField");button=root.Q<Button>("CreateJoinRoomButton");
            nickname.value=" VIPER ";room.value=" BUILDER-LOBBY ";Click(button);await Task.Delay(120);
            Check(PhotonNetwork.InRoom&&PhotonNetwork.CurrentRoom.Name=="BUILDER-LOBBY"&&PhotonNetwork.NickName=="VIPER","Named Create/Join failed");Check(!doc.gameObject.activeSelf,"Toolkit lobby did not hide after joining");
            var legacyCanvas=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Canvas");Check(legacyCanvas.transform.Find("Room").gameObject.activeInHierarchy,"Existing team selection did not open");Check(!legacyCanvas.transform.Find("Menu").gameObject.activeSelf,"Old lobby was re-enabled");
            manager.LeaveRoomBtn();await Task.Delay(150);Check(doc.gameObject.activeSelf&&!PhotonNetwork.InRoom,"Toolkit lobby did not return after leaving");
            root=doc.rootVisualElement;Check(root.Q<TextField>("NickNameTextField").value=="VIPER"&&root.Q<TextField>("RoomNameTextField").value=="BUILDER-LOBBY","Input values lost on re-enable");
        }
        manager.OnJoinRoomFailed(1,"Preview connection error");Check(doc.rootVisualElement.Q<Label>("LobbyStatusLabel").text.Contains("Preview connection error"),"Network error not displayed");
        doc.rootVisualElement.Q<TextField>("RoomNameTextField").value="BUILDER-LOBBY-01";Check(doc.rootVisualElement.Q<Label>("LobbyStatusLabel").ClassListContains("lobby-status--hidden"),"Status did not clear on edit");
        return "Actual Toolkit submit event, nickname/required-room validation, named Create/Join, unchanged team-selection handoff, two leave/reopen cycles, preserved inputs and error feedback passed in offline mode.";
    }
}
