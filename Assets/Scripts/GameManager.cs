using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Collections;
using ExitGames.Client.Photon;

public class GameManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static GameManager Instance;

    public static System.Action<int, int> OnScoreChanged;
    public static System.Action<float> OnTimerChanged;
    public static System.Action<int, string> OnMatchEnded; // winner, playerStats string

    [SerializeField] private GameObject playerPrefab;

    public int blueKills = 0;
    public int redKills = 0;

    // Per-player stats
    public System.Collections.Generic.Dictionary<int, int> playerKills = new System.Collections.Generic.Dictionary<int, int>();
    public System.Collections.Generic.Dictionary<int, float> playerDamage = new System.Collections.Generic.Dictionary<int, float>();

    public float matchTime = 1200f; // 20 minutes
    public float remainingTime = 1200f;
    public bool matchEnded = false;
    public const int WIN_SCORE = 10;

    private const byte KILL_EVENT = 1;
    private const byte DAMAGE_EVENT = 2;

    [Header("Spawn Points")]
    [Tooltip("Four points forming the Blue team's line.")]
    [SerializeField] private Transform[] blueSpawnPoints;

    [Tooltip("Four points forming the Red team's line, facing the Blue line.")]
    [SerializeField] private Transform[] redSpawnPoints;

    private bool timerRunning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (matchEnded) return;

        if (photonEvent.Code == KILL_EVENT)
        {
            int killerActorNumber = (int)photonEvent.CustomData;
            ProcessKill(killerActorNumber);
        }
        else if (photonEvent.Code == DAMAGE_EVENT)
        {
            var data = (object[])photonEvent.CustomData;
            int shooterActorNumber = (int)data[0];
            float damage = (float)data[1];
            if (!playerDamage.ContainsKey(shooterActorNumber))
                playerDamage[shooterActorNumber] = 0f;
            playerDamage[shooterActorNumber] += damage;
        }
    }

    private void Start()
    {
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.HideLoadingScreen();
        }

        if (!PhotonNetwork.InRoom) return;

        if (PhotonNetwork.IsMasterClient)
        {
            blueKills = 0;
            redKills = 0;
            remainingTime = matchTime;
            timerRunning = true;
            StartCoroutine(MasterTimerLoop());
            SyncScoresToRoom();
        }

        if (TryGetSpawnLoc(RoomManager.GetLocalTeam(), RoomManager.GetLocalTeamIndex(), out Transform spawn))
        {
            PhotonNetwork.Instantiate(playerPrefab.name, spawn.position, spawn.rotation);
        }
        else
        {
            Debug.LogWarning("No free spawn point available, spawning at origin.");
            PhotonNetwork.Instantiate(playerPrefab.name, new Vector3(0f, 2f, 0f), Quaternion.identity);
        }
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("blueKills"))
            blueKills = (int)propertiesThatChanged["blueKills"];
        if (propertiesThatChanged.ContainsKey("redKills"))
            redKills = (int)propertiesThatChanged["redKills"];
        if (propertiesThatChanged.ContainsKey("remainingTime"))
            remainingTime = (float)propertiesThatChanged["remainingTime"];
        if (propertiesThatChanged.ContainsKey("matchEnded"))
        {
            matchEnded = (bool)propertiesThatChanged["matchEnded"];
            if (matchEnded && propertiesThatChanged.ContainsKey("winner"))
            {
                int winner = (int)propertiesThatChanged["winner"];
                string stats = propertiesThatChanged.ContainsKey("playerStats") ? (string)propertiesThatChanged["playerStats"] : "";
                OnScoreChanged?.Invoke(blueKills, redKills);
                OnMatchEnded?.Invoke(winner, stats);
                return;
            }
        }

        OnScoreChanged?.Invoke(blueKills, redKills);
        OnTimerChanged?.Invoke(remainingTime);
    }

    private void SyncScoresToRoom()
    {
        var props = new ExitGames.Client.Photon.Hashtable();
        props["blueKills"] = blueKills;
        props["redKills"] = redKills;
        props["remainingTime"] = remainingTime;
        props["matchEnded"] = matchEnded;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    private IEnumerator MasterTimerLoop()
    {
        while (timerRunning && !matchEnded)
        {
            yield return new WaitForSeconds(1f);
            remainingTime -= 1f;
            if (remainingTime < 0f) remainingTime = 0f;

            var props = new ExitGames.Client.Photon.Hashtable();
            props["remainingTime"] = remainingTime;
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);

            OnTimerChanged?.Invoke(remainingTime);

            // Check win conditions
            if (blueKills >= WIN_SCORE)
            {
                EndMatch(1);
            }
            else if (redKills >= WIN_SCORE)
            {
                EndMatch(2);
            }
            else if (remainingTime <= 0f)
            {
                if (blueKills > redKills) EndMatch(1);
                else if (redKills > blueKills) EndMatch(2);
                else EndMatch(0);
            }
        }
    }

    private void EndMatch(int winner)
    {
        if (matchEnded) return;
        matchEnded = true;
        timerRunning = false;

        // Build player stats string: "actor:team:kills:damage;..."
        var statsBuilder = new System.Text.StringBuilder();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            int actor = player.ActorNumber;
            int kills = playerKills.ContainsKey(actor) ? playerKills[actor] : 0;
            float dmg = playerDamage.ContainsKey(actor) ? playerDamage[actor] : 0f;
            var team = RoomManager.GetTeam(player);
            int teamInt = (team == DataTypes.Team.Blue) ? 0 : 1;
            if (statsBuilder.Length > 0) statsBuilder.Append(";");
            statsBuilder.Append(actor + ":" + teamInt + ":" + kills + ":" + dmg);
        }

        var props = new ExitGames.Client.Photon.Hashtable();
        props["matchEnded"] = true;
        props["winner"] = winner;
        props["blueKills"] = blueKills;
        props["redKills"] = redKills;
        props["playerStats"] = statsBuilder.ToString();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        // Build stats string for local call
        var statsBuilder2 = new System.Text.StringBuilder();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            int actor = player.ActorNumber;
            int kills = playerKills.ContainsKey(actor) ? playerKills[actor] : 0;
            float dmg = playerDamage.ContainsKey(actor) ? playerDamage[actor] : 0f;
            var team = RoomManager.GetTeam(player);
            int teamInt = (team == DataTypes.Team.Blue) ? 0 : 1;
            if (statsBuilder2.Length > 0) statsBuilder2.Append(";");
            statsBuilder2.Append(actor + ":" + teamInt + ":" + kills + ":" + dmg);
        }
        OnMatchEnded?.Invoke(winner, statsBuilder2.ToString());
    }

    private void ProcessKill(int killerActorNumber)
    {
        if (matchEnded) return;

        if (!playerKills.ContainsKey(killerActorNumber))
            playerKills[killerActorNumber] = 0;
        playerKills[killerActorNumber]++;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber != killerActorNumber) continue;
            var team = RoomManager.GetTeam(player);
            if (team == DataTypes.Team.Blue) blueKills++;
            else redKills++;
            break;
        }

        SyncScoresToRoom();
        OnScoreChanged?.Invoke(blueKills, redKills);

        // Check win condition immediately
        if (blueKills >= WIN_SCORE)
            EndMatch(1);
        else if (redKills >= WIN_SCORE)
            EndMatch(2);
    }

    public static void AddKill(int killerActorNumber)
    {
        if (Instance == null) return;
        if (Instance.matchEnded) return;

        if (PhotonNetwork.IsMasterClient)
        {
            Instance.ProcessKill(killerActorNumber);
        }
        else
        {
            // Send kill event to master client
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
            PhotonNetwork.RaiseEvent(KILL_EVENT, killerActorNumber, options, SendOptions.SendReliable);
        }
    }

    public bool TryGetSpawnLoc(DataTypes.Team team, int preferredIndex, out Transform spawn)
    {
        spawn = null;

        Transform[] points = team == DataTypes.Team.Blue ? blueSpawnPoints : redSpawnPoints;
        if (points == null || points.Length == 0) return false;

        bool[] taken = GetTakenFlags(points.Length);

        if (preferredIndex >= 0 && preferredIndex < points.Length && !taken[preferredIndex])
        {
            spawn = points[preferredIndex];
            return true;
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (taken[i]) continue;
            spawn = points[i];
            return true;
        }

        return false;
    }

    public Transform GetSpawnLoc()
    {
        return TryGetSpawnLoc(RoomManager.GetLocalTeam(), RoomManager.GetLocalTeamIndex(), out Transform spawn)
            ? spawn
            : null;
    }

    private bool[] GetTakenFlags(int count)
    {
        var taken = new bool[count];

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player == null || player.IsLocal) continue;

            int index = RoomManager.GetTeamIndex(player);
            if (index >= 0 && index < count) taken[index] = true;
        }

        return taken;
    }
}