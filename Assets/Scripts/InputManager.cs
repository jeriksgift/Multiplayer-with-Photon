using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    private PlayerInput playerInput;

    [HideInInspector] public InputAction move;
    [HideInInspector] public InputAction look;
    [HideInInspector] public InputAction sprint;
    [HideInInspector] public InputAction jump;
    [HideInInspector] public InputAction fire;
    [HideInInspector] public InputAction escape;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }

        playerInput = GetComponent<PlayerInput>();
        move = playerInput.actions.FindAction("Move");
        look = playerInput.actions.FindAction("Look");
        jump = playerInput.actions.FindAction("Jump");
        sprint = playerInput.actions.FindAction("Sprint");
        fire = playerInput.actions.FindAction("Fire");
        escape = playerInput.actions.FindAction("Escape");
    }

    public void LockCursor(bool locked)
    {
        Cursor.lockState = (locked) ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void Update()
    {
        bool escapePressed = false;
        if (escape != null && escape.WasPressedThisFrame())
            escapePressed = true;
        else if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            escapePressed = true;

        if (escapePressed)
        {
            LockCursor(!Cursor.visible);
        }
    }
}
