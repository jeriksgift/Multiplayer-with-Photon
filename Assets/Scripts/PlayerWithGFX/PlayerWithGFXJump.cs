using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Owner-controlled jump physics shared by the blue and red GFX variants.</summary>
[DefaultExecutionOrder(40)]
public sealed class PlayerWithGFXJump : MonoBehaviourPun
{
    [Min(.1f)] public float jumpHeight = 1f;
    [Min(1f)] public float risingGravity = 2f;
    [Min(1f)] public float fallingGravity = 3f;
    public LayerMask groundMask = Physics.DefaultRaycastLayers;
    public bool Grounded { get; private set; }

    Rigidbody body;
    CapsuleCollider capsule;
    InputAction jump;
    Vector3 desiredMovement;
    bool movementSupplied;
    readonly RaycastHit[] hits = new RaycastHit[12];
    float lastGrounded = float.NegativeInfinity;
    float requestedAt = float.NegativeInfinity;
    float lastJump = float.NegativeInfinity;
    bool Local => (!PhotonNetwork.InRoom && photonView.ViewID == 0) || photonView.IsMine;
    public void SetMoveVelocity(Vector3 velocity) { desiredMovement = velocity; movementSupplied = true; }

    public void ResetMotion()
    {
        desiredMovement = Vector3.zero;
        movementSupplied = false;
        Grounded = false;
        lastGrounded = requestedAt = lastJump = float.NegativeInfinity;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        jump = GetComponent<PlayerInput>().actions.FindAction("Jump", false);
    }

    void Update()
    {
        if (Local && capsule.enabled && Cursor.lockState == CursorLockMode.Locked && jump != null && jump.WasPressedThisFrame())
            requestedAt = Time.time;
    }

    void FixedUpdate()
    {
        if (!Local || body.isKinematic || !capsule.enabled || !body.useGravity) return;
        if (movementSupplied)
        {
            var v = body.linearVelocity;
            body.linearVelocity = new Vector3(desiredMovement.x, v.y, desiredMovement.z);
        }
        Grounded = ProbeGround() && body.linearVelocity.y <= .1f && Time.time - lastJump > .12f;
        if (Grounded) lastGrounded = Time.time;
        // A brief input buffer and edge grace keep takeoff responsive without air jumps.
        if (Time.time - requestedAt <= .1f && Time.time - lastGrounded <= .08f && Time.time - lastJump > .15f)
        {
            var v = body.linearVelocity;
            v.y = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * risingGravity * jumpHeight);
            body.linearVelocity = v;
            requestedAt = float.NegativeInfinity;
            lastGrounded = float.NegativeInfinity;
            lastJump = Time.time;
            Grounded = false;
        }
        if (!Grounded)
        {
            float multiplier = body.linearVelocity.y > 0 ? risingGravity : fallingGravity;
            body.AddForce(Physics.gravity * (multiplier - 1), ForceMode.Acceleration);
        }
    }

    bool ProbeGround()
    {
        var bounds = capsule.bounds;
        float radius = Mathf.Min(.22f, capsule.radius * .75f);
        var origin = new Vector3(bounds.center.x, bounds.min.y + radius + .1f, bounds.center.z);
        int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, hits, .18f, groundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(transform) && Vector3.Dot(hits[i].normal, Vector3.up) > .6f) return true;
        return false;
    }
}
