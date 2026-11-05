using UnityEngine;

public class HudWorldTrigger : MonoBehaviour
{
    [SerializeField] private HudController HudController;
    [SerializeField] private string tipDetails;

    void Start()
    {
        if (HudController == null)
        {
            HudController = FindFirstObjectByType<HudController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HudController.updateTip(tipDetails);
        }
    }
}
