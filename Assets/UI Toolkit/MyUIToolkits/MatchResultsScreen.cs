using System.Globalization;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public sealed class MatchResultsScreen : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    private Button returnButton;

    public void Show(int winner, string stats)
    {
        var view = GetComponent<PhotonView>();
        if (view && PhotonNetwork.InRoom && !view.IsMine) return;
        var menu = GetComponent<ToysGameUI>();
        if (menu && menu.IsExitOpen) menu.CancelExit();
        document.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var root = document.rootVisualElement;
        root.Q<Label>("WinnerTitle").text = winner == 1 ? "Blue Team Wins!" : winner == 2 ? "Red Team Wins!" : "Draw!";
        root.Q<Label>("ResultRoom").text = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "Toys Death Match";
        root.Q<Label>("ResultSubtitle").text = winner == 0 ? "Evenly matched. See you in the next round." : "Match complete. Here's how everyone played.";
        root.Q("BlueCard").EnableInClassList("winning-team", winner == 1);
        root.Q("RedCard").EnableInClassList("winning-team", winner == 2);
        root.Q<Label>("BlueStatus").text = winner == 1 ? "WINNER" : winner == 0 ? "DRAW" : "FINAL SCORE";
        root.Q<Label>("RedStatus").text = winner == 2 ? "WINNER" : winner == 0 ? "DRAW" : "FINAL SCORE";
        var game = GameManager.Instance;
        root.Q<Label>("BlueFinalScore").text = (game ? game.blueKills : 0).ToString("00");
        root.Q<Label>("RedFinalScore").text = (game ? game.redKills : 0).ToString("00");
        var blue = root.Q("BluePlayers");
        var red = root.Q("RedPlayers");
        blue.Clear(); red.Clear();
        foreach (var entry in (stats ?? "").Split(';'))
        {
            var parts = entry.Split(':');
            if (parts.Length != 4 || !int.TryParse(parts[0], out int actor) ||
                !int.TryParse(parts[1], out int team) || !int.TryParse(parts[2], out int kills) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float damage) ||
                (team != 0 && team != 1)) continue;
            var player = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actor);
            var row = new VisualElement();
            row.AddToClassList("result-row");
            bool local = PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.ActorNumber == actor;
            row.EnableInClassList("result-local", local);
            string nick = player != null && !string.IsNullOrWhiteSpace(player.NickName) ? player.NickName : "Player " + actor;
            var name = new Label(nick + (local ? " (YOU)" : ""));
            name.AddToClassList("player-name");
            name.tooltip = nick;
            row.Add(name);
            var killLabel = new Label(kills.ToString()); killLabel.AddToClassList("stat-value"); row.Add(killLabel);
            var damageLabel = new Label(Mathf.FloorToInt(damage).ToString()); damageLabel.AddToClassList("stat-value"); row.Add(damageLabel);
            (team == 0 ? blue : red).Add(row);
        }
        foreach (var list in new[] { blue, red })
            if (list.childCount == 0) { var empty = new Label("No player stats recorded."); empty.AddToClassList("empty-stats"); list.Add(empty); }
        if (returnButton != null) returnButton.clicked -= ReturnToLobby;
        returnButton = root.Q<Button>("ReturnToLobbyButton");
        returnButton.SetEnabled(true);
        returnButton.text = "Return to Lobby";
        returnButton.clicked += ReturnToLobby;
        returnButton.schedule.Execute(() => returnButton.Focus());
    }

    private void ReturnToLobby()
    {
        returnButton.SetEnabled(false);
        returnButton.text = "Returning...";
        GetComponent<PlayerUI>().ExitToLobby();
    }

    private void OnDestroy()
    {
        if (returnButton != null) returnButton.clicked -= ReturnToLobby;
    }
}
