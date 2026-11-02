using UnityEngine;
using UnityEngine.UI;
[System.Serializable]
class WeaponData
{
    public string Name;
    public GameObject Object;
    //public Sprite HudPreview;
}



public class WeaponInstanceData
{
    public WeaponIds WeaponId;
    public int Ammo;
    public string Name;
    //public Sprite HudPreview;

    public WeaponInstanceData(WeaponIds WeaponId, int Ammo, string Name/*, Sprite HudPreview*/)
    {
        this.WeaponId = WeaponId;
        this.Ammo = Ammo;
        this.Name = Name;
        //this.HudPreview = HudPreview;
    }
}