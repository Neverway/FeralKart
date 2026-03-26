using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
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
                newCheckpoint.checkpointIndex = i;
                raceCheckpoints.Add(newCheckpoint);
            }
        }
    }

    public void Init()
    {
        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            raceCheckpoints[i].checkpointIndex = i;
            raceCheckpoints[i].gameObject.SetActive(true);
        }
        
        UpdateVisibilityForLocalPlayer();
    }

    public void NextCheckpoint(FeKaPawn feKaPawn)
    {
        feKaPawn.FeKaCurrentStats.currentCheckpoint++;

        if (feKaPawn.FeKaCurrentStats.currentCheckpoint >= raceCheckpoints.Count)
        {
            feKaPawn.FeKaCurrentStats.currentCheckpoint = 0;
            feKaPawn.FeKaCurrentStats.currentLap++;
            
        }

        if (feKaPawn.FeKaCurrentStats.controlMode == ControlMode.LocalPlayer)
        {
            UpdateVisibilityForLocalPlayer(feKaPawn);
        }
    }
    
    private void UpdateVisibilityForLocalPlayer(FeKaPawn localPawn = null)
    {
        var targetCheckpoint = localPawn != null ? localPawn.FeKaCurrentStats.currentCheckpoint : 0;

        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            var visual = raceCheckpoints[i].visual;
            if (visual != null) visual.gameObject.SetActive(i == targetCheckpoint);
        }
    }
}
