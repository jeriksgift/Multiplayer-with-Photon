using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviourPunCallbacks
{
    [Header("Components")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Camera cam;
    [SerializeField] private Transform capsuleRef;
    [SerializeField] private Transform footPoint;
    //private InputManager InputManager.Instance;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed;
    [SerializeField] private float runSpeed;
    private float speed;
    [SerializeField] private float jumpForce;
    [SerializeField] private float camMinAngle;
    [SerializeField] private float camMaxAngle;

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity;

    private Vector2 moveDir;
    private Vector2 lookDir;

    private Vector3 newPos;
    private Vector3 currentRot;
    float pitch;

    private PhotonView photonView;
    private PlayerWithGFXJump gfxJump;

    [Header("Camera Cleanup")]
    [SerializeField] private float cameraSweepInterval = 0.5f;
    private float nextCameraSweep;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        gfxJump = GetComponent<PlayerWithGFXJump>();

        //InputManager.Instance = gameObject.AddComponent<InputManager>();
    }

    private void Start()
    {
        if (!photonView.IsMine) return;

        DestroyOtherCameras();
        InputManager.Instance.LockCursor(true);
    }

    private void DestroyOtherCameras()
    {
        GameObject[] cameras = GameObject.FindGameObjectsWithTag("MainCamera");

        foreach (GameObject cameraObject in cameras)
        {
            PhotonView owner = PhotonView.Get(cameraObject);

            // Scene cameras are not ours to destroy.
            if (owner == null) continue;

            if (owner.IsMine) continue;

            Destroy(cameraObject);
        }
    }

    private void SweepForeignCameras()
    {
        if (Time.unscaledTime < nextCameraSweep) return;
        nextCameraSweep = Time.unscaledTime + cameraSweepInterval;

        if (GameObject.FindGameObjectsWithTag("MainCamera").Length > 1)
        {
            DestroyOtherCameras();
        }
    }

    private void Update()
    {
        if (!photonView.IsMine) return;
        if (InputManager.Instance && InputManager.Instance.IsGameplayBlocked)
        {
            if (gfxJump) gfxJump.SetMoveVelocity(Vector3.zero);
            return;
        }

        MovementAndLook();
        if (gfxJump == null) HandleJump();
        SweepForeignCameras();
    }

    private void MovementAndLook()
    {
        speed = InputManager.Instance.SprintHeld ? runSpeed : walkSpeed;

        moveDir = InputManager.Instance.MoveValue;
        lookDir = InputManager.Instance.LookValue;

        currentRot = rb.rotation.eulerAngles;

        newPos = ((capsuleRef.transform.forward * moveDir.y) + (capsuleRef.transform.right * moveDir.x)) * speed * Time.deltaTime;

        currentRot.y += lookDir.x * mouseSensitivity * Time.deltaTime;

        if (gfxJump != null)
        {
            // GFX characters keep Rigidbody gravity/vertical velocity in FixedUpdate.
            var direction = Vector3.ClampMagnitude(capsuleRef.forward * moveDir.y + capsuleRef.right * moveDir.x, 1f);
            gfxJump.SetMoveVelocity(direction * speed);
            rb.rotation = Quaternion.Euler(currentRot);
        }
        else
        {
            rb.Move(rb.position + newPos, Quaternion.Euler(currentRot));
        }

        //float pitch = cam.transform.localEulerAngles.x;
        pitch -= lookDir.y * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, camMinAngle, camMaxAngle);

        Vector3 angles = cam.transform.localEulerAngles;

        // Convert X to signed angle if needed
        float currentY = angles.y;
        float currentZ = angles.z;

        cam.transform.localRotation = Quaternion.Euler(pitch, currentY, currentZ);
    }

    private void HandleJump()
    {
        if (InputManager.Instance.JumpPressed && IsGrounded())
        {
            rb.AddForce(capsuleRef.transform.up * jumpForce, ForceMode.Impulse);
        }
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(
            footPoint.position,
            -footPoint.up,
            groundCheckDistance,
            groundLayer
        );
    }

    private void DisplayFPS()
    {
        Debug.Log("FPS: " + 1 / Time.deltaTime);
    }
}
