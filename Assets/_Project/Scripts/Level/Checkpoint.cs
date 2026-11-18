using Project.Combat;
using Unity.VisualScripting;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private GameManager gameManager;    //The script for the game manager
    private GameObject playerObj;   //The scipt for the players location

    private CheckpointData oldData; //The script for the old data that the checkpoint will use

    private void Awake()
    {
        //Finds the game manager object, and gets the script from it
        GameObject gameManagerObj = GameObject.Find("GameManager");
        gameManager = gameManagerObj.GetComponent<GameManager>();

        playerObj = GameObject.FindWithTag("Player");   //Finds the player's game object
        oldData = playerObj.GetComponent<CheckpointData>(); //Finds the checkpoint data on the player

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
        playerObj.transform.position = oldData.oldPlayerLocation;   //Resets the player location

        //Loops through all the enmies and records their data in the checkpoint data script
        foreach (GameObject enemy in gameManager.enemyL)
        {
            CheckpointData usedData = enemy.GetComponent<CheckpointData>(); //Finds the checkpoint data script
            Health healthData = enemy.GetComponent<Health>();   //Finds the health script

            //Checks to see if it was dead before the checkpoint was activated, then respawns it if it was alive before the checkpoint was activated
            if (usedData.wasDead == false)
            {
                healthData.Revive();    //Revies the enemy
                enemy.SetActive(true);  //Makes the enemy active
                enemy.transform.position = usedData.oldEnemyLocation;   //Resets the eneimes location
                enemy.transform.rotation = usedData.oldEnemyRotation;   //Resets the enemies roatation
                healthData.Heal(usedData.oldHealth);    //Restes the enemies health
            }
        }
    }

    //Detects when the player touches teh checkpoint
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
           playerObj.GetComponent<CheckpointData>().SaveData();
           GameObject[] enemys = GameObject.FindGameObjectsWithTag("enemy");

            foreach (GameObject enemy in  enemys)
            {
                enemy.GetComponent<CheckpointData>().SaveData();
            }

        }
    }
}
