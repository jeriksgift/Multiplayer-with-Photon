using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MatchLobbyReturn : MonoBehaviour
{
    private static MatchLobbyReturn active;

    public static void Begin()
    {
        if (active) return;
        active = new GameObject("Match Lobby Return").AddComponent<MatchLobbyReturn>();
        DontDestroyOnLoad(active.gameObject);
        active.StartCoroutine(active.Return());
    }

    private static bool ReadyForLobby => PhotonNetwork.OfflineMode ||
        PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer ||
        PhotonNetwork.NetworkClientState == ClientState.JoinedLobby ||
        PhotonNetwork.NetworkClientState == ClientState.Disconnected ||
        PhotonNetwork.NetworkClientState == ClientState.PeerCreated;

    private IEnumerator Return()
    {
        yield return null;
        // Photon removes the player during LeaveRoom, so this operation lives independently.
        if (PhotonNetwork.InRoom && !PhotonNetwork.LeaveRoom(false)) PhotonNetwork.Disconnect();
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((PhotonNetwork.InRoom || !ReadyForLobby) && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (PhotonNetwork.InRoom || !ReadyForLobby)
        {
            PhotonNetwork.Disconnect();
            while (PhotonNetwork.NetworkClientState != ClientState.Disconnected &&
                   PhotonNetwork.NetworkClientState != ClientState.PeerCreated)
                yield return null;
        }

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        SceneManager.LoadScene("Lobby");
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
    }
}
