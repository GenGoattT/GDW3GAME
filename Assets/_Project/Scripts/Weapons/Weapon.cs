using UnityEditor.Animations;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator Animator;
    private WeaponManager WeaponManager;


    //if null, it will use the default value, otherwise it will copy stuff from this. (like current ammo)
    public WeaponInstanceData WeaponInstance;


    [SerializeField] private WeaponIds Id;
    [SerializeField] private string Name;
    [SerializeField] private int CurrentAmmo;
    [SerializeField] private int MaxAmmo;

    private bool AllowPlayerInputs = false;
    private void Awake()
    {
        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (WeaponInstance == null)
        {

        } else
        {

        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void PlayerFires()
    {
        AllowPlayerInputs = false;
        Animator.Play("Fire", 0, 0);
    }
    public void PlayerTriesToFire()
    {
        if (AllowPlayerInputs)
        {
            PlayerFires();
        }

    }
    public void SetWeaponManagerReference(WeaponManager weaponManager)
    {
        WeaponManager = weaponManager;
    }
    public WeaponInstanceData ConvertToWeaponInstanceData()
    {
        WeaponInstanceData w = new WeaponInstanceData(Id,CurrentAmmo,Name);



        return w;
    }
    public bool DoesGunAllowInputs()
    {
        return AllowPlayerInputs;
    }
    public void StartAllowingPlayerInput()
    {
        AllowPlayerInputs = true;
    }
}
