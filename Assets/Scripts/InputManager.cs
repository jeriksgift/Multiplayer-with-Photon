using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    private PlayerInput playerInput;
    private ToysGameUI gameUI;
    public GameHudScreen Hud { get; set; }
    public Vector2 TouchMove { get; set; }
    public Vector2 TouchLook { get; set; }
    public bool TouchSprint { get; set; }
    public bool TouchJumpRequested { get; set; }
    public bool TouchFireRequested { get; set; }
    public Vector2 MoveValue => Application.isMobilePlatform ? TouchMove : move.ReadValue<Vector2>() + TouchMove;
    public Vector2 LookValue => Application.isMobilePlatform ? TouchLook : look.ReadValue<Vector2>() + TouchLook;
    public bool SprintHeld => TouchSprint || sprint.IsPressed();
    public bool JumpPressed { get { bool touch = TouchJumpRequested; TouchJumpRequested = false; return touch || jump.WasPressedThisFrame(); } }
    public bool FirePressed { get { bool touch = TouchFireRequested; TouchFireRequested = false; return touch || (!Application.isMobilePlatform && fire.WasPressedThisFrame()); } }
    public bool IsGameplayBlocked => (gameUI && gameUI.IsExitOpen) || (Hud && Hud.IsPanelOpen) ||
        (GameManager.Instance && GameManager.Instance.matchEnded);

    [HideInInspector] public InputAction move;
    [HideInInspector] public InputAction look;
    [HideInInspector] public InputAction sprint;
    [HideInInspector] public InputAction jump;
    [HideInInspector] public InputAction fire;
    [HideInInspector] public InputAction escape;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        gameUI = GetComponent<ToysGameUI>();
        var view = GetComponent<PhotonView>();
        bool local = view == null || view.IsMine || (!PhotonNetwork.InRoom && view.ViewID == 0);
        if (!local)
        {
            // Remote avatars must never pair with this client's keyboard or mouse.
            playerInput.enabled = false;
            enabled = false;
            return;
        }
        Instance = this;
        // Prefabs start with PlayerInput disabled so a remote instance cannot pair
        // devices during OnEnable before ownership has been checked here.
        playerInput.enabled = true;
        move = playerInput.actions.FindAction("Move");
        look = playerInput.actions.FindAction("Look");
        jump = playerInput.actions.FindAction("Jump");
        sprint = playerInput.actions.FindAction("Sprint");
        fire = playerInput.actions.FindAction("Fire");
        escape = playerInput.actions.FindAction("Escape");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void LockCursor(bool locked)
    {
        if (Application.isMobilePlatform) locked = false;
        Cursor.lockState = (locked) ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void Update()
    {
        if (GameManager.Instance && GameManager.Instance.matchEnded) return;
        bool escapePressed = false;
        if (escape != null && escape.WasPressedThisFrame())
            escapePressed = true;
        else if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            escapePressed = true;

        if (escapePressed)
        {
            if (Hud && Hud.ClosePanel()) return;
            if (gameUI) gameUI.ToggleExit();
            else LockCursor(!Cursor.visible);
        }
    }
}
