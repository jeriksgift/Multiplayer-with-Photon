using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyTeamPlayers
{
    sealed class Opponent : Player
    {
        public Opponent() : base("Verification opponent", 2, false) { }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static async Task<string> Run()
    {
        Check(Application.isPlaying, "Requires Play mode in Game scene");
        int firstFrame = Time.frameCount;
        await Task.Delay(100);
        Check(Time.frameCount > firstFrame, "Game loop frozen");
        Check(!UnityEngine.Object.FindObjectsByType<PlayerWithGFXPresentation>().Any(), "A preview player is still active");
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("TeamVerification");
        await Task.Delay(100);
        Check(PhotonNetwork.InRoom, "Offline test room was not created");
        var opponent = new Opponent();
        PhotonNetwork.CurrentRoom.StorePlayer(opponent);
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Verification ground";
        plane.transform.position = new Vector3(0, 100, 0);
        plane.transform.localScale = Vector3.one * 8;
        var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.EnableDevice(Keyboard.current);
        InputSystem.EnableDevice(Mouse.current);
        string report = "";
        GameObject local = null, remote = null, holder = null;
        try
        {
            foreach (var team in new[] { DataTypes.Team.Red, DataTypes.Team.Blue })
            {
                PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
                    {RoomManager.KeyTeam, (int)team}, {RoomManager.KeyTeamIndex, 0}, {RoomManager.KeySlot, 0} });
                opponent.CustomProperties[RoomManager.KeyTeam] = (int)(team == DataTypes.Team.Red ? DataTypes.Team.Blue : DataTypes.Team.Red);
                opponent.CustomProperties[RoomManager.KeyTeamIndex] = 0;
                Check(GameManager.Instance.TryGetSpawnLoc(team, 0, out var spawn), "No team spawn");
                var configured = new UnityEditor.SerializedObject(GameManager.Instance).FindProperty(team == DataTypes.Team.Red ? "redSpawnPoints" : "blueSpawnPoints").GetArrayElementAtIndex(0).objectReferenceValue;
                Check(spawn == configured, "Opposing slot zero blocks preferred team spawn");
                string prefab = team == DataTypes.Team.Red ? "PlayerWithGFX_Red" : "PlayerWithGFX";
                local = PhotonNetwork.Instantiate(prefab, new Vector3(0, 101, 0), Quaternion.identity);
                await Task.Delay(500);
                var p = local.GetComponent<PlayerWithGFXPresentation>();
                Check(p.photonView.IsMine && p.playerCamera && p.playerCamera.enabled, "Local camera/ownership failed");
                Check(InputManager.Instance == local.GetComponent<InputManager>(), "Wrong local input singleton");
                // Spawn a remote avatar after the owner to catch late-arriving input/camera theft.
                holder = new GameObject("Inactive remote setup");
                holder.SetActive(false);
                remote = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(prefab), holder.transform);
                remote.transform.position = new Vector3(10, 100, 0);
                var view = remote.GetComponent<PhotonView>();
                view.ViewID = 2001;
                view.OwnerActorNr = 2;
                view.ControllerActorNr = 2;
                holder.SetActive(true);
                await Task.Delay(150);
                Check(!remote.GetComponent<PlayerInput>().enabled && !remote.GetComponent<InputManager>().enabled, "Remote consumes input");
                Check(remote.GetComponent<Rigidbody>().isKinematic && !remote.GetComponent<Rigidbody>().useGravity, "Remote simulates physics");
                Check(!remote.GetComponentInChildren<Camera>(true).gameObject.activeInHierarchy, "Remote camera active");
                Check(InputManager.Instance == local.GetComponent<InputManager>(), "Remote stole input singleton");
                Cursor.lockState = CursorLockMode.Locked;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
                var start = local.transform.position;
                await Task.Delay(350);
                float walk = Vector3.Distance(start, local.transform.position);
                Check(walk > .5f, "Walking failed: " + walk);
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W, Key.LeftShift));
                start = local.transform.position;
                await Task.Delay(350);
                float run = Vector3.Distance(start, local.transform.position);
                Check(run > walk * 1.3f, "Sprinting failed");
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                await Task.Delay(150);
                float groundY = local.transform.position.y;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Space));
                await Task.Delay(150);
                float rise = local.transform.position.y - groundY;
                Check(rise > .3f, "Jump failed: " + rise);
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                int shots = p.Shots;
                InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left));
                await Task.Delay(80);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState());
                Check(p.Shots > shots, "Shooting failed");
                var health = local.GetComponent<HealthComponent>();
                health.Damage(100, local.transform.position, Vector3.up, 2);
                Check(health.IsDead, "Damage/death failed");
                Check(!local.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.enabled), "Dead model remains visible");
                Check(p.body.linearVelocity.sqrMagnitude < .001f, "Dead player retains velocity");
                await Task.Delay(2100);
                Check(!health.IsDead && health.LifeRevision == 2, "Owner failed to respawn");
                Check(Vector3.Distance(local.transform.position, spawn.position) < .5f, "Respawn used wrong location: " + local.transform.position + " expected " + spawn.position);
                // Deliver transition, then a delayed packet from the dead life.
                var sync = remote.GetComponent<PlayerNetworkSync>();
                var rh = remote.GetComponent<HealthComponent>();
                double now = PhotonNetwork.Time;
                Vector3 deadPosition = new Vector3(10, 100, 0);
                Vector3 respawnPosition = new Vector3(20, 100, 0);
                sync.ReceiveSnapshot(1, true, deadPosition, Quaternion.identity, Vector3.zero, now);
                Check(rh.IsDead && !remote.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.enabled), "Remote death visibility failed");
                sync.ReceiveSnapshot(2, false, respawnPosition, Quaternion.Euler(0, 180, 0), Vector3.zero, now + .01);
                Check(!rh.IsDead && Vector3.Distance(remote.transform.position, respawnPosition) < .001f, "Remote visible before teleport");
                sync.ReceiveSnapshot(1, true, deadPosition, Quaternion.identity, Vector3.zero, now + .02);
                await Task.Delay(150);
                Check(!rh.IsDead && Vector3.Distance(remote.transform.position, respawnPosition) < .001f, "Stale packet moved player back to corpse");
                Check(remote.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r => r.enabled), "Remote skin did not restore");
                Vector3 movingPosition = respawnPosition + Vector3.forward * 2;
                sync.ReceiveSnapshot(2, false, movingPosition, Quaternion.identity, Vector3.zero, PhotonNetwork.Time);
                await Task.Delay(300);
                Check(Vector3.Distance(remote.transform.position, movingPosition) < .1f, "Remote motion interpolation failed");
                var stream = new PhotonStream(true, new object[0]);
                typeof(PhotonStream).GetMethod("SetWriteStream", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(stream, new object[] { new System.Collections.Generic.List<object>(), 0 });
                local.GetComponent<PlayerNetworkSync>().OnPhotonSerializeView(stream, default(PhotonMessageInfo));
                Check(stream.ToArray().Length == 5, "Snapshot wire format invalid");
                report += team + ": walk=" + walk.ToString("F2") + ", sprint=" + run.ToString("F2") + ", jump=" + rise.ToString("F2") + "; input, camera, shooting, death, owner respawn, remote atomic respawn and stale packet rejection passed.\n";
                UnityEngine.Object.Destroy(holder);
                holder = remote = null;
                PhotonNetwork.Destroy(local);
                local = null;
                await Task.Delay(150);
            }
            report += "Frames advanced " + firstFrame + " -> " + Time.frameCount;
            File.WriteAllText("AgentScripts/team-player-validation.txt", report);
            return report;
        }
        finally
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            InputSystem.settings.backgroundBehavior = oldBackground;
            if (holder) UnityEngine.Object.Destroy(holder);
            if (local) PhotonNetwork.Destroy(local);
            UnityEngine.Object.Destroy(plane);
        }
    }
}
