using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class LevelExit : MonoBehaviour
{
    [SerializeField] string playerTag = "Player";   //Player tag
    [SerializeField] Material canExitColor; //The color for the block when you can exit
    [SerializeField] Material cantExitColor;    //The color for the block when you can't exit

    private GameManager gameManager;    //The script for the game manager

    void Start()
    {
        gameManager = Object.FindFirstObjectByType<GameManager>();  //Finds the game manager script
    }

    void Update()
    {
        //If the player can exit, change the material color
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
