using Project.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class HudController : MonoBehaviour
{
    [Header("Canvas elements")]
    //[SerializeField] Canvas canvas;
    [SerializeField] UnityEngine.UI.Slider healthBar;
    [SerializeField] GameObject healthText;
    [SerializeField] GameObject ammoText;
    [SerializeField] UnityEngine.UI.Image currentWeaponImage;
    [SerializeField] UnityEngine.UI.Image secondWeaponImage;
    [SerializeField] UnityEngine.UI.Image thirdWeaponImage;
    [SerializeField] UnityEngine.UI.Slider dashCooldown;

    [Header("References")]
    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] WeaponManager weaponManager;
    //Don't have any other resources in the game that need tracking right now.
    //[SerializeField] PlayerStatus playerStatus;

    [Header("Temporary fields")]
    [Tooltip("This is a temporary field for testing purposes. It will be removed when the player status is implemented.")]
    [SerializeField] int maxHealth = 100;
    [SerializeField] int playerHealth = 70;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        healthBar.value = (float)playerHealth / maxHealth;
        healthText.GetComponent<TextMeshProUGUI>().text = $"{playerHealth/maxHealth}";

        dashCooldown.value = playerMovement.dashCharges;
    }
}
