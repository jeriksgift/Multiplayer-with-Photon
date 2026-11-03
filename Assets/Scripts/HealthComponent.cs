using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using UnityEngine;

public class HealthComponent : MonoBehaviour
{
    public event Action<float> HealthChanged;

    private float health;
    [SerializeField] private float maxHealth;
    private bool isDead;
    public bool IsDead => isDead;
    public int LifeRevision { get; private set; }
    private int lastShooterActorNumber = -1;

    private PhotonView photonView;

    private Renderer[] renderers;
    private bool[] rendererStates;
    private Collider[] colliders;
    private bool[] colliderStates;
    private PlayerMovement playerMovement;
    private WeaponSystem weaponSystem;
    private Rigidbody rigidbody;
    private PlayerUI playerUI;

    [SerializeField] private ParticleSystem damageParticleSystem;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererStates = Array.ConvertAll(renderers, r => r.enabled);
        colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = Array.ConvertAll(colliders, c => c.enabled);
        playerMovement = GetComponent<PlayerMovement>();
        weaponSystem = GetComponent<WeaponSystem>();
        rigidbody = GetComponent<Rigidbody>();
        playerUI = GetComponent<PlayerUI>();
        health = maxHealth;
    }

    [PunRPC]
    public void Damage(float damage, Vector3 hitPoint, Vector3 hitNormal, int shooterActorNumber)
    {
        if (!photonView.IsMine || isDead || damage <= 0f) return;
        lastShooterActorNumber = shooterActorNumber;
        health -= damage;

        // Track damage dealt (send to master)
        if (shooterActorNumber != -1 && shooterActorNumber != photonView.Owner.ActorNumber)
        {
            var options = new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.MasterClient };
            Photon.Pun.PhotonNetwork.RaiseEvent(2, new object[] { shooterActorNumber, damage }, options, ExitGames.Client.Photon.SendOptions.SendReliable);
        }
        health = Mathf.Clamp(health, 0, maxHealth);
        HealthChanged?.Invoke(health / maxHealth);
        photonView.RPC(nameof(PlayBloodEffectRpc), RpcTarget.All, hitPoint, hitNormal);
        if (health <= 0)
        {
            Dead();
        }
    }

    [PunRPC]
    private void PlayBloodEffectRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!damageParticleSystem) return;
        damageParticleSystem.transform.position = hitPoint;
        damageParticleSystem.transform.rotation = Quaternion.LookRotation(hitNormal);

        damageParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        damageParticleSystem.Play();
    }

    private void Dead()
    {
        Debug.Log("Dead");
        if (!photonView.IsMine) return;

        // Notify killer for score tracking
        if (lastShooterActorNumber != -1 && lastShooterActorNumber != photonView.Owner.ActorNumber)
        {
            global::GameManager.AddKill(lastShooterActorNumber);
        }

        photonView.RPC(nameof(SetLifeStateRpc), RpcTarget.All, LifeRevision + 1, true, transform.position, transform.rotation);
        StartCoroutine(RespawnPlayer());
    }

    private IEnumerator RespawnPlayer()
    {
        yield return new WaitForSeconds(2f);
        if (!PhotonNetwork.InRoom || (GameManager.Instance && GameManager.Instance.matchEnded)) yield break;
        Transform spawnLoc = GameManager.Instance ? GameManager.Instance.GetSpawnLoc() : null;
        Vector3 position = spawnLoc ? spawnLoc.position : new Vector3(0f, 2f, 0f);
        Quaternion rotation = spawnLoc ? spawnLoc.rotation : Quaternion.identity;
        photonView.RPC(nameof(SetLifeStateRpc), RpcTarget.All, LifeRevision + 1, false, position, rotation);
    }

    [PunRPC]
    private void SetLifeStateRpc(int revision, bool dead, Vector3 position, Quaternion rotation, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner) return;
        ApplyLifeState(revision, dead, position, rotation);
    }

    // Also carried in motion snapshots so late joiners receive the current life state.
    public void ApplyLifeState(int revision, bool dead, Vector3 position, Quaternion rotation)
    {
        if (revision <= LifeRevision) return;
        LifeRevision = revision;
        isDead = dead;
        // Teleport and reset interpolation BEFORE restoring any renderer or collider.
        var sync = GetComponent<PlayerNetworkSync>();
        if (sync) sync.Teleport(position, rotation);
        else
        {
            rigidbody.position = position;
            rigidbody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
        if (!rigidbody.isKinematic)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }
        GetComponent<PlayerWithGFXJump>()?.ResetMotion();
        GetComponent<PlayerWithGFXPresentation>()?.ResetMotion();
        if (!dead)
        {
            health = maxHealth;
            lastShooterActorNumber = -1;
            HealthChanged?.Invoke(1f);
        }
        EnableOrDisableGraphics(!dead);
    }

    private void EnableOrDisableGraphics(bool enable)
    {
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = enable && rendererStates[i];
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = enable && colliderStates[i];
        playerMovement.enabled = enable && photonView.IsMine;
        weaponSystem.enabled = enable && photonView.IsMine;
        bool local = photonView.IsMine || (!PhotonNetwork.InRoom && photonView.ViewID == 0);
        rigidbody.isKinematic = !local;
        rigidbody.useGravity = enable && local;
        if (playerUI && playerUI.worldNameText)
            playerUI.worldNameText.gameObject.SetActive(enable && !local);
    }
}
