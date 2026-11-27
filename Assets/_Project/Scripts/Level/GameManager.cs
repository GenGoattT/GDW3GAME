using Project.Combat;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool canExit = false;   //Checks if the player is allowed to leave

    public List<GameObject> enemyL = new List<GameObject>();  //Enemy list

    public 

    void Awake()
    {
        //Sets up all the enemys in the game into an array, then changes it into a list
        GameObject[] enemyArray = GameObject.FindGameObjectsWithTag("enemy");
        enemyL = enemyArray.ToList();
    }

    //Function to see if your able to leave once there are no enemys left in the scene
    public void DestroyEnemys()
    {
        foreach (GameObject enemy in enemyL)
        {
            if (enemy.GetComponent<Health>().IsDead == true)
            {
                enemy.SetActive(false);
            }
        }

        // Checks if the enemie count is below zero and lets the player be able to exit if it is
        if (enemyL.Count <= 0)
        {
            canExit = true;
        }
    }
}
