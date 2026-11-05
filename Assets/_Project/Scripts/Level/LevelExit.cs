using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class LevelExit : MonoBehaviour
{
    private bool canExit = false;   //Checks if the player is allowed to leave

    [SerializeField] List<GameObject> enemyL = new List<GameObject>();  //Enemie list, doesn't need to stay in this script
    [SerializeField] string playerTag = "Player";   //Player tag
    [SerializeField] Material canExitColor;
    [SerializeField] Material cantExitColor;

    void Start()
    {
        
    }

    void Update()
    {
        DestroyEnemys();

        if (canExit == true)
        {
            GetComponent<Renderer>().material = canExitColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // If the player is within the trigger range, the game ends
        if (other.CompareTag(playerTag) && canExit == true)
        {
            Debug.Log("Game Over");
            Time.timeScale = 0.0f;
        }
    }

    // Test function to see if your able to leave once there are no enemys left in the scene
    private void DestroyEnemys()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            for (int i = 0; i < enemyL.Count; i++)
            {
                Destroy(enemyL[i]);
            }
            enemyL.Clear();
        }
        // Checks if the enemie count is below zero and lets the player be able to exit if it is
        if (enemyL.Count <= 0)
        {
            canExit = true;
        }
    }


}
