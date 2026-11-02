using System.Collections.Generic;
using UnityEngine;
using Project.Player;

public enum WeaponIds
{
    PlaceHolderGun = 0,
}
public class WeaponManager : MonoBehaviour
{
    [SerializeField] private WeaponData[] Weapons;

    [SerializeField] private List<WeaponInstanceData> HeldWeapons;

    private Weapon HeldWeapon = null;

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
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            PlayerSwapsWeapon(0);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            PlayerSwapsWeapon(1);
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
        HeldWeapon.SetWeaponManagerReference(this);
    }
    void PlayerTrysToFire()
    {
        if (HeldWeapon != null)
        {
            HeldWeapon.PlayerTriesToFire();
        }
    }
}
