using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointTracker : MonoBehaviour
{
    public List<RaceCheckpoint> raceCheckpoints;

    [ContextMenu("Get Checkpoints From Children")]
    public void GetCheckpointsFromChildren()
    {
        raceCheckpoints.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var newCheckpoint = transform.GetChild(i).GetComponent<RaceCheckpoint>();
            if (newCheckpoint != null)
            {
                raceCheckpoints.Add(newCheckpoint);
            }
        }
    }

    public void Start()
    {
    }

    public void Init()
    {
        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            raceCheckpoints[i].gameObject.SetActive(false);
        }
        raceCheckpoints[0].gameObject.SetActive(true);
    }

    public void NextCheckpoint(FeKaPawn feKaPawn)
    {
        // When passing the goal post, increase the lap counter
        if (feKaPawn.FeKaCurrentStats.currentCheckpoint == 0) feKaPawn.FeKaCurrentStats.currentLap++;

        feKaPawn.FeKaCurrentStats.currentCheckpoint++;
        
        // If we hit the last checkpoint then loop back around
        if (feKaPawn.FeKaCurrentStats.currentCheckpoint >= raceCheckpoints.Count)
        {
            feKaPawn.FeKaCurrentStats.currentCheckpoint = 0;
        }

        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            raceCheckpoints[i].gameObject.SetActive(
                i == feKaPawn.FeKaCurrentStats.currentCheckpoint);
        }
    }
}
