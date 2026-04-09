// =========================================================================================================
// FeralKartGameRules
// =========================================================================================================
// Implements IGameRules for Feral Kart
// Owns all kart-specific server logic: intermission, map selection, player spawning, and race flow
// =========================================================================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;

public class FeralKartGameRules : IGameRules
{
    #region ========================================( Variables )======================================================//

    /*-----[ Constants ]---------------------------------------------------------------------------------------*/
    private const string FEKA_MAGIC = "FeKa";
    private const int LOAD_WAIT_MILLISECONDS = 2000;

    /*-----[ Engine Reference ]--------------------------------------------------------------------------------*/
    private readonly IGameRulesEngine engine;

    /*-----[ Config ]------------------------------------------------------------------------------------------*/
    private readonly FeKa_ServerConfig config;

    /*-----[ Game Phase ]--------------------------------------------------------------------------------------*/
    private GamePhase currentPhase = GamePhase.Intermission;
    private readonly object phaseLock = new object();
    private int intermissionTimeLeft;
    private int raceTimeLeft = 0;
    private string currentMap = "";

    /*-----[ Lookup Table 1: Character/Spectate Choice Per Player ]--------------------------------------------*/
    private readonly Dictionary<ConnectedPlayer, string> playerChoices = new();
    private readonly object playerChoicesLock = new object();

    /*-----[ Lookup Table 2: Network Object ID Per Player ]----------------------------------------------------*/
    private readonly Dictionary<ConnectedPlayer, string> playerNetworkObjects = new();
    private readonly object playerNetworkObjectsLock = new object();

    /*-----[ Lookup Table 3: Race Results Per Player ]---------------------------------------------------------*/
    private readonly Dictionary<ConnectedPlayer, FinishPacket> raceResults = new();
    private readonly object raceResultsLock = new object();

    /*-----[ Threads ]-----------------------------------------------------------------------------------------*/
    private Thread? intermissionThread;
    private Thread? raceTimerThread;

    #endregion


    #region =======================================( Functions )=================m=====================================//

    /*-----[ Constructor ]-------------------------------------------------------------------------------------*/

    public FeralKartGameRules(IGameRulesEngine engine, FeKa_ServerConfig config)
    {
        this.engine = engine;
        this.config = config;
        intermissionTimeLeft = config.IntermissionDuration;
        Log("Entering intermission.");
        StartIntermissionTimer();
    }


    /*-----[ IGameRules Implementation ]-----------------------------------------------------------------------*/

    public void OnPlayerJoined(ConnectedPlayer player)
    {
        lock (phaseLock)
        {
            if (currentPhase == GamePhase.InProgress)
            {
                // Spawn a spectator body for players who join mid-race
                string networkObjectId = engine.RequestSpawnAndGetId("prefab_spectator", 0f, 0f, 0f, 0f, 0f, 0f);
                lock (playerNetworkObjectsLock)
                    playerNetworkObjects[player] = networkObjectId;

                Log($"'{player.Name}' joined mid-race, spawned as spectator.");
            }
        }
    }

    public void OnPlayerLeft(ConnectedPlayer player)
    {
        // Despawn their network body if they had one
        string? networkObjectId = null;
        lock (playerNetworkObjectsLock)
        {
            if (playerNetworkObjects.TryGetValue(player, out networkObjectId))
                playerNetworkObjects.Remove(player);
        }
        if (networkObjectId != null)
            engine.RequestDespawn(networkObjectId);

        if (player.EndPoint != null)
        {
            var clientOwnedIds = engine.GetSpawnIdsByOwner(player.EndPoint.ToString());
            foreach (var ownedId in clientOwnedIds)
                engine.RequestDespawn(ownedId);
        }

        // Remove their choice and results - players who disconnect mid-race do not appear on the results screen
        lock (playerChoicesLock) playerChoices.Remove(player);
        lock (raceResultsLock) raceResults.Remove(player);
    }

    public void OnPlayerReady(ConnectedPlayer player, string choice)
    {
        lock (playerChoicesLock)
            playerChoices[player] = choice;

        Log($"'{player.Name}' is ready (choice: {choice}).");

        // Check if all connected players have now readied up
        var allConnectedPlayers = engine.GetConnectedPlayers();
        bool allPlayersReady = allConnectedPlayers.Count > 0
            && allConnectedPlayers.TrueForAll(p => playerChoices.ContainsKey(p));

        if (allPlayersReady)
        {
            Log("All players are ready.");
            OnAllPlayersReady();
        }
    }

    public void OnAllPlayersReady()
    {
        lock (phaseLock)
        {
            if (currentPhase != GamePhase.Intermission) return;

            // Stop the intermission timer since all players readied up early
            intermissionThread?.Interrupt();

            StartGame();
        }
    }

    public void OnObjectSpawned(SpawnBroadcastPacket spawnedObject)
    {
        // Feral Kart does not need to react to generic spawn confirmations from the engine
    }

    public void OnUnknownPacket(string message, IPEndPoint remote)
    {
        if (message.StartsWith(FEKA_MAGIC + ":FINISH:"))
        {
            string json = message[(FEKA_MAGIC + ":FINISH:").Length..];
            var finishPacket = JsonSerializer.Deserialize<FinishPacket>(json);
            if (finishPacket == null) return;

            var allPlayers = engine.GetConnectedPlayers();
            var sender = allPlayers.Find(p => p.EndPoint?.ToString() == remote.ToString());
            if (sender == null) return;

            Log($"'{sender.Name}' finished the race (failed={finishPacket.Failed}, placement={finishPacket.Placement}).");
            OnFinishPacketReceived(sender, finishPacket);
        }
    }

    public ServerBrowserInfo GetServerBrowserInfo()
    {
        return new ServerBrowserInfo
        {
            MapName = currentMap,
            GameMode = config.GameMode
        };
    }

    public bool ExecuteGameCommand(string cmd, string arg, ConnectedPlayer? source)
    {
        switch (cmd)
        {
            case "gamehelp":
            {
                if (source != null)
                {
                    engine.SendToPlayer(source, FEKA_MAGIC + ":CHAT:Server:Game commands: /startgame, /endgame, /setmap <mapname>");
                    break;
                }
                Console.WriteLine("  startgame               force-start the game immediately");
                Console.WriteLine("  endgame                 force-end the race and return to intermission");
                Console.WriteLine("  setmap <mapname>        change the map and restart the game");
                break;
            }

            case "startgame":
            {
                lock (phaseLock)
                {
                    if (currentPhase == GamePhase.InProgress)
                    {
                        if (source != null)
                            engine.SendToPlayer(source, FEKA_MAGIC + ":CHAT:Server:The game is already in progress.");
                        else
                            Console.WriteLine("  The game is already in progress.");
                        break;
                    }

                    intermissionThread?.Interrupt();
                    Log($"Game force-started by {(source?.Name ?? "console")}.");
                    StartGame();
                }
                break;
            }

            case "endgame":
            {
                lock (phaseLock)
                {
                    if (currentPhase == GamePhase.Intermission)
                    {
                        if (source != null)
                            engine.SendToPlayer(source, FEKA_MAGIC + ":CHAT:Server:Already in intermission.");
                        else
                            Console.WriteLine("  Already in intermission.");
                        break;
                    }

                    raceTimerThread?.Interrupt();
                    Log($"Game force-ended by {(source?.Name ?? "console")}.");
                    OnRaceEnd();
                }
                break;
            }

            case "setmap":
            {
                if (string.IsNullOrEmpty(arg))
                {
                    if (source != null)
                        engine.SendToPlayer(source, FEKA_MAGIC + ":CHAT:Server:Usage: /setmap <mapname>");
                    else
                        Console.WriteLine("  Usage: setmap <mapname>");
                    break;
                }

                if (!config.MapPool.Contains(arg))
                {
                    if (source != null)
                        engine.SendToPlayer(source, FEKA_MAGIC + ":CHAT:Server:Invalid map '" + arg + "'.");
                    else
                        Console.WriteLine($"  Invalid map '{arg}'. Check your feka.config MapPool.");
                    break;
                }

                lock (phaseLock)
                {
                    if (currentPhase == GamePhase.InProgress)
                        raceTimerThread?.Interrupt();

                    intermissionThread?.Interrupt();

                    Log($"Map set to '{arg}' by {(source?.Name ?? "console")}. Starting game.");
                    StartGameWithMap(arg);
                }
                break;
            }

            default:
                return false;
        }

        return true;
    }


    /*-----[ State Broadcasting ]------------------------------------------------------------------------------*/

    public void BroadcastGameState(List<ConnectedPlayer> players)
    {
        string phaseString = PhaseToString(currentPhase);

        var packet = new GameStatePacket
        {
            Phase = phaseString,
            MapName = currentMap,
            GameMode = config.GameMode,
            TimeLeft = currentPhase == GamePhase.InProgress ? raceTimeLeft : intermissionTimeLeft,
            PlayerNames = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs })
        };

        engine.BroadcastToAll(FEKA_MAGIC + ":STATE:" + JsonSerializer.Serialize(packet));
    }

    public void SendWelcomeState(IPEndPoint endpoint, List<ConnectedPlayer> players)
    {
        string phaseString = PhaseToString(currentPhase);

        var packet = new GameStatePacket
        {
            Phase = phaseString,
            MapName = currentMap,
            GameMode = config.GameMode,
            TimeLeft = currentPhase == GamePhase.InProgress ? raceTimeLeft : intermissionTimeLeft,
            PlayerNames = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs })
        };

        engine.SendToEndpoint(endpoint, FEKA_MAGIC + ":STATE:" + JsonSerializer.Serialize(packet));
    }


    /*-----[ Internal Game Flow ]------------------------------------------------------------------------------*/

    private void StartGame()
    {
        StartGameWithMap(PickMap());
    }

    private void StartGameWithMap(string mapName)
    {
        currentMap = mapName;
        currentPhase = GamePhase.Loading;
        intermissionTimeLeft = config.IntermissionDuration;

        lock (raceResultsLock) raceResults.Clear();

        Log($"Loading map '{currentMap}'.");

        engine.BroadcastToAll(FEKA_MAGIC + ":CHAT:Server:Server is changing level...");
        engine.BroadcastToAll(FEKA_MAGIC + ":LOADMAP:" + currentMap);
        engine.BroadcastState();

        // Wait for clients to load the map, then mark the game as in progress and spawn all players
        new Thread(() =>
        {
            Thread.Sleep(LOAD_WAIT_MILLISECONDS);

            lock (phaseLock)
            {
                currentPhase = GamePhase.InProgress;
                Log("Game is now in progress.");
                SpawnAllPlayers();
                lock (playerChoicesLock) playerChoices.Clear();
                engine.BroadcastState();
                StartRaceTimer();
            }
        }) { IsBackground = true }.Start();
    }

    private void SpawnAllPlayers()
    {
        var allPlayers = engine.GetConnectedPlayers();
        foreach (var player in allPlayers)
        {
            string choice;
            lock (playerChoicesLock)
                playerChoices.TryGetValue(player, out choice!);

            // Default to spectator if no choice was recorded (e.g. the timer forced the match to start)
            if (choice == null || choice == "spectate")
            {
                string networkObjectId = engine.RequestSpawnAndGetId("prefab_spectator", 0f, 0f, 0f, 0f, 0f, 0f);
                lock (playerNetworkObjectsLock)
                    playerNetworkObjects[player] = networkObjectId;
            }
        }
    }
    private void StartRaceTimer()
    {
        raceTimerThread = new Thread(() =>
        {
            // TODO: Make race duration configurable in FeKa_ServerConfig
            raceTimeLeft = 300;
            try
            {
                while (raceTimeLeft > 0)
                {
                    Thread.Sleep(1000);
                    lock (phaseLock)
                    {
                        if (currentPhase != GamePhase.InProgress) return;
                        raceTimeLeft--;
                        engine.BroadcastState();
                    }
                }

                Log("Race timer expired.");
                OnRaceEnd();
            }
            catch (ThreadInterruptedException)
            {
                // The race ended early - either all players finished or it was force-ended
            }
        }) { IsBackground = true };
        raceTimerThread.Start();
    }

    private void OnFinishPacketReceived(ConnectedPlayer player, FinishPacket finishPacket)
    {
        lock (raceResultsLock)
            raceResults[player] = finishPacket;

        // Check if all non-spectator players have now finished or failed
        var allPlayers = engine.GetConnectedPlayers();
        List<ConnectedPlayer> racers;
        lock (playerChoicesLock)
            racers = allPlayers.FindAll(p => playerChoices.TryGetValue(p, out var choice) && choice != "spectate");

        bool allRacersDone;
        lock (raceResultsLock)
            allRacersDone = racers.Count > 0 && racers.TrueForAll(p => raceResults.ContainsKey(p));

        if (allRacersDone)
        {
            Log("All racers have finished. Ending race early.");
            raceTimerThread?.Interrupt();
            OnRaceEnd();
        }
    }

    private void OnRaceEnd()
    {
        // Build the results list sorted by placement, with DNFs at the bottom
        List<KeyValuePair<ConnectedPlayer, FinishPacket>> sortedResults;
        lock (raceResultsLock)
            sortedResults = new List<KeyValuePair<ConnectedPlayer, FinishPacket>>(raceResults);

        sortedResults.Sort((firstEntry, secondEntry) =>
        {
            if (firstEntry.Value.Failed != secondEntry.Value.Failed)
                return firstEntry.Value.Failed ? 1 : -1;
            return firstEntry.Value.Placement.CompareTo(secondEntry.Value.Placement);
        });

        var resultsPacket = new RaceResultsPacket
        {
            Results = sortedResults.ConvertAll(entry => new RaceResultEntry
            {
                PlayerName = entry.Key.Name,
                Failed = entry.Value.Failed,
                Placement = entry.Value.Placement,
                FinishTime = entry.Value.FinishTime,
                HealthRemaining = entry.Value.HealthRemaining,
                LivesRemaining = entry.Value.LivesRemaining,
                Kills = entry.Value.Kills,
                DamageTaken = entry.Value.DamageTaken,
                DamageDealt = entry.Value.DamageDealt,
                DamageHealed = entry.Value.DamageHealed
            })
        };

        Log($"Race ended. Broadcasting results for {sortedResults.Count} player(s).");
        engine.BroadcastToAll(FEKA_MAGIC + ":RESULTS:" + JsonSerializer.Serialize(resultsPacket));

        // Despawn all player network bodies
        lock (playerNetworkObjectsLock)
        {
            foreach (var entry in playerNetworkObjects)
                engine.RequestDespawn(entry.Value);
            playerNetworkObjects.Clear();
        }

        // Wait for the results screen to display on clients, then return to intermission
        new Thread(() =>
        {
            Thread.Sleep(8000);
            lock (phaseLock)
            {
                currentPhase = GamePhase.Intermission;
                intermissionTimeLeft = config.IntermissionDuration;
                lock (playerChoicesLock) playerChoices.Clear();
                engine.BroadcastState();
                Log("Entering intermission.");
                StartIntermissionTimer();
            }
        }) { IsBackground = true }.Start();
    }

    private void StartIntermissionTimer()
    {
        intermissionThread = new Thread(() =>
        {
            try
            {
                while (intermissionTimeLeft > 0)
                {
                    Thread.Sleep(1000);
                    lock (phaseLock)
                    {
                        if (currentPhase != GamePhase.Intermission) return;
                        intermissionTimeLeft--;
                        engine.BroadcastState();
                    }
                }

                Log("Intermission timer expired. Starting game.");
                OnAllPlayersReady();
            }
            catch (ThreadInterruptedException)
            {
                // Timer was interrupted because all players readied up early or a command forced a start
            }
        }) { IsBackground = true };
        intermissionThread.Start();
    }

    private string PickMap()
    {
        // TODO Replace with a map voting system when that feature is implemented
        var randomNumberGenerator = new Random();
        return config.MapPool[randomNumberGenerator.Next(config.MapPool.Count)];
    }

    private string PhaseToString(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Intermission: return "Intermission";
            case GamePhase.Loading:      return "Loading";
            case GamePhase.InProgress:   return "InProgress";
            default:                     return "Intermission";
        }
    }

    private void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FeKa] {message}");
    }

    #endregion
}


// =========================================================================================================
// FeKa-specific packet types
// =========================================================================================================

public class RaceResultEntry
{
    public string PlayerName { get; set; } = "";
    public bool Failed { get; set; }
    public int Placement { get; set; }
    public float FinishTime { get; set; }
    public float HealthRemaining { get; set; }
    public int LivesRemaining { get; set; }
    public int Kills { get; set; }
    public float DamageTaken { get; set; }
    public float DamageDealt { get; set; }
    public float DamageHealed { get; set; }
}

public class RaceResultsPacket
{
    public List<RaceResultEntry> Results { get; set; } = new();
}