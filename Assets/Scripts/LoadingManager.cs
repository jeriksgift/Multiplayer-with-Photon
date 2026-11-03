using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;

    [SerializeField] private RectTransform loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private RectTransform loadingImage;
    [SerializeField] private float loadingIconRotateSpeed;
    [SerializeField] private UnityEngine.UI.Image progressFill;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private LoadingScreen loadingScreen;
    private bool loadingNetworkLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (loadingScreen)
        {
            loadingScreen.SetProgress(loadingNetworkLevel ? PhotonNetwork.LevelLoadingProgress : 0f);
            return;
        }
        if (loadingPanel.gameObject.activeSelf)
        {
            loadingImage.Rotate(new Vector3(0, 0, -1) * Time.deltaTime * loadingIconRotateSpeed);
            if (progressFill)
            {
                float progress = PhotonNetwork.LevelLoadingProgress;
                bool loadingScene = progress > 0f && progress < 1f;
                var rect = progressFill.rectTransform;
                rect.anchorMin = new Vector2(loadingScene ? 0f : Mathf.PingPong(Time.unscaledTime * .3f, .78f), 0f);
                rect.anchorMax = new Vector2(loadingScene ? progress : rect.anchorMin.x + .22f, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                if (progressText) progressText.text = loadingScene ? Mathf.RoundToInt(progress * 100) + "%" : "PLEASE WAIT";
            }
        }
    }

    public void ShowLoadingScreen(string loadingTxt)
    {
        loadingNetworkLevel = false;
        if (loadingScreen)
        {
            loadingScreen.Show(loadingTxt);
            return;
        }
        loadingPanel.gameObject.SetActive(true);
        loadingText.text = loadingTxt;
    }

    public void HideLoadingScreen()
    {
        loadingNetworkLevel = false;
        if (loadingScreen)
        {
            loadingScreen.Hide();
            return;
        }
        loadingPanel.gameObject.SetActive(false);
    }

    public void LoadNetworkLevel(string levelName)
    {
        ShowLoadingScreen("Loading...");
        loadingNetworkLevel = true;
        PhotonNetwork.LoadLevel(levelName);
    }
}
