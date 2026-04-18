using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class GI_RaceManager : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public float raceCountdownDuration = 3;
    public float timeRemaining = 600;
    public float raceDuration = 600;
    public int totalLaps = 3;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public List<FeKaPawn_Base> racers;
    public List<FeKaPawn_Base> racerStandings = new List<FeKaPawn_Base>();
    [Tooltip("The results of the race that just ended, used for the results screen")]
    public List<RaceResultEntry> lastRaceResults = new List<RaceResultEntry>();


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private float startTime;
    private bool raceInProgress;
    private bool raceEnded = false;
    private Coroutine countdownCoroutine;
    public HashSet<FeKaPawn_Base> placedRacers = new HashSet<FeKaPawn_Base>();
    public HashSet<FeKaPawn_Base> eliminatedRacers = new HashSet<FeKaPawn_Base>();


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public GameObject RaceFinishedWidget, RaceResultsWidget, RaceCountdownWidget;
    public FeKa_GameRules fekaGameRules;
    private CheckpointTracker checkpointTracker;


    #endregion

    
    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Start()
    {
        // Subscribe to game state events
        fekaGameRules = FindObjectOfType<FeKa_GameRules>();
        if (fekaGameRules != null) 
            fekaGameRules.OnGameStateReceived += OnGameStateReceived;
        if (fekaGameRules != null && fekaGameRules.lastGameState != null)
            OnGameStateReceived(fekaGameRules.lastGameState);
    }
    public void Update()
    {
        if (!raceInProgress) return;
        UpdateStandings();
        CheckRacerStatus();
    }
    public void OnDestroy()
    {
        // Unsubscribe to game state events
        if (fekaGameRules != null)
            fekaGameRules.OnGameStateReceived -= OnGameStateReceived;
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Update the race manager timeRemaining to match the gameState TimeLeft
    /// </summary>
    private void OnGameStateReceived(FeKa_GameStatePacket gameState)
    {
        timeRemaining = gameState.TimeLeft;
    }
    
    /// <summary>
    /// Update the race placement of the racers based on their checkpoint and race progress
    /// </summary>
    private void UpdateStandings()
    {
        checkpointTracker = FindObjectOfType<CheckpointTracker>();
        if (!checkpointTracker) return;
        var checkpointCount = checkpointTracker.raceCheckpoints.Count;
    
        racerStandings = new List<FeKaPawn_Base>(racers);
        racerStandings.Sort((a, b) =>
        {
            var progressA = GetRaceProgress(a, checkpointCount);
            var progressB = GetRaceProgress(b, checkpointCount);
            return progressB.CompareTo(progressA);
        });
    }
    
    /// <summary>
    /// Gets a racers progress in the race based on their checkpoint and distance
    /// </summary>
    private float GetRaceProgress(FeKaPawn_Base pawn, int checkpointCount)
    {
        checkpointTracker = FindObjectOfType<CheckpointTracker>();
        var baseProgress = pawn.FeKaCurrentStats.currentLap * checkpointCount + pawn.FeKaCurrentStats.currentCheckpoint;
    
        var nextIndex = pawn.FeKaCurrentStats.currentCheckpoint;
        var nextPos = checkpointTracker.raceCheckpoints[nextIndex].transform.position;
        var distToNext = Vector3.Distance(pawn.transform.position, nextPos);
        var fraction = 1f - Mathf.Clamp01(distToNext / 100f); // 100 is the max expected checkpoint spacing (This is a crappy way of handling this and I should blow it up)
    
        return baseProgress + fraction;
    }
    
    /// <summary>
    /// Start the race countdown and release the racers
    /// </summary>
    private IEnumerator RaceCountdown()
    {
        
        yield return new WaitForSeconds(1); // Why was I waiting for 1 second here?
        // Apparently not waiting for a second here causes the players to stay frozen?
        
        // Initialize the checkpoints
        FindObjectOfType<CheckpointTracker>().Init();
        
        // Show the countdown
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        widgetManager.AddWidget(RaceCountdownWidget);
        
        // Reset any lingering race data
        racers.Clear();
        placedRacers.Clear();
        eliminatedRacers.Clear();
        raceEnded = false;
        
        // Populate racers list and position racers at starting points
        checkpointTracker = FindAnyObjectByType<CheckpointTracker>();
        var allPawns =  new List<FeKaPawn_Base>(FindObjectsOfType<FeKaPawn_Base>());
        for (int i = 0; i < allPawns.Count; i++)
        {
            allPawns[i].Init();
            
            if (checkpointTracker.raceStartPoints != null && i < checkpointTracker.raceStartPoints.Count)
            {
                print($"Assinging pawn {allPawns[i].gameObject.name} at index {i}]");
                var startPoint = checkpointTracker.raceStartPoints[i];
                print(startPoint.position);
                print(allPawns[i].transform.position);
                if (allPawns[i] != null)
                    allPawns[i].transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
            }

            racers.Add(allPawns[i]);
        }
        
        // Wait for the countdown to finish
        yield return new WaitForSeconds(3);
        
        // RELEASE ZE HOUNDS!!!
        // (This will unlock player movement)
        foreach (var racer in racers)
        {
            racer.FeKaCurrentStats.racerState = FeKaPawnStats.RacerState.racing;
        }
        
        // Set the race flags
        timeRemaining = raceDuration;
        raceInProgress = true;
        startTime = Time.deltaTime;
        countdownCoroutine = null;
    }
    
    /// <summary>
    /// Buggy buggy messy messy garbage code
    /// </summary>
    private void CheckRacerStatus()
    {
        foreach (var racer in racers)
        {
            if (placedRacers.Contains(racer) || eliminatedRacers.Contains(racer)) continue;

            if (racer.FeKaCurrentStats.currentLap >= totalLaps)
            {
                print($"Racer {racer.controlMode} {racer.displayName} completed race");
                placedRacers.Add(racer);
                racer.FeKaCurrentStats.racerState = FeKaPawnStats.RacerState.finished;
                racer.FeKaCurrentStats.finishPlacement = GetRacerPlacement(racer);
                racer.FeKaCurrentStats.finishTime = timeRemaining-raceDuration;
                if (racer.controlMode == ControlMode.LocalPlayer)
                    SendFinishAndShowResults(racer, false);
            }

            /*if (racer.isDead)
            {
                print($"[gi] player had {racer.FeKaCurrentStats.stocks} stocks");
            }
            if (racer.isDead && racer.FeKaCurrentStats.stocks <= 0)
            {
                print($"[gi] player had {racer.FeKaCurrentStats.stocks} stocks and was marked as eliminated");
                eliminatedRacers.Add(racer);
                if (racer.controlMode == ControlMode.LocalPlayer)
                    SendFinishAndShowResults(racer, true);
            }*/
        }

        if (placedRacers.Count == racers.Count)
        {
            Debug.Log("All racers found to be in placedRacers hashset");
            EndRace();
            return;
        }

        // Multiple players
        if (racers.Count > 1)
        {
            // Either one racer is still standing, or all racers have finished
            int activeRacers = racers.Count - eliminatedRacers.Count - placedRacers.Count;
            if (activeRacers <= 1)
            {
                EndRace();
            }
        }

        // Just a solo player
        else if (racers.Count == 1)
        {
            // Either the solo racer failed, or they finished the race
            int activeRacers = racers.Count - eliminatedRacers.Count - placedRacers.Count;
            if (activeRacers == 0)
            {
                EndRace();
            }
        }
        
        // No players
        else if (racers.Count <= 0)
        {
            EndRace();
        }
    }

    private void EndRace()
    {
        // Exit if the race was already over
        if (raceEnded) 
            return;
        
        // Set the flags
        raceEnded = true;
        raceInProgress = false;
        
        // Freeze the racers
        foreach (var racer in racers)
            if (racer.physicsbody) 
                racer.physicsbody.isKinematic = true;
        
        
        FeKaPawn_Base localRacer = racers.Find(racer => racer.controlMode == ControlMode.LocalPlayer);
        if (localRacer != null && !placedRacers.Contains(localRacer) && !eliminatedRacers.Contains(localRacer))
        {
            localRacer.FeKaCurrentStats.finishPlacement = GetRacerPlacement(localRacer);
            localRacer.FeKaCurrentStats.finishTime = timeRemaining-raceDuration;
            SendFinishAndShowResults(localRacer, true);
        }
    }

    public void SendFinishAndShowResults(FeKaPawn_Base racer, bool failed)
    {
        Debug.Log($"Racer {racer.controlMode} {racer.displayName} sent finish packet");
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


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Called by the GameRules interface (when the game-state is set to in-progress)
    /// </summary>
    [ContextMenu("Start Race")]
    public void StartRace()
    {
        // Safety stop in case the countdown was running (which it shouldn't be (but this fixes that (if it did happen (which it shouldn't))))
        StopAllCoroutines();
        
        // Begin the countdown!
        countdownCoroutine = StartCoroutine(RaceCountdown());
    }
    
    /// <summary>
    /// Get the race placement of a racer participating in the race (Used for displaying info on hud)
    /// </summary>
    public int GetRacerPlacement(FeKaPawn pawn)
    {
        return racerStandings.IndexOf((FeKaPawn_Base)pawn) + 1;
    }

    /// <summary>
    /// Called by the GameRules interface (when a results packet is sent?)
    /// to show the race results screen... and set the race state flags? (I should really move that)
    /// </summary>
    public void ShowResultsFromServer(RaceResultsPacket resultsPacket)
    {
        // Set the race flags
        raceInProgress = false;
        raceEnded = true;

        // Store the race results for the results screen
        lastRaceResults = resultsPacket?.Results ?? new List<RaceResultEntry>();
        
        // Freeze the players
        foreach (var racer in racers)
            if (racer.physicsbody) racer.physicsbody.isKinematic = true;
        
        // Show the results screen
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && !widgetManager.GetExistingWidget(RaceResultsWidget.name))
            widgetManager.AddWidget(RaceResultsWidget);
    }


    #endregion
    





}
