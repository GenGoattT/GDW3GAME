using UnityEngine;
[System.Serializable]
class WeaponData
{
    public string Name;
    public GameObject Object;
    
}



public class WeaponInstanceData
{
    public WeaponIds WeaponId;
    public int Ammo;
    public string Name;

    public WeaponInstanceData(WeaponIds WeaponId, int Ammo, string Name)
    {
        this.WeaponId = WeaponId;
        this.Ammo = Ammo;
        this.Name = Name;
    }
}