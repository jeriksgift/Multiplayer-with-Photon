using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Animation and rifle presentation, used only by PlayerWithGFX.</summary>
[DefaultExecutionOrder(50)]
public sealed class PlayerWithGFXPresentation : MonoBehaviourPun, IPunObservable
{
    public Animator animator;
    public Transform rifleAim;
    public Transform rightGrip;
    public Transform leftGrip;
    public Transform muzzle;
    public GameObject muzzleFlash;
    public Transform firstPersonRifle;
    public GameObject firstPersonFlash;
    public Camera playerCamera;
    public Rigidbody body;
    public Transform footPoint;
    public LayerMask groundMask;
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    [HideInInspector] public float jumpImpulse = 5f; // Legacy builder value; PlayerWithGFXJump controls the jump.
    public float mouseSensitivity = 18f;

    PlayerInput input;
    PlayerMovement movement;
    WeaponSystem weapon;
    Vector3 previousPosition;
    Vector3 sampledVelocity;
    float sampleTime;
    float speed, pitch, remotePitch, recoil, flashUntil;
    int shots, receivedShots;
    bool grounded;
    Quaternion aimRest;
    CapsuleCollider capsule;
    SkinnedMeshRenderer[] skins;
    Vector3 viewRest;
    PlayerWithGFXJump jumpController;
    HealthComponent health;
    bool Standalone => !PhotonNetwork.InRoom && photonView.ViewID == 0;
    bool Local => Standalone || photonView.IsMine;
    public float AnimationSpeed => speed;
    public bool Grounded => grounded;
    public int Shots => shots;

    void Awake()
    {
        input = GetComponent<PlayerInput>();
        movement = GetComponent<PlayerMovement>();
        weapon = GetComponent<WeaponSystem>();
        jumpController = GetComponent<PlayerWithGFXJump>();
        health = GetComponent<HealthComponent>();
        previousPosition = transform.position;
        capsule = GetComponent<CapsuleCollider>();
        skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        aimRest = rifleAim.localRotation;
        if (firstPersonRifle) viewRest = firstPersonRifle.localPosition;
        if (Local) InputManager.Instance = GetComponent<InputManager>();
        if (Standalone)
        {
            // The shared controllers require a Photon owner. This prefab also supports
            // playing its placed scene instance directly, without joining a room.
            movement.enabled = false;
            weapon.enabled = false;
        }
        if (!Local)
        {
            input.enabled = false;
            if (playerCamera) playerCamera.gameObject.SetActive(false);
            if (firstPersonRifle) firstPersonRifle.gameObject.SetActive(false);
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) canvas.gameObject.SetActive(false);
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    void Start()
    {
        if (!Local) return;
        if (Standalone)
            foreach (var other in FindObjectsByType<Camera>())
                if (other != playerCamera && other.GetComponentInParent<PhotonView>()) other.enabled = false;
        // Hide only this owner's world model. Other players stay visible to this camera.
        foreach (var skin in skins) skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        foreach (var renderer in rifleAim.GetComponentsInChildren<Renderer>(true))
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        if (firstPersonRifle) firstPersonRifle.gameObject.SetActive(true);
        if (playerCamera) { playerCamera.gameObject.SetActive(true); playerCamera.enabled = true; }
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) canvas.gameObject.SetActive(true);
        if (Standalone) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    void Update()
    {
        if (health && health.IsDead)
        {
            if (muzzleFlash) muzzleFlash.SetActive(false);
            if (firstPersonRifle) firstPersonRifle.gameObject.SetActive(false);
            if (firstPersonFlash) firstPersonFlash.SetActive(false);
            return;
        }
        if (Local)
        {
            if (Standalone) UpdateStandalone();
            else if (playerCamera) pitch = Mathf.DeltaAngle(0, playerCamera.transform.localEulerAngles.x);
            if (Pressed("Fire") && (!InputManager.Instance || !InputManager.Instance.IsGameplayBlocked) && (Standalone || (weapon && weapon.enabled)))
            {
                shots++;
                PlayShot();
                if (Standalone) FireStandalone();
            }
        }
        else pitch = Mathf.LerpAngle(pitch, remotePitch, 12 * Time.deltaTime);
        foreach (var skin in skins) skin.enabled = capsule.enabled;

        sampleTime += Time.deltaTime;
        if (sampleTime >= .08f)
        {
            sampledVelocity = (transform.position - previousPosition) / sampleTime;
            previousPosition = transform.position;
            sampleTime = 0;
        }
        Vector3 velocity = Standalone ? body.linearVelocity : sampledVelocity;
        // Ignore spawn/respawn teleports when deriving remote locomotion.
        float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
        speed = Mathf.Lerp(speed, horizontal > runSpeed * 3 ? 0 : horizontal, 1 - Mathf.Exp(-12 * Time.deltaTime));
        grounded = Local && jumpController ? jumpController.Grounded : Physics.Raycast(footPoint.position + Vector3.up * .12f, Vector3.down, .3f, groundMask, QueryTriggerInteraction.Ignore);
        animator.SetFloat("Speed", speed);
        animator.SetBool("Grounded", grounded);
        if (jumpController) animator.SetFloat("VerticalSpeed", Local ? body.linearVelocity.y : sampledVelocity.y);
        var localVelocity = transform.InverseTransformDirection(velocity);
        animator.SetFloat("Playback", localVelocity.z < -.1f ? -1f : 1f);
        recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 8);
        rifleAim.localRotation = aimRest * Quaternion.Euler(Mathf.Clamp(pitch, -55, 55) - recoil * 4, 0, 0);
        if (muzzleFlash) muzzleFlash.SetActive(Time.time < flashUntil);
        if (firstPersonRifle && Local)
        {
            firstPersonRifle.gameObject.SetActive(capsule.enabled);
            float bob = Mathf.Clamp01(speed / runSpeed);
            firstPersonRifle.localPosition = viewRest + new Vector3(Mathf.Sin(Time.time * 8) * .003f * bob, Mathf.Cos(Time.time * 16) * .003f * bob, -recoil * .035f);
            firstPersonRifle.localRotation = Quaternion.Euler(-recoil * 4, 0, 0);
        }
        if (firstPersonFlash) firstPersonFlash.SetActive(Local && Time.time < flashUntil);
    }

    void UpdateStandalone()
    {
        if (Cursor.lockState != CursorLockMode.Locked || !capsule.enabled)
        {
            jumpController.SetMoveVelocity(Vector3.zero);
            return;
        }
        var move = Read("Move");
        var look = Read("Look");
        transform.Rotate(0, look.x * mouseSensitivity * Time.deltaTime, 0);
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity * Time.deltaTime, -70, 70);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        float rate = Held("Sprint") ? runSpeed : walkSpeed;
        var direction = Vector3.ClampMagnitude(transform.forward * move.y + transform.right * move.x, 1);
        jumpController.SetMoveVelocity(direction * rate);
        // PlayerWithGFXJump owns takeoff and gravity in FixedUpdate.
    }

    InputAction Action(string name) => input && input.actions != null ? input.actions.FindAction(name, false) : null;
    Vector2 Read(string name) => Action(name)?.ReadValue<Vector2>() ?? Vector2.zero;
    bool Pressed(string name) => Action(name)?.WasPressedThisFrame() ?? false;
    bool Held(string name) => Action(name)?.IsPressed() ?? false;

    void FireStandalone()
    {
        // Online hit detection and damage stay in the existing WeaponSystem.
        var ray = playerCamera.ViewportPointToRay(new Vector3(.5f, .5f));
        if (Physics.Raycast(ray, out var hit, 100, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = hit.rigidbody;
            if (target && !target.isKinematic && target != body) target.AddForceAtPosition(ray.direction * 3, hit.point, ForceMode.Impulse);
        }
    }

    public void PlayShot()
    {
        recoil = 1;
        flashUntil = Time.time + .065f;
    }

    public void ResetMotion()
    {
        previousPosition = transform.position;
        sampledVelocity = Vector3.zero;
        sampleTime = speed = recoil = flashUntil = 0;
        animator.SetFloat("Speed", 0);
        if (muzzleFlash) muzzleFlash.SetActive(false);
        if (firstPersonFlash) firstPersonFlash.SetActive(false);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) { stream.SendNext(pitch); stream.SendNext(shots); }
        else
        {
            remotePitch = (float)stream.ReceiveNext();
            int sequence = (int)stream.ReceiveNext();
            if (sequence != receivedShots) { if (receivedShots != 0 || sequence > 0) PlayShot(); receivedShots = sequence; }
        }
    }

    public void ApplyHandIK(Animator target)
    {
        if (!rightGrip || !leftGrip) return;
        target.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
        target.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
        target.SetIKPosition(AvatarIKGoal.RightHand, rightGrip.position);
        target.SetIKRotation(AvatarIKGoal.RightHand, rightGrip.rotation);
        target.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
        target.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
        target.SetIKPosition(AvatarIKGoal.LeftHand, leftGrip.position);
        target.SetIKRotation(AvatarIKGoal.LeftHand, leftGrip.rotation);
        target.SetIKHintPositionWeight(AvatarIKHint.RightElbow, .7f);
        target.SetIKHintPosition(AvatarIKHint.RightElbow, transform.TransformPoint(.36f, 1.05f, .02f));
        target.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, .7f);
        target.SetIKHintPosition(AvatarIKHint.LeftElbow, transform.TransformPoint(-.38f, 1.12f, .22f));
        target.SetLookAtWeight(.35f, .1f, .7f, .2f, .5f);
        target.SetLookAtPosition(rifleAim.position + rifleAim.forward * 15);
    }
}
