using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class GameHudScreen : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    private HealthComponent health;
    private ToysGameUI menu;
    private VisualElement root;
    private Button menuButton;
    private Label blueScore, redScore, timer, healthValue, respawn;
    private VisualElement healthFill;
    private bool subscribed;

    private void Start()
    {
        var view = GetComponent<PhotonView>();
        bool local = view && (view.IsMine || (!PhotonNetwork.InRoom && view.ViewID == 0));
        if (!local) { document.gameObject.SetActive(false); enabled = false; return; }
        document.gameObject.SetActive(true);
        root = document.rootVisualElement;
        root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        root.pickingMode = PickingMode.Ignore;
        menuButton = root.Q<Button>("MenuButton");
        menuButton.pickingMode = PickingMode.Position;
        menu = GetComponent<ToysGameUI>();
        menuButton.clicked += OpenMenu;
        blueScore = root.Q<Label>("BlueScore");
        redScore = root.Q<Label>("RedScore");
        timer = root.Q<Label>("MatchTimer");
        healthValue = root.Q<Label>("HealthValue");
        healthFill = root.Q("HealthFill");
        respawn = root.Q<Label>("RespawnStatus");
        root.Q<Label>("RoomName").text = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "Toys Death Match";
        root.Q<Label>("LocalTeam").text = RoomManager.GetLocalTeam() == DataTypes.Team.Blue ? "BLUE TEAM / DEATHMATCH" : "RED TEAM / DEATHMATCH";
        root.Q<Label>("ScoreTarget").text = "FIRST TO " + GameManager.WIN_SCORE;
        health = GetComponent<HealthComponent>();
        if (health) health.HealthChanged += SetHealth;
        GameManager.OnScoreChanged += SetScores;
        GameManager.OnTimerChanged += SetTimer;
        GameManager.OnMatchEnded += EndMatch;
        subscribed = true;
        SetHealth(1f);
        var game = GameManager.Instance;
        SetScores(game ? game.blueKills : 0, game ? game.redKills : 0);
        SetTimer(game ? game.remainingTime : 0f);
        if (game && game.matchEnded) document.gameObject.SetActive(false);
    }

    private void OpenMenu() { if (menu) menu.ToggleExit(); }
    private void SetScores(int blue, int red) { blueScore.text = blue.ToString(); redScore.text = red.ToString(); }
    private void SetTimer(float seconds)
    {
        int value = Mathf.Max(0, Mathf.FloorToInt(seconds));
        timer.text = (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
    }
    private void SetHealth(float value)
    {
        value = Mathf.Clamp01(value);
        healthValue.text = Mathf.RoundToInt(value * 100f).ToString();
        healthFill.style.width = Length.Percent(value * 100f);
        respawn.EnableInClassList("hud-hidden", value > 0f);
    }
    private void EndMatch(int winner, string stats) { document.gameObject.SetActive(false); }
    private void OnDestroy()
    {
        if (!subscribed) return;
        if (health) health.HealthChanged -= SetHealth;
        GameManager.OnScoreChanged -= SetScores;
        GameManager.OnTimerChanged -= SetTimer;
        GameManager.OnMatchEnded -= EndMatch;
        if (menuButton != null) menuButton.clicked -= OpenMenu;
    }
}
