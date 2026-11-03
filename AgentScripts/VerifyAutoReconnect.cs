using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class VerifyAutoReconnect
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static object Field(LobbyManager manager, string name) => typeof(LobbyManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
    static void Stop(LobbyManager manager) => typeof(LobbyManager).GetMethod("StopReconnect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
    static async Task Disconnect()
    {
        float scale = Time.timeScale;
        Time.timeScale = 1f;
        PhotonNetwork.Disconnect();
        for (int i = 0; i < 100 && PhotonNetwork.NetworkClientState != ClientState.Disconnected; i++) await Task.Delay(50);
        Time.timeScale = scale;
        Check(PhotonNetwork.NetworkClientState == ClientState.Disconnected, "Could not prepare disconnected state");
    }
    public static async Task<string> Run()
    {
        Check(Application.isPlaying, "Requires Lobby Play mode");
        Application.runInBackground = true;
        var manager = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
        await Disconnect(); Stop(manager);
        typeof(LobbyManager).GetField("reconnectAttempts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, 0);
        float previousScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            manager.OnDisconnected(DisconnectCause.ClientTimeout);
            var first = Field(manager, "reconnectRoutine");
            Check(first != null && manager.StatusMessage.Contains("2s"), "Retry not scheduled automatically");
            manager.OnDisconnected(DisconnectCause.ServerTimeout);
            Check(ReferenceEquals(first, Field(manager, "reconnectRoutine")), "Duplicate retry coroutine");
            await Task.Delay(2300);
            Check((int)Field(manager, "reconnectAttempts") == 1, "First automatic attempt did not run while paused");
            Check(PhotonNetwork.NetworkClientState != ClientState.Disconnected, "Retry did not start a Photon connection");
            await Disconnect();
            Check(Field(manager, "reconnectRoutine") == null, "Intentional disconnect did not cancel retries");
            manager.OnDisconnected(DisconnectCause.ClientTimeout);
            Check(manager.StatusMessage.Contains("4s"), "Second delay did not increase");
            await Task.Delay(4300);
            Check((int)Field(manager, "reconnectAttempts") == 2 && PhotonNetwork.NetworkClientState != ClientState.Disconnected, "Second automatic connection attempt failed to start");
            await Disconnect();
            manager.OnDisconnected(DisconnectCause.ClientTimeout);
            Check(manager.StatusMessage.Contains("8s"), "Third delay did not increase");
            manager.enabled = false;
            Check(Field(manager, "reconnectRoutine") == null, "Disabling manager did not stop retries");
            manager.enabled = true;
            typeof(LobbyManager).GetField("reconnectAttempts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, 20);
            manager.OnDisconnected(DisconnectCause.ClientTimeout);
            Check(manager.StatusMessage.Contains("30s"), "Retry delay is not capped");
            Stop(manager);
            string result = "PASS: simulated timeout callbacks automatically start real Photon connections after 2s then 4s, including while Time.timeScale=0; duplicate callbacks share one loop; next delay is 8s and cap is 30s; intentional disconnect and component disable cancel retries. Successful online reconnection remains unverified due to TLS/network failures.";
            File.WriteAllText("AgentScripts/AutoReconnect-validation.txt", result);
            return result;
        }
        finally { Time.timeScale = previousScale; Stop(manager); }
    }

    public static async Task<string> MatchDisconnect()
    {
        PhotonNetwork.OfflineMode = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        await Task.Delay(400);
        var game = GameManager.Instance;
        game.OnDisconnected(DisconnectCause.ClientTimeout);
        for (int i = 0; i < 50 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Lobby"; i++) await Task.Delay(100);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby", "Match disconnection did not return to lobby");
        return "PASS: simulated match connection loss safely loads Lobby; lobby owns subsequent retries.";
    }
}
