using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class GI_RaceManager : MonoBehaviour
{
    private float startTime;
    public float raceCountdownDuration = 3;
    public float timeRemaining = 600;
    public float raceDuration = 600;
    public int totalLaps = 3;
    private bool raceInProgress;
    public GameObject RaceFinishedWidget, RaceCountdownWidget;

    public List<FeKaPawn_Base> racers;
    public List<FeKaPawn_Base> racerStandings = new List<FeKaPawn_Base>();


    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartRace();
        }
        
        if (!raceInProgress)
        {
            return;
        }
        UpdateStandings();
        CheckRacerStatus();
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
        }
        else
        {
            timeRemaining = 0;
            StopRace();
        }
    }

    [ContextMenu("Start Race")]
    public void StartRace()
    {
        FindObjectOfType<CheckpointTracker>().Init();
        
        racers.Clear();
        foreach (var feKaPawn in FindObjectsOfType<FeKaPawn_Base>())
        {
            feKaPawn.Init();
            racers.Add(feKaPawn);
        }

        StartCoroutine(RaceCountdown());
    }

    public IEnumerator RaceCountdown()
    {
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        Destroy(widgetManager.GetExistingWidget(RaceCountdownWidget.name));
        widgetManager.AddWidget(RaceCountdownWidget);
        yield return new WaitForSeconds(3);
        
        // RELEASE ZE HOUNDS!!!
        foreach (var racer in racers)
        {
            racer.FeKaCurrentStats.racerState = FeKaPawnStats.RacerState.racing;
        }
        
        timeRemaining = raceDuration;
        startTime = Time.deltaTime;
        raceInProgress = true;
    }

    public void CheckRacerStatus()
    {
        if (haveAllRacersFinished()) StopRace();
        //if (isOneRacerRemaining) StopRace();
    }

    public bool haveAllRacersFinished()
    {
        // End the game if only one racer is alive, or if all racers have placed
        foreach (var racer in racers)
        {
            if (racer.FeKaCurrentStats.currentLap < totalLaps)
            {
                return false;
            }

            // Mark racers as finished
            if (racer.FeKaCurrentStats.racerState != FeKaPawnStats.RacerState.finished)
            {
                // Show the finish screen for the local player
                if (racer.FeKaCurrentStats.controlMode == ControlMode.LocalPlayer)
                {
                    if (!GameInstance.Get<GI_WidgetManager>().GetExistingWidget(RaceFinishedWidget.name))
                    {
                        GameInstance.Get<GI_WidgetManager>().AddWidget(RaceFinishedWidget);
                    }
                }
                // Mark the racers as finished
                racer.FeKaCurrentStats.racerState = FeKaPawnStats.RacerState.finished;
                racer.FeKaCurrentStats.finishPlacement = GetRacerPlace(racer);
                racer.FeKaCurrentStats.finishTime = timeRemaining;
            }
        }

        return true;
    }

    public void StopRace()
    {
        raceInProgress = false;
        foreach (FeKaPawn_Base racer in racers)
        {
            racer.isPaused = true;
        }

        if (!GameInstance.Get<GI_WidgetManager>().GetExistingWidget(RaceFinishedWidget.name))
        {
            GameInstance.Get<GI_WidgetManager>().AddWidget(RaceFinishedWidget);
        }
    }
    
    private void UpdateStandings()
    {
        var checkpointCount = FindObjectOfType<CheckpointTracker>().raceCheckpoints.Count;
    
        racerStandings = new List<FeKaPawn_Base>(racers);
        racerStandings.Sort((a, b) =>
        {
            var progressA = GetRaceProgress(a, checkpointCount);
            var progressB = GetRaceProgress(b, checkpointCount);
            return progressB.CompareTo(progressA);
        });
    }
    
    private float GetRaceProgress(FeKaPawn_Base pawn, int checkpointCount)
    {
        var checkpointTracker = FindObjectOfType<CheckpointTracker>();
        var baseProgress = pawn.FeKaCurrentStats.currentLap * checkpointCount 
                           + pawn.FeKaCurrentStats.currentCheckpoint;
    
        var nextIndex = pawn.FeKaCurrentStats.currentCheckpoint;
        var nextPos = checkpointTracker.raceCheckpoints[nextIndex].transform.position;
        var distToNext = Vector3.Distance(pawn.transform.position, nextPos);
        var fraction = 1f - Mathf.Clamp01(distToNext / 100f); // 100 is the max expected checkpoint spacing (This is a crappy way of handling this and I should blow it up)
    
        return baseProgress + fraction;
    }
    
    public int GetRacerPlace(FeKaPawn pawn)
    {
        return racerStandings.IndexOf((FeKaPawn_Base)pawn) + 1;
    }
}
