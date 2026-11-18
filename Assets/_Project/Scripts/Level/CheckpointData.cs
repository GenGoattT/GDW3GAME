using Project.Combat;
using Project.Enemies;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointData : MonoBehaviour
{
    //The variables for the checkpoint snapshot to be stored in
    public Vector3 oldPlayerLocation;   
    public Vector3 oldEnemyLocation;
    public Quaternion oldEnemyRotation;
    public float oldHealth;
    public bool wasDead;

    private Health healthData;  //The health script

    void Awake()
    {
        healthData = GetComponent<Health>();
    }

    //Saves the data when the player touches the checkpoint
    public void SaveData()
    {
        //Saves the player data
        if (gameObject.CompareTag("Player"))
        {
            oldPlayerLocation = gameObject.transform.position;
        }

        //Saves the enemy data
        if (gameObject.CompareTag("enemy"))
        {
            oldEnemyLocation = gameObject.transform.position;
            oldEnemyRotation = gameObject.transform.rotation;
            oldHealth = healthData.CurrentHealth;
            wasDead = healthData.IsDead;
        }
        Debug.Log("Saved");
        Debug.Log(wasDead);
    }
}
