using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public enum WeaponIds
{
    PlaceHolderGun = 0,
}
public class WeaponManager : MonoBehaviour
{
    [SerializeField] private WeaponData[] Weapons;


    
    [SerializeField] private List<WeaponInstanceData> HeldWeapons;


    public Weapon HeldWeapon = null; //Changed this to public so that the HUD can access it for the weapon preview.


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayerSwapsWeapon(0);
    }

    // Update is called once per frame
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
        if (HeldWeapon != null)
        {
            Destroy(HeldWeapon.gameObject);
        }
        Weapon w = Instantiate(Weapons[WeaponIndex].Object,this.gameObject.transform).GetComponent<Weapon>();
        HeldWeapon = w;
        HeldWeapon.SetWeaponManagerReference(this);
    }
    void PlayerTrysToFire()
    {
        HeldWeapon.PlayerTriesToFire();
    }
}
