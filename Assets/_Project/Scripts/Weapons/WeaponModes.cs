public interface IWeaponPrimaryFire
{
    float PrimaryCooldown { get; }
    void FirePrimary();
}

public interface IWeaponAltFire
{
    void TryAltFire();
}
