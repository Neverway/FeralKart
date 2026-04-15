using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class GI_RaceManager : MonoBehaviour
{
    [Header("Other Stuff")]
    private float startTime;
    public float raceCountdownDuration = 3;
    public float timeRemaining = 600;
    public float raceDuration = 600;
    public int totalLaps = 3;
    private bool raceInProgress;
    public GameObject RaceFinishedWidget, RaceResultsWidget, RaceCountdownWidget;

    public List<FeKaPawn_Base> racers;
    public List<FeKaPawn_Base> racerStandings = new List<FeKaPawn_Base>();
    private Coroutine countdownCoroutine;
    
    private HashSet<FeKaPawn_Base> placedRacers = new HashSet<FeKaPawn_Base>();
    private HashSet<FeKaPawn_Base> eliminatedRacers = new HashSet<FeKaPawn_Base>();
    private bool raceEnded = false;
    public List<RaceResultEntry> lastRaceResults = new List<RaceResultEntry>();
    
    public FeKa_GameRules fekaGameRules;

    public void Start()
    {
        fekaGameRules = FindObjectOfType<FeKa_GameRules>();
        if (fekaGameRules != null) fekaGameRules.OnGameStateReceived += OnGameStateReceived;
        
        if (fekaGameRules != null && fekaGameRules.lastGameState != null)
            OnGameStateReceived(fekaGameRules.lastGameState);
    }

    public void OnDestroy()
    {
        if (fekaGameRules != null)
            fekaGameRules.OnGameStateReceived -= OnGameStateReceived;
    }

    public void Update()
    {
        if (!raceInProgress) return;
        UpdateStandings();
        CheckRacerStatus();
    }

    private void OnGameStateReceived(FeKa_GameStatePacket gameState)
    {
        timeRemaining = gameState.TimeLeft;
    }

    [ContextMenu("Start Race")]
    public void StartRace()
    {
        StopAllCoroutines();
        countdownCoroutine = StartCoroutine(RaceCountdown());
    }

    public IEnumerator RaceCountdown()
    {
        yield return new WaitForSeconds(1);
        FindObjectOfType<CheckpointTracker>().Init();
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        
        widgetManager.AddWidget(RaceCountdownWidget);
        
        racers.Clear();
        placedRacers.Clear();
        eliminatedRacers.Clear();
        raceEnded = false;
        
        var checkpointTracker = FindAnyObjectByType<CheckpointTracker>();
        var allPawns =  new List<FeKaPawn_Base>(FindObjectsOfType<FeKaPawn_Base>());
        for (int i = 0; i < allPawns.Count; i++)
        {
            allPawns[i].Init();
            
            if (checkpointTracker.raceStartPoints != null && i < checkpointTracker.raceStartPoints.Count)
            {
                var startPoint = checkpointTracker.raceStartPoints[i];
                allPawns[i].transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
            }

            racers.Add(allPawns[i]);
        }
        
        yield return new WaitForSeconds(3);
        
        // RELEASE ZE HOUNDS!!!
        foreach (var racer in racers)
        {
            racer.FeKaCurrentStats.racerState = FeKaPawnStats.RacerState.racing;
        }
        
        timeRemaining = raceDuration;
        startTime = Time.deltaTime;
        raceInProgress = true;
        countdownCoroutine = null;
    }

    public void CheckRacerStatus()
    {
        foreach (var racer in racers)
        {
            if (placedRacers.Contains(racer) || eliminatedRacers.Contains(racer)) continue;

            if (racer.FeKaCurrentStats.currentLap >= totalLaps)
            {
                placedRacers.Add(racer);
                racer.FeKaCurrentStats.racerState = FeKaPawnStats.RacerState.finished;
                racer.FeKaCurrentStats.finishPlacement = GetRacerPlace(racer);
                racer.FeKaCurrentStats.finishTime = timeRemaining;
                print("Racer completed race");
                if (racer.controlMode == ControlMode.LocalPlayer)
                    SendFinishAndShowResults(racer, false);
            }
            
            if (racer.isDead && racer.FeKaCurrentStats.stocks <= 0)
                eliminatedRacers.Add(racer);
        }

        print(placedRacers.Count +" ==? " +racers.Count);
        if (placedRacers.Count == racers.Count)
        {
            print("All racers finished");
            EndRace();
            return;
        }

        // All players but one eliminated
        /*if (racers.Count > 1)
        {
            int activeRacers = racers.Count - eliminatedRacers.Count - placedRacers.Count;
            if (activeRacers <= 1)
            {
                print("All racers finished or were eliminated");
                EndRace();
            }
        }*/

        // Just a solo player
        if (racers.Count == 1)
        {
            int activeRacers = racers.Count - eliminatedRacers.Count - placedRacers.Count;
            if (activeRacers == 0)
            {
                print("Solo racer placed");
                EndRace();
            }
        }
        
        // No players
        if (racers.Count <= 0)
        {
            print("No players");
            EndRace();
        }
    }

    public void StopRace()
    {
        /*raceInProgress = false;
        foreach (FeKaPawn_Base racer in racers)
            racer.isPaused = true;
        

        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        if (!widgetManager.GetExistingWidget(RaceFinishedWidget.name))
            widgetManager.AddWidget(RaceFinishedWidget);*/
        EndRace();
    }

    public void EndRace()
    {
        if (raceEnded) return;
        raceEnded = true;
        raceInProgress = false;
        
        foreach (var racer in racers)
            if (racer.physicsbody) 
                racer.physicsbody.isKinematic = true;
        
        var localRacer = racers.Find(racer => racer.controlMode == ControlMode.LocalPlayer);
        if (localRacer != null && !placedRacers.Contains(localRacer))
            SendFinishAndShowResults(localRacer, true);
    }

    private void SendFinishAndShowResults(FeKaPawn_Base racer, bool failed)
    {
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && !widgetManager.GetExistingWidget(RaceFinishedWidget.name))
            widgetManager.AddWidget(RaceFinishedWidget);
        
        GameInstance.Get<FeKa_GameRules>().SendFinishPacket(new FeKa_FinishPacket
        {
            Failed = failed,
            Placement = racer.FeKaCurrentStats.finishPlacement,
            FinishTime = timeRemaining,
            HealthRemaining = racer.FeKaCurrentStats.health,
            LivesRemaining = racer.FeKaCurrentStats.stocks,
            Kills = racer.FeKaCurrentStats.kills,
            DamageTaken = racer.FeKaCurrentStats.damageTaken,
            DamageDealt = racer.FeKaCurrentStats.damageDealt,
            DamageHealed = racer.FeKaCurrentStats.damageHealed
        });
    }
    
    private void UpdateStandings()
    {
        var cpt = FindObjectOfType<CheckpointTracker>();
        if (!cpt) return;
        var checkpointCount = cpt.raceCheckpoints.Count;
    
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

    public void ShowResultsFromServer(RaceResultsPacket resultsPacket)
    {
        raceInProgress = false;
        raceEnded = true;

        lastRaceResults = resultsPacket?.Results ?? new List<RaceResultEntry>();
        
        foreach (var racer in racers)
            if (racer.physicsbody) racer.physicsbody.isKinematic = true;
        
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && !widgetManager.GetExistingWidget(RaceResultsWidget.name))
            widgetManager.AddWidget(RaceResultsWidget);
    }
}
