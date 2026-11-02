using Project.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class HudController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] UnityEngine.UI.Slider healthBar;
    [SerializeField] GameObject healthText;
    [SerializeField] UnityEngine.UI.Image healthFill;
    [SerializeField] Color HealthColor;
    [SerializeField] Color FlashColor;
    private float healthFlashTime = 0.1f;
    private float healthFlashTimer = 0f;

    [Header("Dash")]
    [SerializeField] GameObject dashText;
    [SerializeField] UnityEngine.UI.Slider dashCooldown;
    [SerializeField] UnityEngine.UI.Image dashFill; 
    [SerializeField] Color DashColor;
    [SerializeField] Color RechargeColor;
    private float rechargeFlashTime = 0.1f;
    private float rechargeFlashTimer = 0f;
    private float lastDashCharges = 0f;

    [Header("Weapons")]
    [SerializeField] GameObject ammoText;
    [SerializeField] UnityEngine.UI.Image currentWeaponImage;
    [SerializeField] UnityEngine.UI.Image secondWeaponImage;
    [SerializeField] UnityEngine.UI.Image thirdWeaponImage;
    

    [Header("References")]
    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] WeaponManager weaponManager;
    //Don't have any other resources in the game that need tracking right now.
    //[SerializeField] PlayerStatus playerStatus;

    [Header("Temporary fields")]
    [Tooltip("This is a temporary field for testing purposes. It will be removed when the player status is implemented.")]
    [SerializeField] float maxHealth = 100;
    [SerializeField] float playerHealth = 70;
    public float dashCooldownPercent; //Using this to view the dash cooldown as a percent

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Health
        healthBar.value = playerHealth / maxHealth;
        healthText.GetComponent<TextMeshProUGUI>().text = $"{playerHealth/maxHealth*100}";
        if (healthFlashTimer > 0)
        {
            healthFlashTimer -= Time.deltaTime;
        }
        else
        {
            healthFill.color = HealthColor;
        }

        if (Input.GetKeyDown(KeyCode.H)) //Temporary testing for health
        {
            UpdateHealth(playerHealth - 10);
        }

        //Dashes
        dashCooldown.value = playerMovement.dashCharges + ((playerMovement.dashRegenTime - playerMovement.dashRegenTimer) / playerMovement.dashRegenTime) / playerMovement.maxDashCharges;
        dashText.GetComponent<TextMeshProUGUI>().text = $"{playerMovement.dashCharges}";
        if (lastDashCharges != playerMovement.dashCharges)
        {
            lastDashCharges = playerMovement.dashCharges;
            dashFill.color = RechargeColor;
            rechargeFlashTimer = rechargeFlashTime;
        }
        else if (rechargeFlashTimer > 0)
        {
            rechargeFlashTimer -= Time.deltaTime;
        }
        else
        {
            dashFill.color = DashColor;
        }

            dashCooldownPercent = dashCooldown.value; // Using this to present in the inspector for testing purposes

        //Weapons
        currentWeaponImage.sprite = weaponManager.HeldWeapon.WeaponInstance.HudPreview;
        ammoText.GetComponent<TextMeshProUGUI>().text = $"{weaponManager.HeldWeapon.WeaponInstance.Ammo}";
    }

    void UpdateHealth(float newHealth)
    {
        healthBar.value = newHealth / maxHealth;
        playerHealth = newHealth; // Used for video demonstration purposes, will be removed when player status is implemented.
        healthText.GetComponent<TextMeshProUGUI>().text = $"{newHealth / maxHealth * 100}";
        healthFill.color = FlashColor;
        healthFlashTimer = healthFlashTime;
    }

    void UpdateWeaponPreview()
    {
        currentWeaponImage.sprite = weaponManager.HeldWeapon.WeaponInstance.HudPreview;
    }
}
