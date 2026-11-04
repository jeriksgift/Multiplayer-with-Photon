using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;
public static class VerifyAndroidGameplay
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Click(Button button) { button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt); }
    static void Press(VisualElement element, Vector2 point)
    {
        using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = point })) element.SendEvent(evt);
    }
    static void Release(VisualElement element, Vector2 point)
    {
        using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = point })) element.SendEvent(evt);
    }
    public static async Task<string> Preview()
    {
        foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>()) doc.gameObject.SetActive(false);
        var player = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("PlayerWithGFX"), new Vector3(0, 10, 0), Quaternion.identity);
        player.GetComponent<Rigidbody>().useGravity = false;
        await Task.Delay(400);
        var root = player.transform.Find("GameHudDocument").GetComponent<UIDocument>().rootVisualElement;
        root.Q("MobileControls").style.display = DisplayStyle.Flex;
        return "Preview ready";
    }
    public static async Task<string> Run()
    {
        Check(Application.isPlaying, "Play mode required");
        var player = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("PlayerWithGFX"), new Vector3(0, 10, 0), Quaternion.identity);
        try
        {
            await Task.Delay(500);
            int frame = Time.frameCount; await Task.Delay(100); Check(Time.frameCount > frame, "Frames frozen");
            var hud = player.GetComponent<GameHudScreen>();
            var input = player.GetComponent<InputManager>();
            var doc = player.transform.Find("GameHudDocument").GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            root.Q("MobileControls").style.display = DisplayStyle.Flex;
            await Task.Delay(100);
            var weapon = player.GetComponent<WeaponSystem>();
            int ammo = weapon.AmmoInMagazine;
            Press(root.Q("Fire"), root.Q("Fire").worldBound.center);
            await Task.Delay(120);
            Check(weapon.AmmoInMagazine == ammo - 1, "Fire did not produce exactly one shot");
            await Task.Delay(120);
            Check(weapon.AmmoInMagazine == ammo - 1, "Fire repeated without another press");
            Press(root.Q("AmmoArea"), root.Q("AmmoArea").worldBound.center);
            Check(weapon.IsReloading, "Ammo touch did not reload");
            await Task.Delay(1700);
            Check(weapon.AmmoInMagazine == WeaponSystem.MagazineSize, "Reload did not complete");
            Press(root.Q("ScoreArea"), root.Q("ScoreArea").worldBound.center);
            Check(hud.IsPanelOpen && input.IsGameplayBlocked, "Scoreboard did not block gameplay");
            Click(root.Q<Button>("CloseScoreboard")); await Task.Delay(100); Check(!hud.IsPanelOpen, "Scoreboard did not close");
            Click(root.Q<Button>("MenuButton"));
            await Task.Delay(100);
            var settings = doc.GetComponent<LobbySettingsScreen>();
            Check(settings.IsOpen && input.IsGameplayBlocked, "Settings did not open or block input");
            Check(root.Q("AccountsTab").ClassListContains("toys-hidden") && root.Q("FriendsTab").ClassListContains("toys-hidden"), "Lobby-only tabs visible");
            Check(!root.Q("ControlsPage").ClassListContains("toys-hidden"), "Controls is not default tab");
            Click(root.Q<Button>("SoundTab")); await Task.Delay(100); Check(!root.Q("SoundPage").ClassListContains("toys-hidden"), "Sound tab failed");
            Click(root.Q<Button>("GraphicsTab")); await Task.Delay(100); Check(!root.Q("GraphicsPage").ClassListContains("toys-hidden"), "Graphics tab failed");
            Click(root.Q<Button>("CloseSettingsButton")); await Task.Delay(100); Check(!settings.IsOpen && !hud.IsPanelOpen, "Settings failed to close");
            var start = player.transform.position;
            input.TouchMove = Vector2.up; await Task.Delay(200);
            Check(input.TouchSprint, "Full-forward joystick did not sprint");
            Check(Vector3.Distance(start, player.transform.position) > .2f, "Touch locomotion failed");
            input.TouchMove = Vector2.zero;
            var sprint = root.Q("Sprint"); Press(sprint, sprint.worldBound.center); await Task.Delay(80);
            Check(input.TouchSprint, "Separate sprint button failed"); Release(sprint, sprint.worldBound.center); await Task.Delay(80);
            Check(!input.TouchSprint, "Sprint stuck after release");
            var joystick = root.Q("Joystick");
            Press(joystick, joystick.worldBound.center + Vector2.down * joystick.layout.width * .32f); await Task.Delay(100);
            Check(input.TouchMove.y > .9f && input.TouchSprint, "Joystick event did not move/sprint");
            Release(joystick, joystick.worldBound.center); await Task.Delay(80); Check(input.TouchMove == Vector2.zero, "Joystick stuck after release");
            var look = root.Q("LookArea"); Vector2 point = look.worldBound.center;
            Quaternion rotation = player.transform.rotation;
            Press(look, point);
            using (var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = point + new Vector2(40, 0) })) look.SendEvent(evt);
            await Task.Delay(100); Release(look, point);
            Check(Quaternion.Angle(rotation, player.transform.rotation) > .1f, "Camera drag failed");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var jump = player.GetComponent<PlayerWithGFXJump>();
                for (int layer = 0; layer < 32; layer++) if ((jump.groundMask.value & (1 << layer)) != 0) { floor.layer = layer; break; }
                floor.transform.position = new Vector3(player.transform.position.x, player.GetComponent<CapsuleCollider>().bounds.min.y - .5f, player.transform.position.z);
                floor.transform.localScale = new Vector3(20, 1, 20);
                await Task.Delay(350);
                Check(jump.Grounded, "Test player did not settle on floor");
                Press(root.Q("Jump"), root.Q("Jump").worldBound.center); await Task.Delay(80);
                Check(player.GetComponent<Rigidbody>().linearVelocity.y > 0, "Jump button failed");
            }
            finally { UnityEngine.Object.Destroy(floor); }
            Click(root.Q<Button>("LeaveMatchButton")); await Task.Delay(100); Check(player.GetComponent<ToysGameUI>().IsExitOpen, "Leave match did not open confirmation");
            player.GetComponent<ToysGameUI>().CancelExit();
            return "Advancing frames; single-shot fire; reload; scoreboard; settings tabs; input blocking; joystick movement/forward sprint/release; separate sprint/release; camera drag; jump; leave confirmation passed.";
        }
        finally { UnityEngine.Object.Destroy(player); }
    }
}
