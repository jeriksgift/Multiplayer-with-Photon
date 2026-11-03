using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomSlot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Color occupiedTextColor = Color.white;

    [Header("Team")]
    [SerializeField] private DataTypes.Team team;

    [Header("Colours")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color emptyTextColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color blueColor = new Color(0.25f, 0.55f, 1f, 0.85f);
    [SerializeField] private Color redColor = new Color(1f, 0.30f, 0.30f, 0.85f);
    [SerializeField] private Color readyColor = new Color(0.30f, 0.95f, 0.45f, 0.9f);

    /// <summary>Team this slot belongs to. Assigned per GameObject in the Lobby scene.</summary>
    public DataTypes.Team Team => team;

    public bool isOccupied { get; private set; }
    public int occupantActor { get; private set; } = -1;
    public bool occupantIsReady { get; private set; }
    public bool isMine { get; private set; }

    private string playerName = string.Empty;
    public string PlayerName => playerName;

    private void Awake()
    {
        if (background == null)
        {
            background = GetComponent<Image>();
        }
    }

    public void SetEmpty()
    {
        isOccupied = false;
        occupantActor = -1;
        occupantIsReady = false;
        isMine = false;
        playerName = string.Empty;
        ApplyVisual();
    }

    public void SetOccupied(string name, int actor, bool ready, bool mine)
    {
        isOccupied = true;
        playerName = name;
        occupantActor = actor;
        occupantIsReady = ready;
        isMine = mine;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (playerNameText != null)
        {
            playerNameText.text = isOccupied
                ? playerName + (isMine ? " (You)" : string.Empty)
                : "OPEN SLOT";
            playerNameText.color = isOccupied ? occupiedTextColor : emptyTextColor;
        }
        if (statusText)
        {
            statusText.text = !isOccupied ? "CLICK TO JOIN" : occupantIsReady ? "READY" : "NOT READY";
            statusText.color = occupantIsReady ? new Color(.58f, .79f, .49f) : emptyTextColor;
        }

        if (background == null) return;

        if (!isOccupied)
        {
            background.color = emptyColor;
        }
        else if (occupantIsReady)
        {
            background.color = readyColor;
        }
        else
        {
            background.color = team == DataTypes.Team.Blue ? blueColor : redColor;
        }
    }
}
