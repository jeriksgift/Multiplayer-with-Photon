using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

public static class VerifyAmmo
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static async Task Fire()
    {
        InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left));
        await Task.Delay(45);
        InputSystem.QueueStateEvent(Mouse.current, new MouseState());
        await Task.Delay(45);
    }
    static async Task ReloadKey()
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.R));
        await Task.Delay(50);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        await Task.Delay(50);
    }
    public static async Task<string> Prepare()
    {
        var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyManager>();
        if (lobby) lobby.enabled = false;
        PhotonNetwork.Disconnect();
        for (int i = 0; i < 300 && PhotonNetwork.NetworkClientState != Photon.Realtime.ClientState.Disconnected; i++) await Task.Delay(50);
        Check(PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Disconnected, "Cannot prepare offline test while connected");
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("AMMO-VERIFICATION");
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomManager.KeyTeam, 0 }, { RoomManager.KeyTeamIndex, 0 } });
        UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        await Task.Delay(600);
        return "Offline ammo test match ready";
    }
    public static async Task<string> Run()
    {
        Check(Application.isPlaying && PhotonNetwork.InRoom && !GameManager.Instance.matchEnded, "Requires active match");
        Application.runInBackground = true;
        var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.EnableDevice(Keyboard.current); InputSystem.EnableDevice(Mouse.current);
        string report = "";
        try
        {
            foreach (var team in new[] { DataTypes.Team.Blue, DataTypes.Team.Red })
            {
                var player = UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().SingleOrDefault(x => x.photonView.IsMine);
                if (team == DataTypes.Team.Red)
                {
                    PhotonNetwork.Destroy(player.gameObject);
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomManager.KeyTeam, 1 }, { RoomManager.KeyTeamIndex, 1 } });
                    var spawn = GameManager.Instance.GetSpawnLoc();
                    player = PhotonNetwork.Instantiate("PlayerWithGFX_Red", spawn.position, spawn.rotation).GetComponent<PlayerWithGFXPresentation>();
                    await Task.Delay(400);
                }
                var weapon = player.GetComponent<WeaponSystem>();
                var root = player.transform.Find("GameHudDocument").GetComponent<UIDocument>().rootVisualElement;
                var status = root.Q<Label>("AmmoStatus"); var value = root.Q<Label>("AmmoValue");
                Check(weapon.AmmoInMagazine == 7 && value.text == "07 / 07", "Initial magazine/HUD incorrect");
                await ReloadKey(); Check(!weapon.IsReloading, "Full magazine can reload");
                int shots = player.Shots;
                for (int i = 0; i < 7; i++) await Fire();
                Check(weapon.AmmoInMagazine == 0 && weapon.IsReloading && player.Shots == shots + 7, "Seven shots did not empty/start reload");
                Check(value.text == "00 / 07" && root.Q("ReloadIcon").ClassListContains("reload-active"), "Reload HUD not live");
                await Fire(); Check(player.Shots == shots + 7 && weapon.AmmoInMagazine == 0, "Eighth shot fired during reload");
                await Task.Delay(1600);
                Check(weapon.AmmoInMagazine == 7 && !weapon.IsReloading && value.text == "07 / 07", "Automatic reload failed");
                await Fire(); await Fire(); Check(weapon.AmmoInMagazine == 5, "Magazine did not decrement again");
                await ReloadKey(); Check(weapon.IsReloading && weapon.AmmoInMagazine == 5, "Manual R reload failed");
                shots = player.Shots;
                await Fire(); Check(player.Shots == shots, "Fired during manual reload");
                await Task.Delay(1600); Check(weapon.AmmoInMagazine == 7, "Unlimited reserve refill failed");
                var menu = player.GetComponent<ToysGameUI>(); menu.ShowExit();
                await Fire(); await ReloadKey(); Check(player.Shots == shots && !weapon.IsReloading, "Ammo input through menu");
                menu.CancelExit();
                await Fire(); Check(weapon.AmmoInMagazine == 6, "Firing after menu failed");
                player.GetComponent<HealthComponent>().Damage(100, player.transform.position, Vector3.up, -1);
                await Task.Delay(2300);
                Check(weapon.AmmoInMagazine == 7 && !weapon.IsReloading && value.text == "07 / 07", "Respawn did not reset magazine/HUD");
                report += team + ": 7-round limit, no eighth shot, automatic and R reload, unlimited refill, blocked reload/fire during menu, shot effects and respawn reset passed.\n";
                if (team == DataTypes.Team.Red)
                {
                    await Fire(); await Fire();
                    ScreenCapture.CaptureScreenshot("AgentScripts/AmmoHUD-Verified.png");
                    await Task.Delay(150);
                }
            }
            File.WriteAllText("AgentScripts/Ammo-validation.txt", report);
            return report;
        }
        finally
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
        }
    }
}
