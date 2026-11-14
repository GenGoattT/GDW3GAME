using Unity.VisualScripting;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private GameManager gameManager;    //The script for the game manager
    private GameObject playerObj;   //The scipt for the players location

    private CheckpointData oldData;

    private void Awake()
    {
        //Finds the game manager object, and gets the script from it
        GameObject gameManagerObj = GameObject.Find("GameManager");
        gameManager = gameManagerObj.GetComponent<GameManager>();

        oldData = gameManagerObj.GetComponent<CheckpointData>();

        playerObj = GameObject.FindWithTag("Player");   //Finds the player's game object
    }

    void Update()
    {
        //Test for player death to reset scene
        if (Input.GetKeyDown(KeyCode.E))
        {
            GoBackToCheckpoint();
        }
    }

    //Resets the game back to it's last save state
    void GoBackToCheckpoint()
    {
        playerObj.transform.position = oldData.oldPlayerLocation;
    }

    //Saves the data when the player touches the checkpoint
    void SaveData()
    {
        oldData.oldPlayerLocation = playerObj.transform.position;

        Debug.Log("Saved");
    }

    //Detects when the player touches teh checkpoint
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SaveData();
        }
    }
}
