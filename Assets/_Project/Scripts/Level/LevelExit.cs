using NUnit.Framework;
using Project.Combat;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelExit : MonoBehaviour
{
    [SerializeField] string playerTag = "Player";   //Player tag
    [SerializeField] Material canExitColor; //Color of the object when you can leave
    [SerializeField] Material cantExitColor;    //Color of the object when you can't leave

    private GameManager gameManager;    //The script for the game manager

    void Awake()
    {
        //Finds the game manager object, and gets the script from it
        GameObject gameManagerObj = GameObject.Find("GameManager");
        gameManager = gameManagerObj.GetComponent<GameManager>();
    }

    void Update()
    {
        gameManager.DestroyEnemys();

        //If you can exit, chnage the color to indicate it
        if (gameManager.canExit == true)
        {
            GetComponent<Renderer>().material = canExitColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // If the player is within the trigger range, the game ends
        if (other.CompareTag(playerTag) && gameManager.canExit == true)
        {
            Debug.Log("Game Over");
            Time.timeScale = 0.0f;
        }
    }    
}
