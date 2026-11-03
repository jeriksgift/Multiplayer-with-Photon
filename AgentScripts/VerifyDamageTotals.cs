using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class VerifyDamageTotals
{
    sealed class Shooter : Player { public Shooter() : base("DAMAGE TEST", 9, false) {} }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task<string> Run()
    {
        Check(Application.isPlaying && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient, "Requires offline match");
        Application.runInBackground = true;
        string report = "";
        var shooter = new Shooter();
        PhotonNetwork.CurrentRoom.StorePlayer(shooter);
        foreach (bool overkill in new[] { false, true })
        {
            int victimTeam = overkill ? 1 : 0;
            shooter.CustomProperties[RoomManager.KeyTeam] = 1 - victimTeam;
            shooter.CustomProperties[RoomManager.KeyTeamIndex] = 1;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { RoomManager.KeyTeam, victimTeam }, { RoomManager.KeyTeamIndex, 0 } });
            SceneManager.LoadScene("Game"); await Task.Delay(500);
            var player = UnityEngine.Object.FindObjectsByType<PlayerUI>().Single(x => x.photonView.IsMine);
            var health = player.GetComponent<HealthComponent>();
            var game = GameManager.Instance;
            string finalStats = null;
            Action<int, string> capture = (winner, stats) => {
                finalStats = stats;
                Check(game.playerDamage[9] == 1000f, "Results published before final damage");
            };
            GameManager.OnMatchEnded += capture;
            try
            {
                for (int life = 0; life < 10; life++)
                {
                    if (life > 0) health.ApplyLifeState(health.LifeRevision + 1, false, player.transform.position, player.transform.rotation);
                    int hits = overkill ? 4 : 5;
                    for (int hit = 0; hit < hits; hit++)
                    {
                        if (!overkill && life == 9 && hit == 4)
                            Check(game.playerDamage[9] == 980f && !game.matchEnded, "Expected 980 before final 20-damage hit");
                        health.Damage(overkill ? 30f : 20f, player.transform.position, Vector3.up, 9);
                    }
                    Check(health.IsDead && game.playerKills[9] == life + 1, "Kill count incorrect or doubled");
                }
                Check(game.matchEnded && game.playerDamage[9] == 1000f, "Ten kills did not record 1000 actual damage");
                Check(finalStats != null && finalStats.Split(';').Any(x => x == "9:" + (1 - victimTeam) + ":10:1000"), "Serialized final stats omit damage");
                var result = player.transform.Find("MatchResultsDocument").GetComponent<UIDocument>();
                var list = result.rootVisualElement.Q(victimTeam == 0 ? "RedPlayers" : "BluePlayers");
                var row = list.Children().Single(x => x.Q<Label>(className: "player-name")?.text == "DAMAGE TEST");
                Check(row.Query<Label>(className: "stat-value").ToList()[1].text == "1000", "Results damage label wrong");
                Check((string)PhotonNetwork.CurrentRoom.CustomProperties["playerStats"] == finalStats, "Host and room stats differ");
                float before = game.playerDamage[9];
                health.Damage(100f, player.transform.position, Vector3.up, 9);
                Check(game.playerDamage[9] == before, "Dead target awarded extra damage");
                report += overkill ? "PASS: Blue winner, 10 kills with 30-damage hits count 1000 actual health removed; overkill excluded.\n" : "PASS: Red winner, final hit changes 980 to 1000 before match completion; 10 kills and results label 1000.\n";
            }
            finally { GameManager.OnMatchEnded -= capture; }
        }

        SceneManager.LoadScene("Game"); await Task.Delay(400);
        var master = GameManager.Instance;
        master.blueKills = 0; master.redKills = 9;
        shooter.CustomProperties[RoomManager.KeyTeam] = 1;
        master.playerKills[9] = 9; master.playerDamage[9] = 980f;
        var evt = new EventData { Code = 2 };
        evt.Parameters[ParameterCode.Data] = new object[] { 9, 20f, true };
        master.OnEvent(evt);
        Check(master.matchEnded && master.playerKills[9] == 10 && master.playerDamage[9] == 1000f, "Received combined hit did not record final damage before kill");
        Check(((string)PhotonNetwork.CurrentRoom.CustomProperties["playerStats"]).Contains("9:1:10:1000"), "Remote event final stats incorrect");
        report += "PASS: simulated remote combined damage/kill event records final damage before ending match; host and synchronized result snapshots agree.\n";

        SceneManager.LoadScene("Game"); await Task.Delay(400);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            GameManager.Instance.redKills = 9;
            GameManager.RecordHit(9, 12.5f, true);
            Check(((string)PhotonNetwork.CurrentRoom.CustomProperties["playerStats"]).Contains("12.5"), "Damage snapshot uses locale comma");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        report += "PASS: fractional damage serialized with invariant decimal separator.\n";
        File.WriteAllText("AgentScripts/DamageTotals-validation.txt", report);
        return report;
    }
}
