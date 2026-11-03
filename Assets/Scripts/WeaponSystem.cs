using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;

public class WeaponSystem : MonoBehaviourPun
{
    [SerializeField] private float bulletDamage;

    [SerializeField] private Transform muzzlePoint;
    [SerializeField, Min(0.1f)] private float reloadDuration = 1.5f;
    public const int MagazineSize = 7;
    public int AmmoInMagazine { get; private set; } = MagazineSize;
    public bool IsReloading { get; private set; }
    public int LastShotFrame { get; private set; } = -1;
    public event System.Action AmmoChanged;
    private float reloadRemaining;
    private HealthComponent health;
    private PlayerInput playerInput;
    private int lifeRevision;

    private bool Local => photonView.IsMine || (!PhotonNetwork.InRoom && photonView.ViewID == 0);

    private void Awake()
    {
        health = GetComponent<HealthComponent>();
        playerInput = GetComponent<PlayerInput>();
        if (health) health.HealthChanged += OnHealthChanged;
    }

    private void OnHealthChanged(float value)
    {
        if (!health.IsDead && health.LifeRevision > lifeRevision)
        {
            lifeRevision = health.LifeRevision;
            AmmoInMagazine = MagazineSize;
            IsReloading = false;
            LastShotFrame = -1;
            AmmoChanged?.Invoke();
        }
    }

    private void OnDisable()
    {
        IsReloading = false;
        AmmoChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (health) health.HealthChanged -= OnHealthChanged;
    }

    private void Update()
    {
        if (!Local || (health && health.IsDead)) return;
        if (IsReloading)
        {
            reloadRemaining -= Time.deltaTime;
            if (reloadRemaining <= 0f)
            {
                IsReloading = false;
                AmmoInMagazine = MagazineSize;
                AmmoChanged?.Invoke();
            }
        }
        if (InputManager.Instance && InputManager.Instance.IsGameplayBlocked) return;
        if (playerInput && playerInput.actions.FindAction("Reload", false)?.WasPressedThisFrame() == true) Reload();
        if (InputManager.Instance && InputManager.Instance.fire != null && InputManager.Instance.fire.WasPressedThisFrame()) Fire();
    }

    public void Reload()
    {
        if (!Local || !enabled || IsReloading || AmmoInMagazine == MagazineSize ||
            (health && health.IsDead) || (InputManager.Instance && InputManager.Instance.IsGameplayBlocked)) return;
        IsReloading = true;
        reloadRemaining = reloadDuration;
        AmmoChanged?.Invoke();
    }

    private void Fire()
    {
        if (IsReloading) return;
        if (AmmoInMagazine == 0) { Reload(); return; }
        AmmoInMagazine--;
        LastShotFrame = Time.frameCount;
        AmmoChanged?.Invoke();
        if (AmmoInMagazine == 0) Reload();
        if (!PhotonNetwork.InRoom) return;
        RaycastHit? hit = FireHit();

        if (hit.HasValue)
        {
            if (hit.Value.collider.TryGetComponent<HealthComponent>(out HealthComponent healthComponent))
            {
                var targetView = healthComponent.GetComponent<PhotonView>();
                if (targetView == null || targetView.Owner == null) return;

                // Friendly fire check: only damage enemies
                var localTeam = RoomManager.GetLocalTeam();
                var targetTeam = RoomManager.GetTeam(targetView.Owner);
                if (localTeam == targetTeam) return;

                targetView.RPC(nameof(HealthComponent.Damage), targetView.Owner, bulletDamage, hit.Value.point, hit.Value.normal, PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }
    }

    private RaycastHit? FireHit()
    {
        if (!Camera.main) return null;
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

        Ray ray = Camera.main.ScreenPointToRay(screenCenter);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            return hit;
        }
        else
        {
            return null;
        }
    }
}
