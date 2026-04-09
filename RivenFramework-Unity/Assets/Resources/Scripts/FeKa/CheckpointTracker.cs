using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the local visibility and indexing of race checkpoints
/// </summary>
public class CheckpointTracker : MonoBehaviour
{
    [Tooltip("A list of all of the checkpoints on this race track in order")]
    public List<RaceCheckpoint> raceCheckpoints;
    
    public List<Transform> raceStartPoints;

    /// <summary>
    /// Called in the context menu, gathers all the checkpoints that are a child of this object,
    /// in the order they appear in the hierarchy
    /// </summary>
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

    /// <summary>
    /// Called by GI_RaceManager when a race begins, ensures the correct checkpoint is visible to the local player
    /// </summary>
    public void Init()
    {
        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            raceCheckpoints[i].checkpointIndex = i;
            raceCheckpoints[i].gameObject.SetActive(true);
        }
        UpdateVisibilityForLocalPlayer();
    }

    /// <summary>
    /// Called by RaceCheckpoint's OnTriggerEnter function,
    /// Indexes the current checkpoint for racers when they pass through the checkpoint they are on during a race
    /// </summary>
    /// <param name="feKaPawn"></param>
    public void NextCheckpoint(FeKaPawn_Base feKaPawn)
    {
        feKaPawn.FeKaCurrentStats.currentCheckpoint++;
        print($"Called NextCheckpoint for {feKaPawn}");

        // Loop back around when at the last checkpoint
        if (feKaPawn.FeKaCurrentStats.currentCheckpoint >= raceCheckpoints.Count) feKaPawn.FeKaCurrentStats.currentCheckpoint = 0;
        
        // When passing the goal, increase the lap counter for that racer
        if (feKaPawn.FeKaCurrentStats.currentCheckpoint == 0) feKaPawn.FeKaCurrentStats.currentLap++;

        // For the local player, update the visible checkpoints
        if (feKaPawn.controlMode == ControlMode.LocalPlayer)
        {
            print($"Target was a player {feKaPawn.controlMode == ControlMode.LocalPlayer}");
            UpdateVisibilityForLocalPlayer(feKaPawn);
        }
        print($"Target was a player {feKaPawn.controlMode == ControlMode.LocalPlayer}");
    }
    
    /// <summary>
    /// Hide all checkpoints besides the one the local player is currently on during a race
    /// </summary>
    /// <param name="localPawn">If this is null, assume we are just enabling the first checkpoint</param>
    private void UpdateVisibilityForLocalPlayer(FeKaPawn localPawn = null)
    {
        // Either get the targeted local player's current checkpoint, or get the first checkpoint
        var targetCheckpoint = localPawn != null ? localPawn.FeKaCurrentStats.currentCheckpoint : 0;

        // Set the local visibility of all the checkpoints
        for (int i = 0; i < raceCheckpoints.Count; i++)
        {
            var visual = raceCheckpoints[i].visual;
            if (visual != null) visual.gameObject.SetActive(i == targetCheckpoint);
        }
    }
}
