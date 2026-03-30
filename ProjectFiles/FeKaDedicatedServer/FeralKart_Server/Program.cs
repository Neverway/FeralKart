using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

const string CONFIG_PATH = "server.config";

// ----------------------------------
// SETUP
// ----------------------------------
// Default server config
if (!File.Exists(CONFIG_PATH))
{
    var defaultConfig = new ServerConfig
    {
        ServerName  = "Feral Kart Server",
        Port        = 27015,
        MaxPlayers  = 16,
        GameMode    = "Race",
        IconPath    = "",
        MapPool     = new List<string> { "midwen_midnightcity", "da_howelfen", "auho_forrest", "wico_cabin", "corgeo_facility", "fo_mechanyon"},
        IntermissionDuration = 30
    };
    File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Created default config at {CONFIG_PATH} - edit it then restart the server.");
}

// Load config
var config = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(CONFIG_PATH)) ?? new ServerConfig();
int    QUERY_PORT        = config.Port;
string PROTOCOL_MAGIC    = "FeKa";
int    INTERMISSION_TIME = config.IntermissionDuration;

// Load icon if path is set
string iconBase64 = "";
if (!string.IsNullOrEmpty(config.IconPath) && File.Exists(config.IconPath))
{
    iconBase64 = Convert.ToBase64String(File.ReadAllBytes(config.IconPath));
    Console.WriteLine($"Loaded icon from {config.IconPath}");
}

// Also allow --port and --icon as overrides on the command line
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--port" && int.TryParse(args[i + 1], out int customPort))
        QUERY_PORT = customPort;
    else if (args[i] == "--icon" && File.Exists(args[i + 1]))
        iconBase64 = Convert.ToBase64String(File.ReadAllBytes(args[i + 1]));
}

// Shared server state
var state = new ServerState
{
    ServerName  = config.ServerName,
    MapName     = config.MapPool.Count > 0 ? config.MapPool[0] : "None",
    GameMode    = config.GameMode,
    MaxPlayers  = config.MaxPlayers,
    PlayerCount = 0,
    IconBase64  = iconBase64
};


// Player list
var players     = new List<ConnectedPlayer>();
var playersLock = new object();

// Game phase
GamePhase   phase          = GamePhase.Intermission;
var   phaseLock      = new object();
int   intermissionTimeLeft = INTERMISSION_TIME;
// Map voting
var votes     = new Dictionary<string, int>();
var votesLock = new object();

// UDP query listener
var querySocket = new UdpClient(new IPEndPoint(IPAddress.Any, QUERY_PORT));
Log($"Query listener on UDP port {QUERY_PORT}");

// ----------------------------------
// Functions
// ----------------------------------
// Logging
void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

void PrintPlayers()
{
    lock (playersLock)
    {
        if (players.Count == 0) { Console.WriteLine("  No players connected."); return; }
        for (int i = 0; i < players.Count; i++)
        {
            string flags = "";
            if (players[i].IsReady)     flags += " [READY]";
            if (players[i].IsSpectator) flags += " [SPECTATOR]";
            Console.WriteLine($"  [{i}] {players[i].Name}{flags}  ({players[i].EndPoint})");
        }
    }
}

string PickWinningMap()
{
    // Pick the map with the most votes, break ties randomly
    string winner   = config.MapPool[new Random().Next(config.MapPool.Count)];
    int    topVotes = 0;
    var    tied     = new List<string>();

    lock (votesLock)
    {
        foreach (var kv in votes)
        {
            if (kv.Value > topVotes) { topVotes = kv.Value; winner = kv.Key; tied.Clear(); tied.Add(kv.Key); }
            else if (kv.Value == topVotes) tied.Add(kv.Key);
        }
        if (tied.Count > 1) winner = tied[new Random().Next(tied.Count)];
    }
    return winner;
}

void BroadcastToAll(string message)
{
    byte[] data = Encoding.UTF8.GetBytes(message);
    lock (playersLock)
    {
        foreach (var p in players)
        {
            if (p.EndPoint == null) continue; 
            try { querySocket.Send(data, data.Length, p.EndPoint); }
            catch { /* player will time out naturally */ }
        }
    }
}

// Takes a pre-snapshotted player list so it can be called from inside a playersLock block without deadlocking
void BroadcastStateWithSnapshot(List<PlayerNameEntry> snapshot)
{
    string phaseStr = phase switch
    {
        GamePhase.Intermission => "Intermission",
        GamePhase.Loading      => "Loading",
        GamePhase.InProgress   => "InProgress",
        _                      => "Intermission"
    };

    var statePacket = new GameStatePacket
    {
        Phase       = phaseStr,
        MapName     = state.MapName,
        GameMode    = state.GameMode,
        TimeLeft    = intermissionTimeLeft,
        PlayerNames = snapshot
    };

    byte[] data = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":STATE:" + JsonSerializer.Serialize(statePacket));
    lock (playersLock)
    {
        foreach (var p in players)
        {
            if (p.EndPoint == null) continue;
            try { querySocket.Send(data, data.Length, p.EndPoint); }
            catch { /* player will time out naturally */ }
        }
    }
}

void BroadcastState()
{
    List<PlayerNameEntry> snapshot;
    lock (playersLock)
    {
        snapshot = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs });
    }
    BroadcastStateWithSnapshot(snapshot);
}

void StartGame()
{
    string targetMap = PickWinningMap();
    state.MapName    = targetMap;
    phase            = GamePhase.Loading;
    intermissionTimeLeft = INTERMISSION_TIME;

    // Clear votes and ready flags
    lock (votesLock)  { votes.Clear(); }
    lock (playersLock)
    {
        foreach (var p in players) p.IsReady = false;
    }

    Log($"Starting game on {targetMap}");
    BroadcastToAll(PROTOCOL_MAGIC + ":LOADMAP:" + targetMap);

    // Give clients 15s to load, then mark in progress
    new Thread(() =>
    {
        Thread.Sleep(15000);
        lock (phaseLock)
        {
            phase = GamePhase.InProgress;
            BroadcastState();
            Log("Game in progress");
        }
    }) { IsBackground = true }.Start();
}

void EndGame()
{
    lock (phaseLock)
    {
        phase = GamePhase.Intermission;
        intermissionTimeLeft = INTERMISSION_TIME;
        lock (playersLock) { foreach (var p in players) p.IsReady = false; }
        BroadcastState();
        Log("Game ended, entering intermission");
    }
}


// ----------------------------------
// THREADS
// ----------------------------------
// UDP Thread
new Thread(() =>
{
    while (true)
    {
        try
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            string message = Encoding.UTF8.GetString(querySocket.Receive(ref remote));
            
            // Handle Query
            if (message.StartsWith(PROTOCOL_MAGIC + ":QUERY"))
            {

                var payload = new ServerQueryResponse
                {
                    ServerName = state.ServerName,
                    MapName = state.MapName,
                    GameMode = state.GameMode,
                    MaxPlayers = state.MaxPlayers,
                    PlayerCount = state.PlayerCount,
                    IconBase64 = state.IconBase64,
                };

                byte[] reply = Encoding.UTF8.GetBytes(
                    PROTOCOL_MAGIC + ":RESPONSE:" + JsonSerializer.Serialize(payload));
            
                querySocket.Send(reply, reply.Length, remote);
                Log($"Query answered: {remote.Address}");
            }
            
            // Join
            else if (message.StartsWith(PROTOCOL_MAGIC + ":JOIN:"))
            {
                string playerName = message.Substring((PROTOCOL_MAGIC + ":JOIN:").Length).Trim();
                if (string.IsNullOrEmpty(playerName)) playerName = "NetPlayer";

                bool didJoin = false;
                lock (playersLock)
                {
                    if (players.Count >= state.MaxPlayers)
                    {
                        byte[] full = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":JOIN:REJECTED:Server is full");
                        querySocket.Send(full, full.Length, remote);
                        Log($"Rejected {playerName} ({remote.Address}) - server is full");
                    }
                    else
                    {
                        // Remove any stale entry from the same endpoint
                        players.RemoveAll(p => p.EndPoint.ToString() == remote.ToString());

                        players.Add(new ConnectedPlayer
                        {
                            Name      = playerName,
                            EndPoint  = remote,
                            LastSeen  = DateTime.UtcNow
                        });
                        state.PlayerCount = players.Count;

                        byte[] accepted = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":JOIN:ACCEPTED");
                        querySocket.Send(accepted, accepted.Length, remote);
                        Log($"{playerName} joined ({remote.Address}) - {players.Count}/{state.MaxPlayers} players");
                        didJoin = true;
                        
                        var newPlayerEndpoint = remote;
                        new Thread(() =>
                        {
                            // Wait 500ms so the client has time to start its ListenLoop after receiving ACCEPTED
                            Thread.Sleep(500);

                            string phaseStr = phase switch
                            {
                                GamePhase.Intermission => "Intermission",
                                GamePhase.Loading      => "Loading",
                                GamePhase.InProgress   => "InProgress",
                                _                      => "Intermission"
                            };
                            var welcomePacket = new GameStatePacket
                            {
                                Phase       = phaseStr,
                                MapName     = state.MapName,
                                GameMode    = state.GameMode,
                                TimeLeft    = intermissionTimeLeft,
                                PlayerNames   = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs })
                            };
                            byte[] stateBytes = Encoding.UTF8.GetBytes(
                                PROTOCOL_MAGIC + ":STATE:" + JsonSerializer.Serialize(welcomePacket));
                            try { querySocket.Send(stateBytes, stateBytes.Length, newPlayerEndpoint); }
                            catch { }
                        }) { IsBackground = true }.Start();
                    }
                }
                if (didJoin) BroadcastState();   
            }
            
            // Heartbeat
            else if (message.StartsWith(PROTOCOL_MAGIC + ":HEARTBEAT"))
            {
                lock (playersLock)
                {
                    var player = players.Find(p => p.EndPoint.ToString() == remote.ToString());
                    if (player != null)
                    {
                        // Measure time since last heartbeat as an approximation of latency
                        player.LastPingMs = (int)(DateTime.UtcNow - player.LastSeen).TotalMilliseconds;
                        player.LastSeen   = DateTime.UtcNow;
                    }
                }
            }
            
            // The ping's pong!
            else if (message.StartsWith(PROTOCOL_MAGIC + ":PONG"))
            {
                lock (playersLock)
                {
                    var player = players.Find(p => p.EndPoint.ToString() == remote.ToString());
                    if (player != null)
                        player.LastPingMs = (int)(DateTime.UtcNow - player.LastPingSent).TotalMilliseconds;
                }
            }

            // Leave
            else if (message.StartsWith(PROTOCOL_MAGIC + ":LEAVE"))
            {
                bool didLeave = false;
                lock (playersLock)
                {
                    var leaving = players.Find(p => p.EndPoint.ToString() == remote.ToString());
                    if (leaving != null)
                    {
                        players.Remove(leaving);
                        state.PlayerCount = players.Count;
                        Log($"{leaving.Name} left ({remote.Address}) - {players.Count}/{state.MaxPlayers} players");
                        didLeave = true;
                    }
                }
                if (didLeave) BroadcastState();  
            }
            
            // Ready up
            else if (message.StartsWith(PROTOCOL_MAGIC + ":READY"))
            {
                List<PlayerNameEntry> snapshot = null;
                lock (playersLock)
                {
                    var player = players.Find(p => p.EndPoint.ToString() == remote.ToString());
                    if (player != null)
                    {
                        player.IsReady = true;
                        Log($"{player.Name} is ready");
                        snapshot = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs });
                    }
                }
                if (snapshot != null) BroadcastStateWithSnapshot(snapshot);
            }

            // Map vote
            else if (message.StartsWith(PROTOCOL_MAGIC + ":VOTE:"))
            {
                string votedMap = message.Substring((PROTOCOL_MAGIC + ":VOTE:").Length).Trim();
                if (config.MapPool.Contains(votedMap))
                {
                    lock (votesLock)
                    {
                        // One vote per player, so clear their previous vote first
                        var player = players.Find(p => p.EndPoint.ToString() == remote.ToString());
                        if (player != null)
                        {
                            if (player.CurrentVote != null && votes.ContainsKey(player.CurrentVote))
                                votes[player.CurrentVote]--;
                            player.CurrentVote = votedMap;
                            if (!votes.ContainsKey(votedMap)) votes[votedMap] = 0;
                            votes[votedMap]++;
                            Log($"{player?.Name ?? remote.Address.ToString()} voted for {votedMap}");

                            // Broadcast updated vote tally
                            BroadcastToAll(PROTOCOL_MAGIC + ":MAPVOTE:" + JsonSerializer.Serialize(votes));
                        }
                    }
                }
            }

            // Spectate request
            else if (message.StartsWith(PROTOCOL_MAGIC + ":SPECTATE"))
            {
                lock (playersLock)
                {
                    var player = players.Find(p => p.EndPoint.ToString() == remote.ToString());
                    if (player != null)
                    {
                        player.IsSpectator = true;
                        Log($"{player.Name} is now spectating");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log($"ERR: {e.Message}");
        }
    }
}) { IsBackground = true }.Start();

// Heartbeat timeout - boots out flatlined players
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(5000);
        bool didTimeout = false;
        lock (playersLock)
        {
            int removed = players.RemoveAll(p => (DateTime.UtcNow - p.LastSeen).TotalSeconds > 15);
            if (removed > 0)
            {
                state.PlayerCount = players.Count;
                Log($"Timed out {removed} player(s) - {players.Count}/{state.MaxPlayers} players");
                didTimeout = true;
            }
        }
        if (didTimeout) BroadcastState();
    }
}) { IsBackground = true }.Start();

// Ping thread - measures round-trip time for each player
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(5000);
        lock (playersLock)
        {
            foreach (var p in players)
            {
                p.LastPingSent = DateTime.UtcNow;
                byte[] pingPacket = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":PING");
                try { querySocket.Send(pingPacket, pingPacket.Length, p.EndPoint); }
                catch { }
            }
        }
    }
}) { IsBackground = true }.Start();

// Intermission timer
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(1000);

        lock (phaseLock)
        {
            if (phase != GamePhase.Intermission) continue;

            // Check if all players are ready
            bool allReady = false;
            lock (playersLock)
            {
                allReady = players.Count > 0 && players.TrueForAll(p => p.IsReady);
            }

            intermissionTimeLeft--;
            BroadcastState();

            if (intermissionTimeLeft <= 0 || allReady)
                StartGame();
        }
    }
}) { IsBackground = true }.Start();

// Command loop
Log("Server ready! Type 'help' for a list of available commands.");

while (true)
{
    Console.Write("==> ");
    string? raw = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(raw)) continue;

    string[] parts = raw.Split(' ', 2);
    string cmd = parts[0].ToLower();
    string arg = parts.Length > 1 ? parts[1] : "";

    switch (cmd)
    {
        case "help":
            Console.WriteLine("  status                     show the current server info");
            Console.WriteLine("  setname       <text>       set the servers name");
            Console.WriteLine("  setmap        <text>       set the current map");
            Console.WriteLine("  setgamemode   <text>       set the current gamemode");
            Console.WriteLine("  setmaxplayers <n>          set the max player count");
            Console.WriteLine("  seticon       <base64>     set the server icon (paste in a base64-encoded PNG)");
            Console.WriteLine("  quit                       shut down the server");
            Console.WriteLine("  players                    list connected players");
            Console.WriteLine("  kick <index or name>       kick a player");
            Console.WriteLine("  startgame                  force start the game");
            Console.WriteLine("  endgame                    force end the game");
            Console.WriteLine("  phase                      show current game phase");
            Console.WriteLine("  mappool                    list the map pool");
            break;
        
        case "status":
            Console.WriteLine($"  Name:         {state.ServerName}");
            Console.WriteLine($"  Map:          {state.MapName}");
            Console.WriteLine($"  GameMode:     {state.GameMode}");
            Console.WriteLine($"  Players:      {state.PlayerCount}/{state.MaxPlayers}");
            Console.WriteLine($"  Icon:         {(string.IsNullOrEmpty(state.IconBase64))}");
            break;
        
        case "setname": state.ServerName = arg; Log($"Set server name to '{arg}'"); break;
        case "setmap": state.MapName = arg; Log($"Set current map to '{arg}'"); break;
        case "setgamemode": state.GameMode = arg; Log($"Set current game mode to '{arg}'"); break;
        case "seticon": state.IconBase64 = arg; Log($"Updated server icon"); break;
        
        case "setmaxplayers":
            if (int.TryParse(arg, out int n))
            {
                state.MaxPlayers = n;
                Log($"Updated max player count to '{n}'");
            }
            else
            {
                Console.WriteLine($"Couldn't interpert '{arg}' as number");
                Console.WriteLine($"Usage: Setmaxplayers <number>");
            }
            break;
        
        case "quit":
            Log("Shutting down...");
            // Notify all connected players
            lock (playersLock)
            {
                byte[] shutdownPacket = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":KICKED:The server has shut down.");
                foreach (var p in players)
                {
                    if (p.EndPoint == null) continue;
                    try { querySocket.Send(shutdownPacket, shutdownPacket.Length, p.EndPoint); }
                    catch { }
                }
            }
            querySocket.Close();
            return;
        
        case "players":
            PrintPlayers();
            break;

        case "kick":
            lock (playersLock)
            {
                if (players.Count == 0) { Console.WriteLine("No players connected"); break; }

                ConnectedPlayer? target = null;

                if (int.TryParse(arg, out int kickIndex))
                {
                    if (kickIndex < 0 || kickIndex >= players.Count)
                    { Console.WriteLine($"No player at index {kickIndex}"); break; }
                    target = players[kickIndex];
                }
                else
                {
                    target = players.Find(p => p.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
                }

                if (target == null) { Console.WriteLine($"No player named '{arg}'"); break; }

                byte[] kickPacket = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":KICKED:You were kicked by the server");
                querySocket.Send(kickPacket, kickPacket.Length, target.EndPoint);
                players.Remove(target);
                state.PlayerCount = players.Count;
                Log($"Kicked {target.Name}");
            }
            break;
        
        case "startgame":
            lock (phaseLock)
            {
                if (phase == GamePhase.InProgress)
                { Console.WriteLine("Game is already in progress"); break; }
                StartGame();
                Log("Game force-started by console");
            }
            break;

        case "endgame":
            lock (phaseLock)
            {
                if (phase == GamePhase.Intermission)
                { Console.WriteLine("Already in intermission"); break; }
                EndGame();
                Log("Game force-ended by console");
            }
            break;

        case "phase":
            lock (phaseLock) { Console.WriteLine($"  Phase: {phase}  TimeLeft: {intermissionTimeLeft}s"); }
            break;

        case "mappool":
            Console.WriteLine("  Map pool:");
            for (int i = 0; i < config.MapPool.Count; i++)
                Console.WriteLine($"  [{i}] {config.MapPool[i]}");
            break;
        
        default:
            Console.WriteLine($"Unknown command '{cmd}'. Type 'help' for a list of available commands.");
            break;
    }
}

// ----------------------------------
// My super cool data classes
// ----------------------------------
class ServerConfig
{
    public string       ServerName           { get; set; } = "Feral Kart Server";
    public int          Port                 { get; set; } = 27015;
    public int          MaxPlayers           { get; set; } = 16;
    public string       GameMode             { get; set; } = "Race";
    public string       IconPath             { get; set; } = "";
    public List<string> MapPool              { get; set; } = new();
    public int          IntermissionDuration { get; set; } = 30;
}

class ServerState
{
    public string ServerName { get; set; } = "";
    public string MapName { get; set; } = "";
    public string GameMode { get; set; } = "";
    public int MaxPlayers { get; set; } = 16;
    public int PlayerCount { get; set; } = 0;
    public string IconBase64 { get; set; } = "";
}

class ServerQueryResponse
{
    public string ServerName { get; set; } = "";
    public string MapName { get; set; } = "";
    public string GameMode { get; set; } = "";
    public int MaxPlayers { get; set; } = 16;
    public int PlayerCount { get; set; } = 0;
    public string IconBase64 { get; set; } = "";
}

class ConnectedPlayer
{
    public string Name { get; set; } = "Player";
    public IPEndPoint? EndPoint { get; set; } = null;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastPingSent { get; set; } = DateTime.UtcNow;
    public int LastPingMs { get; set; } = 0;
    public bool IsReady { get; set; } = false;
    public bool IsSpectator { get; set; } = false;
    public string? CurrentVote { get; set; } = null;     
}

class PlayerNameEntry
{
    public string name { get; set; } = "";
    public int ping { get; set; } = 0;
}

class GameStatePacket
{
    public string              Phase       { get; set; } = "Intermission";
    public string              MapName     { get; set; } = "";
    public string              GameMode    { get; set; } = "";
    public int                 TimeLeft    { get; set; } = 30;
    public List<PlayerNameEntry> PlayerNames { get; set; } = new();
}

enum GamePhase { Intermission, Loading, InProgress }