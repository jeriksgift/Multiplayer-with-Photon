using Photon.Pun;
using UnityEngine;

/// <summary>Replicates motion and life state together, rejecting snapshots from earlier lives.</summary>
[RequireComponent(typeof(PhotonView), typeof(Rigidbody), typeof(HealthComponent))]
public sealed class PlayerNetworkSync : MonoBehaviourPun, IPunObservable
{
    Rigidbody body;
    HealthComponent health;
    Vector3 targetPosition;
    Quaternion targetRotation;
    double lastSnapshotTime = double.NegativeInfinity;
    bool receivedSnapshot;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        health = GetComponent<HealthComponent>();
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        if (photonView.IsMine || !PhotonNetwork.InRoom || !receivedSnapshot || health.IsDead) return;
        float blend = 1f - Mathf.Exp(-15f * Time.fixedDeltaTime);
        body.MovePosition(Vector3.Lerp(body.position, targetPosition, blend));
        body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, blend));
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        targetPosition = position;
        targetRotation = rotation;
        body.position = position;
        body.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(health.LifeRevision);
            stream.SendNext(health.IsDead);
            stream.SendNext(body.position);
            stream.SendNext(body.rotation);
            stream.SendNext(body.linearVelocity);
        }
        else
        {
            int revision = (int)stream.ReceiveNext();
            bool dead = (bool)stream.ReceiveNext();
            Vector3 position = (Vector3)stream.ReceiveNext();
            Quaternion rotation = (Quaternion)stream.ReceiveNext();
            Vector3 velocity = (Vector3)stream.ReceiveNext();
            ReceiveSnapshot(revision, dead, position, rotation, velocity, info.SentServerTime);
        }
    }

    public void ReceiveSnapshot(int revision, bool dead, Vector3 position, Quaternion rotation, Vector3 velocity, double sentTime)
    {
        if (revision < health.LifeRevision || sentTime < lastSnapshotTime) return;
        bool teleport = !receivedSnapshot || revision > health.LifeRevision;
        lastSnapshotTime = sentTime;
        health.ApplyLifeState(revision, dead, position, rotation);
        receivedSnapshot = true;
        // Life transitions always use the exact spawn pose, without extrapolation.
        if (teleport) Teleport(position, rotation);
        else
        {
            float lag = Mathf.Clamp((float)(PhotonNetwork.Time - sentTime), 0f, .2f);
            targetPosition = dead ? position : position + velocity * lag;
            targetRotation = rotation;
        }
    }
}
