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
        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            raceCheckpoints[i].gameObject.SetActive(false);
        }
        raceCheckpoints[0].gameObject.SetActive(true);
    }

    public void NextCheckpoint(FeKaPawn feKaPawn)
    {
        // If we hit the last checkpoint, increase the lap counter, then loop back around
        if (feKaPawn.FeKaCurrentStats.currentCheckpoint >= raceCheckpoints.Count)
        {
            feKaPawn.FeKaCurrentStats.currentCheckpoint = 0;
            feKaPawn.FeKaCurrentStats.currentLap++;
        }
        else
        {
            feKaPawn.FeKaCurrentStats.currentCheckpoint++;
        }

        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            if (i == feKaPawn.FeKaCurrentStats.currentCheckpoint)
            {
                raceCheckpoints[i].gameObject.SetActive(true);
            }
            else
            {
                raceCheckpoints[i].gameObject.SetActive(false);
            }
        }
    }
}
