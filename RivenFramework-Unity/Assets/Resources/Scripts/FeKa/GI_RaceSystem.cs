using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class GI_RaceSystem : MonoBehaviour
{
    private float startTime;
    public float timeRemaining = 600;
    public float raceDuration = 600;
    public int totalLaps = 3;
    private bool raceInProgress;
    public GameObject RaceFinishedWidget;

    public List<FeKaPawn_Base> racers;


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

        GameInstance.Get<GI_WidgetManager>().AddWidget(RaceFinishedWidget);
    }
}
