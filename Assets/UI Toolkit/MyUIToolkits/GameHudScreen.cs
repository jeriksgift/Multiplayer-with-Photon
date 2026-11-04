using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

[DefaultExecutionOrder(-10)]
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
    private WeaponSystem weapon;
    private Label ammoValue, ammoStatus;
    private MobileGameControls mobileControls;
    public bool IsPanelOpen { get; set; }
    private bool scoreboardOpen;
    private CursorLockMode previousLock;
    private bool previousVisible;
    private float nextScoreboardRefresh;

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
        GetComponent<InputManager>().Hud = this;
        mobileControls = new MobileGameControls(root, GetComponent<InputManager>());
        root.Q("ScoreArea").pickingMode = PickingMode.Position;
        root.Q("ScoreArea").RegisterCallback<PointerDownEvent>(OpenScoreboard);
        root.Q("ScoreboardOverlay").pickingMode = PickingMode.Position;
        root.Q<Button>("CloseScoreboard").pickingMode = PickingMode.Position;
        root.Q<Button>("CloseScoreboard").clicked += CloseScoreboard;
        blueScore = root.Q<Label>("BlueScore");
        redScore = root.Q<Label>("RedScore");
        timer = root.Q<Label>("MatchTimer");
        healthValue = root.Q<Label>("HealthValue");
        healthFill = root.Q("HealthFill");
        respawn = root.Q<Label>("RespawnStatus");
        ammoValue = root.Q<Label>("AmmoValue");
        ammoStatus = root.Q<Label>("AmmoStatus");
        weapon = GetComponent<WeaponSystem>();
        var ammoArea = root.Q("AmmoArea");
        ammoArea.pickingMode = PickingMode.Position;
        ammoArea.RegisterCallback<PointerDownEvent>(Reload);
        if (weapon) weapon.AmmoChanged += SetAmmo;
        SetAmmo();
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
    private void Reload(PointerDownEvent evt) { if (weapon) weapon.Reload(); evt.StopPropagation(); }
    private void Update()
    {
        mobileControls?.Tick();
        if (scoreboardOpen && Time.unscaledTime >= nextScoreboardRefresh) { RefreshScoreboard(); nextScoreboardRefresh = Time.unscaledTime + .25f; }
    }
    private void OpenScoreboard(PointerDownEvent evt)
    {
        evt.StopPropagation();
        if (IsPanelOpen || (menu && menu.IsExitOpen)) return;
        previousLock = Cursor.lockState; previousVisible = Cursor.visible;
        scoreboardOpen = IsPanelOpen = true;
        mobileControls.Reset();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        root.Q("ScoreboardOverlay").RemoveFromClassList("hud-hidden");
        RefreshScoreboard();
    }
    public void CloseScoreboard()
    {
        if (!scoreboardOpen) return;
        scoreboardOpen = IsPanelOpen = false;
        root.Q("ScoreboardOverlay").AddToClassList("hud-hidden");
        Cursor.lockState = previousLock; Cursor.visible = previousVisible;
    }
    private void RefreshScoreboard()
    {
        var rows = root.Q<ScrollView>("ScoreboardRows"); rows.Clear();
        var game = GameManager.Instance;
        foreach (var team in new[] { DataTypes.Team.Blue, DataTypes.Team.Red })
        {
            var heading = new Label(team.ToString().ToUpperInvariant() + " TEAM     KILLS / DAMAGE"); heading.AddToClassList("scoreboard-heading"); rows.Add(heading);
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (RoomManager.GetTeam(player) != team) continue;
                int kills = 0; float damage = 0;
                if (game) { game.playerKills.TryGetValue(player.ActorNumber, out kills); game.playerDamage.TryGetValue(player.ActorNumber, out damage); }
                var row = new VisualElement(); row.AddToClassList("scoreboard-row");
                var name = new Label(player.NickName) { enableRichText = false }; name.AddToClassList("scoreboard-name");
                row.Add(name); row.Add(new Label(kills + " / " + Mathf.FloorToInt(damage))); rows.Add(row);
            }
        }
    }
    private void OnApplicationFocus(bool focused) { if (!focused) mobileControls?.Reset(); }
    private void OnApplicationPause(bool paused) { if (paused) mobileControls?.Reset(); }
    private void OnDisable() { mobileControls?.Reset(); }
    private void SetAmmo()
    {
        if (!weapon) return;
        ammoValue.text = weapon.AmmoInMagazine.ToString("00") + " / " + WeaponSystem.MagazineSize.ToString("00");
        ammoStatus.text = weapon.IsReloading ? "RELOADING..." : Application.isMobilePlatform ? "TAP / RELOAD" : "R / RELOAD";
        ammoValue.EnableInClassList("ammo-empty", weapon.AmmoInMagazine == 0);
    }
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
        mobileControls?.Dispose();
        root?.Q("AmmoArea")?.UnregisterCallback<PointerDownEvent>(Reload);
        if (!subscribed) return;
        if (health) health.HealthChanged -= SetHealth;
        if (weapon) weapon.AmmoChanged -= SetAmmo;
        GameManager.OnScoreChanged -= SetScores;
        GameManager.OnTimerChanged -= SetTimer;
        GameManager.OnMatchEnded -= EndMatch;
        if (menuButton != null) menuButton.clicked -= OpenMenu;
    }
}
