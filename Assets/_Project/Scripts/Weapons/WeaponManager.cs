using System.Collections.Generic;
using UnityEngine;
using Project.Player;
using Project.Tutorial;

public enum WeaponIds
{
    PlaceHolderGun = 0,
    GleipnirShotgun = 1,
}
public class WeaponManager : MonoBehaviour
{
    [SerializeField] private WeaponData[] Weapons;

    [SerializeField] private List<WeaponInstanceData> HeldWeapons;

    public Weapon HeldWeapon = null;
    private int heldIndex = -1;

    public Camera AimCamera { get; private set; }
    public Transform PlayerRoot { get; private set; }

    private void Awake()
    {
        PlayerCamera playerCam = GetComponentInParent<PlayerCamera>();
        if (playerCam == null) playerCam = FindFirstObjectByType<PlayerCamera>();
        if (playerCam != null) AimCamera = playerCam.GetComponent<Camera>();

        if (AimCamera == null) AimCamera = GetComponentInParent<Camera>();
        if (AimCamera == null) AimCamera = Camera.main;

        PlayerMovement player = GetComponentInParent<PlayerMovement>();
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) PlayerRoot = player.transform;
    }

    void Start()
    {
        PlayerSwapsWeapon(0);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlayerTrysToFire();
        }
        if (Input.GetMouseButtonDown(1) && HeldWeapon != null)
        {
            HeldWeapon.PlayerTriesToAltFire();
            ActionFeed.Raise(PlayerAction.AltFire, HeldWeapon);
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SwapFromInput(0);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SwapFromInput(1);
        }
    }

    void SwapFromInput(int weaponIndex)
    {
        int before = heldIndex;
        PlayerSwapsWeapon(weaponIndex);
        if (heldIndex != before)
        {
            ActionFeed.Raise(PlayerAction.SwapWeapon, HeldWeapon);
        }
    }

    void PlayerSwapsWeapon(int WeaponIndex)
    {
        if (Weapons == null || WeaponIndex < 0 || WeaponIndex >= Weapons.Length || Weapons[WeaponIndex].Object == null)
        {
            return;
        }

        if (HeldWeapon != null)
        {
            Destroy(HeldWeapon.gameObject);
        }
        Weapon w = Instantiate(Weapons[WeaponIndex].Object, this.gameObject.transform).GetComponent<Weapon>();
        HeldWeapon = w;
        heldIndex = WeaponIndex;
        HeldWeapon.SetWeaponManagerReference(this);
    }
    void PlayerTrysToFire()
    {
        if (HeldWeapon != null)
        {
            HeldWeapon.PlayerTriesToFire();
            ActionFeed.Raise(PlayerAction.Fire, HeldWeapon);
        }
    }
}
