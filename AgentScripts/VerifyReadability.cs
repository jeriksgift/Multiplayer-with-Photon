using System;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class VerifyReadability
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task<string> Hud()
    {
        SceneManager.LoadScene("Game");
        await Task.Delay(500);
        var player = UnityEngine.Object.FindObjectsByType<GameHudScreen>().Single(x => x.GetComponent<PhotonView>().IsMine);
        var root = player.transform.Find("GameHudDocument").GetComponent<UIDocument>().rootVisualElement;
        foreach (var label in root.Query<Label>(className: "hud-caption").ToList())
        {
            Check(label.resolvedStyle.fontSize >= 22, "HUD caption too small");
            Check(label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x <= label.contentRect.width + 2, "HUD caption clipped: " + label.text);
        }
        Check(root.Q(className: "room-info").resolvedStyle.backgroundColor.a == 1f, "Room text has no solid backing");
        Check(root.Q(className: "ammo-panel").resolvedStyle.backgroundColor.a == 1f, "Ammo text has no solid backing");
        return "PASS: HUD captions 22px+, no clipped captions, solid room/ammo backing.";
    }
    public static async Task<string> Results()
    {
        var player = UnityEngine.Object.FindObjectsByType<GameHudScreen>().Single(x => x.GetComponent<PhotonView>().IsMine);
        GameManager.Instance.blueKills = 10; GameManager.Instance.redKills = 7;
        GameManager.Instance.matchEnded = true;
        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        GameManager.OnMatchEnded?.Invoke(1, actor + ":0:10:1000;9:1:7:720;10:0:0:140;11:1:0:60");
        await Task.Delay(150);
        var root = player.transform.Find("MatchResultsDocument").GetComponent<UIDocument>().rootVisualElement;
        Check(root.Q(className: "results-screen").resolvedStyle.backgroundColor.a == 1f, "Results text lacks opaque background");
        foreach (var header in root.Query<VisualElement>(className: "table-header").ToList())
        foreach (var label in header.Query<Label>().ToList())
        {
            Check(label.resolvedStyle.fontSize >= 22, "Results table heading too small");
            Check(label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x <= label.contentRect.width + 2, "Results header clipped: " + label.text);
        }
        Check(root.Query<Label>(className: "stat-value").ToList().Any(x => x.text == "1000"), "Damage value not displayed");
        return "PASS: results captions 22px+, no clipped column headings, 1000 damage visible on opaque background.";
    }
}
