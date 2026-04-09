using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceCheckpoint : MonoBehaviour
{
    public int checkpointIndex;
    public GameObject visual;
    private void OnTriggerEnter(Collider other)
    {
        FeKaPawn feKaPawn =  other.GetComponentInParent<FeKaPawn>();
        print($"Object enterered {feKaPawn}");
        if (feKaPawn == null) return;

        if (feKaPawn.FeKaCurrentStats.currentCheckpoint == checkpointIndex)
            FindObjectOfType<CheckpointTracker>().NextCheckpoint((FeKaPawn_Base)feKaPawn);
    }
}
