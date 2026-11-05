using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool canExit = false;   //Checks if the player is allowed to leave

    [SerializeField] List<GameObject> enemyL = new List<GameObject>();  //Enemie list, doesn't need to stay in this script

    void Start()
    {
        
    }

    void Update()
    {
        DestroyEnemys();
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
