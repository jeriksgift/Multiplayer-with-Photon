using UnityEngine;
using Photon.Pun;

public class WeaponSystem : MonoBehaviourPun
{
    [SerializeField] private float bulletDamage;

    [SerializeField] private Transform muzzlePoint;

    private void Update()
    {
        if (!photonView.IsMine) return;

        if (InputManager.Instance.fire.WasPressedThisFrame()) Fire();
    }

    private void Fire()
    {
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
