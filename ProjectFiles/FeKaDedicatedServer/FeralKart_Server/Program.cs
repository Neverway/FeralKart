// =========================================================================================================
// Feral Kart - Dedicated Game Server
// =========================================================================================================
// Handles all server-side logic: player connections, game phases, map voting, and console commands.
// Communicates with clients over UDP using the FeKa protocol.
//
// Protocol packet format:   FeKa:<COMMAND>[:<JSON_PAYLOAD>]
//
// Incoming (client -> server):
//   FeKa:QUERY              - Server browser ping, no auth required
//   FeKa:JOIN:<name>        - Request to join the session
//   FeKa:LEAVE              - Graceful disconnect
//   FeKa:HEARTBEAT          - Keep-alive, sent every 5s
//   FeKa:PONG               - Response to a server PING, used to measure RTT
//   FeKa:READY              - Player signals they are ready to start
//   FeKa:VOTE:<mapname>     - Player casts a map vote during intermission
//   FeKa:SPECTATE           - Player switches to spectator mode
//   FeKa:CHAT:<text>        - Player sends a chat message (name is resolved server-side)
//
// Outgoing (server -> client):
//   FeKa:RESPONSE:<json>    - Reply to a QUERY
//   FeKa:JOIN:ACCEPTED      - Join approved
//   FeKa:JOIN:REJECTED:<r>  - Join denied, reason appended
//   FeKa:STATE:<json>       - Full game state broadcast (phase, map, players, time)
//   FeKa:LOADMAP:<mapname>  - Tell all clients to load a map
//   FeKa:MAPVOTE:<json>     - Updated vote tally broadcast
//   FeKa:PING               - Server-initiated RTT probe
//   FeKa:KICKED:<reason>    - Player was removed from the session
//   FeKa:CHAT:<name>:<text> - Chat message broadcast to all clients (server stamps the sender name)
// =========================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;


// =========================================================================================================
// SECTION 1 - CONFIGURATION
// Load or create server.config, then apply any command-line overrides.
// =========================================================================================================

const string CONFIG_PATH     = "server.config";
const string PROTOCOL_MAGIC  = "FeKa";

// Create a default config if one doesn't exist yet
if (!File.Exists(CONFIG_PATH))
{
    var defaultConfig = new ServerConfig
    {
        ServerName  = "Feral Kart Server",
        Port        = 27015,
        MaxPlayers  = 16,
        GameMode    = "Race",
        IconPath    = "",
        MapPool     = new List<string>
        {
            "midwen_midnightcity",
            "da_howelfen",
            "auho_forrest",
            "wico_cabin",
            "corgeo_facility",
            "fo_mechanyon"
        },
        IntermissionDuration = 30
    };

    File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Created default config at '{CONFIG_PATH}' - edit it then restart the server.");
}

var config = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(CONFIG_PATH)) ?? new ServerConfig();

int queryPort        = config.Port;
int intermissionTime = config.IntermissionDuration;

// Load server icon, if one is configured
string iconBase64 = "";
if (!string.IsNullOrEmpty(config.IconPath) && File.Exists(config.IconPath))
{
    iconBase64 = Convert.ToBase64String(File.ReadAllBytes(config.IconPath));
    Console.WriteLine($"Loaded icon from '{config.IconPath}'");
}

// Command-line argument overrides (--port <n>  and  --icon <path>)
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--port" && int.TryParse(args[i + 1], out int customPort))
        queryPort = customPort;
    else if (args[i] == "--icon" && File.Exists(args[i + 1]))
        iconBase64 = Convert.ToBase64String(File.ReadAllBytes(args[i + 1]));
}


// =========================================================================================================
// SECTION 2 - SERVER STATE
// All shared mutable state lives here. Every field accessed from multiple threads is protected by a lock.
// =========================================================================================================

var state = new ServerState
{
    ServerName  = config.ServerName,
    MapName     = config.MapPool.Count > 0 ? config.MapPool[0] : "None",
    GameMode    = config.GameMode,
    MaxPlayers  = config.MaxPlayers,
    PlayerCount = 0,
    IconBase64  = iconBase64
};

// Connected players  -  guarded by playersLock
var players     = new List<ConnectedPlayer>();
var playersLock = new object();

// Game phase and intermission countdown  -  guarded by phaseLock
GamePhase phase               = GamePhase.Intermission;
var       phaseLock           = new object();
int       intermissionTimeLeft = intermissionTime;

// Map votes keyed by map name  -  guarded by votesLock
var votes     = new Dictionary<string, int>();
var votesLock = new object();

// Single UDP socket shared by all threads
var querySocket = new UdpClient(new IPEndPoint(IPAddress.Any, queryPort));
Log($"UDP listener started on port {queryPort}");


// =========================================================================================================
// SECTION 3 - LOGGING
// =========================================================================================================

void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");


// =========================================================================================================
// SECTION 4 - BROADCAST HELPERS
// Convenience methods for pushing packets out to connected clients.
// =========================================================================================================

/// <summary>Sends a raw UTF-8 string packet to every connected player.</summary>
void BroadcastToAll(string message)
{
    byte[] data = Encoding.UTF8.GetBytes(message);
    lock (playersLock)
    {
        foreach (var p in players)
        {
            if (p.EndPoint == null) continue;
            try { querySocket.Send(data, data.Length, p.EndPoint); }
            catch { /* Player will time out naturally */ }
        }
    }
}

/// <summary>
/// Broadcasts a STATE packet built from a pre-snapshotted player list.
/// Call this when you already hold playersLock and need to avoid a deadlock.
/// </summary>
void BroadcastStateWithSnapshot(List<PlayerNameEntry> snapshot)
{
    string phaseStr = PhaseToString(phase);

    var packet = new GameStatePacket
    {
        Phase       = phaseStr,
        MapName     = state.MapName,
        GameMode    = state.GameMode,
        TimeLeft    = intermissionTimeLeft,
        PlayerNames = snapshot
    };

    byte[] data = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":STATE:" + JsonSerializer.Serialize(packet));
    lock (playersLock)
    {
        foreach (var p in players)
        {
            if (p.EndPoint == null) continue;
            try { querySocket.Send(data, data.Length, p.EndPoint); }
            catch { }
        }
    }
}

/// <summary>Snapshots the current player list and broadcasts a STATE packet.</summary>
void BroadcastState()
{
    List<PlayerNameEntry> snapshot;
    lock (playersLock)
        snapshot = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs });

    BroadcastStateWithSnapshot(snapshot);
}


// =========================================================================================================
// SECTION 5 - GAME FLOW
// High-level game lifecycle: starting a race, ending one, and picking the winning map.
// =========================================================================================================

/// <summary>Returns the string label used in STATE packets for the given phase.</summary>
string PhaseToString(GamePhase p) => p switch
{
    GamePhase.Intermission => "Intermission",
    GamePhase.Loading      => "Loading",
    GamePhase.InProgress   => "InProgress",
    _                      => "Intermission"
};

/// <summary>
/// Picks the map that received the most votes during intermission.
/// Ties are broken randomly. Falls back to a random pool map if nobody voted.
/// </summary>
string PickWinningMap()
{
    var rng     = new Random();
    string winner = config.MapPool[rng.Next(config.MapPool.Count)];
    int topVotes  = 0;
    var tied      = new List<string>();

    lock (votesLock)
    {
        foreach (var kv in votes)
        {
            if      (kv.Value > topVotes) { topVotes = kv.Value; winner = kv.Key; tied.Clear(); tied.Add(kv.Key); }
            else if (kv.Value == topVotes)  tied.Add(kv.Key);
        }
        if (tied.Count > 1) winner = tied[rng.Next(tied.Count)];
    }
    return winner;
}

/// <summary>Picks the winning map, tells all clients to load it, and transitions to Loading.</summary>
void StartGame()
{
    string targetMap  = PickWinningMap();
    state.MapName     = targetMap;
    phase             = GamePhase.Loading;
    intermissionTimeLeft = intermissionTime;

    // Clear votes and ready flags so the next intermission starts clean
    lock (votesLock)  { votes.Clear(); }
    lock (playersLock){ foreach (var p in players) p.IsReady = false; }

    Log($"Starting game on '{targetMap}'");
    BroadcastState();
    BroadcastToAll(PROTOCOL_MAGIC + ":LOADMAP:" + targetMap);

    // After 15 seconds, assume all clients have loaded and mark the game as in progress
    new Thread(() =>
    {
        Thread.Sleep(15_000);
        lock (phaseLock)
        {
            phase = GamePhase.InProgress;
            BroadcastState();
            Log("Game is now in progress");
        }
    }) { IsBackground = true }.Start();
}

/// <summary>Transitions back to intermission and resets all player ready flags.</summary>
void EndGame()
{
    lock (phaseLock)
    {
        phase                = GamePhase.Intermission;
        intermissionTimeLeft = intermissionTime;
        lock (playersLock) { foreach (var p in players) p.IsReady = false; }
        BroadcastState();
        Log("Game ended - entering intermission");
    }
}


// =========================================================================================================
// SECTION 6 - MESSAGE HANDLERS
// One method per incoming packet type. Called from the UDP listener thread.
// Each handler receives the full raw message string and the sender's endpoint.
// =========================================================================================================

void HandleQuery(IPEndPoint remote)
{
    var payload = new ServerQueryResponse
    {
        ServerName  = state.ServerName,
        MapName     = state.MapName,
        GameMode    = state.GameMode,
        MaxPlayers  = state.MaxPlayers,
        PlayerCount = state.PlayerCount,
        IconBase64  = state.IconBase64
    };

    byte[] reply = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":RESPONSE:" + JsonSerializer.Serialize(payload));
    querySocket.Send(reply, reply.Length, remote);
    Log($"Query answered: {remote.Address}");
}

void HandleJoin(string message, IPEndPoint remote)
{
    string prefix     = PROTOCOL_MAGIC + ":JOIN:";
    string playerName = message.Length > prefix.Length ? message[prefix.Length..].Trim() : "";
    if (string.IsNullOrEmpty(playerName)) playerName = "NetPlayer";

    bool didJoin = false;
    lock (playersLock)
    {
        if (players.Count >= state.MaxPlayers)
        {
            // Server is full - reject immediately
            Send(PROTOCOL_MAGIC + ":JOIN:REJECTED:Server is full", remote);
            Log($"Rejected '{playerName}' ({remote.Address}) - server is full");
        }
        else
        {
            // Remove any stale entry sharing this endpoint (reconnect case)
            players.RemoveAll(p => p.EndPoint!.ToString() == remote.ToString());

            players.Add(new ConnectedPlayer
            {
                Name     = playerName,
                EndPoint = remote,
                LastSeen = DateTime.UtcNow
            });
            state.PlayerCount = players.Count;

            Send(PROTOCOL_MAGIC + ":JOIN:ACCEPTED", remote);
            Log($"'{playerName}' joined ({remote.Address}) - {players.Count}/{state.MaxPlayers} players");
            didJoin = true;

            // Send an initial STATE packet after a short delay so the client's
            // listen loop has time to start up after receiving the ACCEPTED reply
            var newEndpoint = remote;
            new Thread(() =>
            {
                Thread.Sleep(500);
                string phaseStr = PhaseToString(phase);
                var welcome = new GameStatePacket
                {
                    Phase       = phaseStr,
                    MapName     = state.MapName,
                    GameMode    = state.GameMode,
                    TimeLeft    = intermissionTimeLeft,
                    PlayerNames = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs })
                };
                byte[] stateBytes = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":STATE:" + JsonSerializer.Serialize(welcome));
                try { querySocket.Send(stateBytes, stateBytes.Length, newEndpoint); }
                catch { }
            }) { IsBackground = true }.Start();
        }
    }

    if (didJoin) BroadcastState();
}

void HandleLeave(IPEndPoint remote)
{
    bool didLeave = false;
    lock (playersLock)
    {
        var leaving = players.Find(p => p.EndPoint!.ToString() == remote.ToString());
        if (leaving != null)
        {
            players.Remove(leaving);
            state.PlayerCount = players.Count;
            Log($"'{leaving.Name}' left ({remote.Address}) - {players.Count}/{state.MaxPlayers} players");
            didLeave = true;
        }
    }
    if (didLeave) BroadcastState();
}

void HandleHeartbeat(IPEndPoint remote)
{
    lock (playersLock)
    {
        var player = FindPlayer(remote);
        if (player == null) return;

        // Use the time since the last heartbeat as a rough latency estimate
        player.LastPingMs = (int)(DateTime.UtcNow - player.LastSeen).TotalMilliseconds;
        player.LastSeen   = DateTime.UtcNow;
    }
}

void HandlePong(IPEndPoint remote)
{
    lock (playersLock)
    {
        var player = FindPlayer(remote);
        if (player != null)
            player.LastPingMs = (int)(DateTime.UtcNow - player.LastPingSent).TotalMilliseconds;
    }
}

void HandleReady(IPEndPoint remote)
{
    List<PlayerNameEntry>? snapshot = null;
    lock (playersLock)
    {
        var player = FindPlayer(remote);
        if (player != null)
        {
            player.IsReady = true;
            Log($"'{player.Name}' is ready");
            snapshot = players.ConvertAll(p => new PlayerNameEntry { name = p.Name, ping = p.LastPingMs });
        }
    }
    if (snapshot != null) BroadcastStateWithSnapshot(snapshot);
}

void HandleVote(string message, IPEndPoint remote)
{
    string prefix   = PROTOCOL_MAGIC + ":VOTE:";
    string votedMap = message.Length > prefix.Length ? message[prefix.Length..].Trim() : "";

    if (!config.MapPool.Contains(votedMap)) return;

    lock (votesLock)
    {
        var player = FindPlayer(remote);
        if (player == null) return;

        // Each player gets one vote - remove their previous before applying the new one
        if (player.CurrentVote != null && votes.ContainsKey(player.CurrentVote))
            votes[player.CurrentVote]--;

        player.CurrentVote = votedMap;
        if (!votes.ContainsKey(votedMap)) votes[votedMap] = 0;
        votes[votedMap]++;

        Log($"'{player.Name}' voted for '{votedMap}'");
        BroadcastToAll(PROTOCOL_MAGIC + ":MAPVOTE:" + JsonSerializer.Serialize(votes));
    }
}

void HandleSpectate(IPEndPoint remote)
{
    lock (playersLock)
    {
        var player = FindPlayer(remote);
        if (player != null)
        {
            player.IsSpectator = true;
            Log($"'{player.Name}' is now spectating");
        }
    }
}

void HandleChat(string message, IPEndPoint remote)
{
    string prefix = PROTOCOL_MAGIC + ":CHAT:";
    string text   = message.Length > prefix.Length ? message[prefix.Length..].Trim() : "";
    if (string.IsNullOrEmpty(text)) return;

    // Clamp message length so nobody can flood the log with a 10MB string
    if (text.Length > 200) text = text[..200];

    string senderName;
    lock (playersLock)
    {
        var player = FindPlayer(remote);
        if (player == null) return;   // Ignore chat from unknown endpoints
        senderName = player.Name;
    }

    Log($"[CHAT] {senderName}: {text}");
    BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:" + senderName + ":" + text);
}

void HandleSpawnRequest(string message, IPEndPoint remote)
{
    string json = message[(PROTOCOL_MAGIC + ":SPAWNREQ:").Length..];
    var req = JsonSerializer.Deserialize<SpawnRequestPacket>(json);
    if (req == null) return;

    var broadcast = new SpawnBroadcastPacket
    {
        NetworkObjectId = Guid.NewGuid().ToString(),
        PrefabKey = req.PrefabKey,
        RequestId = req.RequestId,
        PX = req.PX, PY = req.PY, PZ = req.PZ,
        RX = req.RX, RY = req.RY, RZ = req.RZ,
    };
    BroadcastToAll(PROTOCOL_MAGIC + ":SPAWN:" + JsonSerializer.Serialize(broadcast));
}

void HandleDespawn(string message, IPEndPoint remote)
{
    string body = message[(PROTOCOL_MAGIC + ":DESPAWNREQ:").Length..];
    BroadcastToAll(PROTOCOL_MAGIC + ":DESPAWN:" + body);
}


// Helper - find a player by endpoint without needing to repeat the predicate everywhere
ConnectedPlayer? FindPlayer(IPEndPoint remote)
{
    // NOTE: must already hold playersLock when calling this
    return players.Find(p => p.EndPoint!.ToString() == remote.ToString());
}

// Helper - send a single UTF-8 string packet to one endpoint
void Send(string message, IPEndPoint remote)
{
    byte[] data = Encoding.UTF8.GetBytes(message);
    querySocket.Send(data, data.Length, remote);
}


// =========================================================================================================
// SECTION 7 - BACKGROUND THREADS
// All long-running server tasks. Each thread has exactly one responsibility.
// =========================================================================================================

// --- UDP Listener ---
// Receives all incoming packets and dispatches them to the appropriate handler above.
new Thread(() =>
{
    while (true)
    {
        try
        {
            var    remote  = new IPEndPoint(IPAddress.Any, 0);
            string message = Encoding.UTF8.GetString(querySocket.Receive(ref remote));

            if      (message.StartsWith(PROTOCOL_MAGIC + ":QUERY"))    HandleQuery(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":JOIN:"))    HandleJoin(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":LEAVE"))    HandleLeave(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":HEARTBEAT"))HandleHeartbeat(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":PONG"))     HandlePong(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":READY"))    HandleReady(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":VOTE:"))    HandleVote(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":SPECTATE")) HandleSpectate(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":CHAT:"))    HandleChat(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":SPAWNREQ:"))  HandleSpawnRequest(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":DESPAWNREQ:")) HandleDespawn(message, remote);
        }
        catch (Exception e)
        {
            Log($"[UDP] Error: {e.Message}");
        }
    }
}) { IsBackground = true }.Start();

// --- Heartbeat Timeout ---
// Evicts players who haven't sent a heartbeat in over 15 seconds.
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(5_000);
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

// --- Ping ---
// Sends a PING to each player every 5 seconds so we can measure true round-trip time.
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(5_000);
        lock (playersLock)
        {
            byte[] pingPacket = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":PING");
            foreach (var p in players)
            {
                p.LastPingSent = DateTime.UtcNow;
                try { querySocket.Send(pingPacket, pingPacket.Length, p.EndPoint); }
                catch { }
            }
        }
    }
}) { IsBackground = true }.Start();

// --- Intermission Timer ---
// Counts down during intermission and starts the game when time runs out or everyone is ready.
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(1_000);
        lock (phaseLock)
        {
            if (phase != GamePhase.Intermission) continue;

            bool allReady;
            lock (playersLock)
                allReady = players.Count > 0 && players.TrueForAll(p => p.IsReady);

            intermissionTimeLeft--;
            BroadcastState();

            if (intermissionTimeLeft <= 0 || allReady)
                StartGame();
        }
    }
}) { IsBackground = true }.Start();


// =========================================================================================================
// SECTION 8 - CONSOLE COMMAND LOOP
// Blocking loop that reads admin commands from stdin while the server runs.
// =========================================================================================================

Log("Server ready! Type 'help' for a list of commands.");

while (true)
{
    Console.Write("==> ");
    string? raw = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(raw)) continue;

    string[] parts = raw.Split(' ', 2);
    string   cmd   = parts[0].ToLower();
    string   arg   = parts.Length > 1 ? parts[1] : "";

    switch (cmd)
    {
        // ---- Info ----
        case "help":
            Console.WriteLine("  status                       show current server info");
            Console.WriteLine("  players                      list connected players");
            Console.WriteLine("  phase                        show current game phase and time remaining");
            Console.WriteLine("  mappool                      list maps in the rotation");
            Console.WriteLine("  say <text>                   send a chat message as the server");
            Console.WriteLine();
            Console.WriteLine("  setname       <text>         set the server name");
            Console.WriteLine("  setmap        <text>         set the current map name");
            Console.WriteLine("  setgamemode   <text>         set the current game mode");
            Console.WriteLine("  setmaxplayers <n>            set the maximum player count");
            Console.WriteLine("  seticon       <base64>       set the server icon (base64-encoded PNG)");
            Console.WriteLine();
            Console.WriteLine("  kick <index or name>         remove a player from the server");
            Console.WriteLine("  startgame                    force-start the game");
            Console.WriteLine("  endgame                      force-end the game");
            Console.WriteLine("  quit                         shut down the server");
            break;

        case "status":
            Console.WriteLine($"  Name:         {state.ServerName}");
            Console.WriteLine($"  Map:          {state.MapName}");
            Console.WriteLine($"  Game Mode:    {state.GameMode}");
            Console.WriteLine($"  Players:      {state.PlayerCount}/{state.MaxPlayers}");
            Console.WriteLine($"  Icon set:     {(string.IsNullOrEmpty(state.IconBase64) ? "No" : "Yes")}");
            break;

        case "players":
            lock (playersLock)
            {
                if (players.Count == 0) { Console.WriteLine("  No players connected."); break; }
                for (int i = 0; i < players.Count; i++)
                {
                    string flags = "";
                    if (players[i].IsReady)     flags += " [READY]";
                    if (players[i].IsSpectator) flags += " [SPECTATOR]";
                    Console.WriteLine($"  [{i}] {players[i].Name}{flags}  ({players[i].EndPoint})  {players[i].LastPingMs}ms");
                }
            }
            break;

        case "phase":
            lock (phaseLock)
                Console.WriteLine($"  Phase: {phase}   Time left: {intermissionTimeLeft}s");
            break;

        case "mappool":
            Console.WriteLine("  Map pool:");
            for (int i = 0; i < config.MapPool.Count; i++)
                Console.WriteLine($"  [{i}] {config.MapPool[i]}");
            break;
        
        case "say":
            if (string.IsNullOrEmpty(arg)) { Console.WriteLine("  Usage: say <message>"); break; }
            Log($"[CHAT] Server: {arg}");
            BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:Server:" + arg);
            break;

        // ---- Settings ----
        case "setname":
            state.ServerName = arg;
            Log($"Server name set to '{arg}'");
            break;

        case "setmap":
            state.MapName = arg;
            Log($"Current map set to '{arg}'");
            break;

        case "setgamemode":
            state.GameMode = arg;
            Log($"Game mode set to '{arg}'");
            break;

        case "seticon":
            state.IconBase64 = arg;
            Log("Server icon updated");
            break;

        case "setmaxplayers":
            if (int.TryParse(arg, out int maxN))
            {
                state.MaxPlayers = maxN;
                Log($"Max players set to {maxN}");
            }
            else
            {
                Console.WriteLine($"  Could not parse '{arg}' as a number.");
                Console.WriteLine("  Usage: setmaxplayers <number>");
            }
            break;

        // ---- Player Management ----
        case "kick":
            lock (playersLock)
            {
                if (players.Count == 0) { Console.WriteLine("  No players connected."); break; }

                ConnectedPlayer? target = null;

                if (int.TryParse(arg, out int kickIndex))
                {
                    if (kickIndex < 0 || kickIndex >= players.Count)
                    { Console.WriteLine($"  No player at index {kickIndex}."); break; }
                    target = players[kickIndex];
                }
                else
                {
                    target = players.Find(p => p.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
                }

                if (target == null) { Console.WriteLine($"  No player named '{arg}'."); break; }

                Send(PROTOCOL_MAGIC + ":KICKED:You were kicked by the server", target.EndPoint!);
                players.Remove(target);
                state.PlayerCount = players.Count;
                Log($"Kicked '{target.Name}'");
            }
            break;

        // ---- Game Control ----
        case "startgame":
            lock (phaseLock)
            {
                if (phase == GamePhase.InProgress)
                { Console.WriteLine("  Game is already in progress."); break; }
                StartGame();
                Log("Game force-started from console");
            }
            break;

        case "endgame":
            lock (phaseLock)
            {
                if (phase == GamePhase.Intermission)
                { Console.WriteLine("  Already in intermission."); break; }
                EndGame();
                Log("Game force-ended from console");
            }
            break;

        case "quit":
            Log("Shutting down...");
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

        default:
            Console.WriteLine($"  Unknown command '{cmd}'. Type 'help' for a list of available commands.");
            break;
    }
}


// =========================================================================================================
// SECTION 9 - DATA TYPES
// Plain data classes used for serialization and shared server state.
// =========================================================================================================

// --- Configuration (loaded from server.config at startup) ---
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

// --- Live runtime state visible to all threads ---
class ServerState
{
    public string ServerName  { get; set; } = "";
    public string MapName     { get; set; } = "";
    public string GameMode    { get; set; } = "";
    public int    MaxPlayers  { get; set; } = 16;
    public int    PlayerCount { get; set; } = 0;
    public string IconBase64  { get; set; } = "";
}

// --- Payload returned in response to a QUERY packet ---
// Kept separate from ServerState so we can control exactly what the browser sees
class ServerQueryResponse
{
    public string ServerName  { get; set; } = "";
    public string MapName     { get; set; } = "";
    public string GameMode    { get; set; } = "";
    public int    MaxPlayers  { get; set; } = 16;
    public int    PlayerCount { get; set; } = 0;
    public string IconBase64  { get; set; } = "";
}

// --- A player currently connected to the server ---
class ConnectedPlayer
{
    public string      Name        { get; set; } = "Player";
    public IPEndPoint? EndPoint    { get; set; } = null;
    public DateTime    LastSeen    { get; set; } = DateTime.UtcNow;
    public DateTime    LastPingSent{ get; set; } = DateTime.UtcNow;
    public int         LastPingMs  { get; set; } = 0;
    public bool        IsReady     { get; set; } = false;
    public bool        IsSpectator { get; set; } = false;
    public string?     CurrentVote { get; set; } = null;
}

// --- Compact player entry sent inside STATE packets ---
class PlayerNameEntry
{
    public string name { get; set; } = "";
    public int    ping { get; set; } = 0;
}

// --- Full game state snapshot broadcast to all clients ---
class GameStatePacket
{
    public string                Phase       { get; set; } = "Intermission";
    public string                MapName     { get; set; } = "";
    public string                GameMode    { get; set; } = "";
    public int                   TimeLeft    { get; set; } = 30;
    public List<PlayerNameEntry> PlayerNames { get; set; } = new();
}

// --- The three phases the server can be in ---
enum GamePhase { Intermission, Loading, InProgress }

class SpawnRequestPacket
{
    public string PrefabKey { get; set; } = "";
    public string RequestId { get; set; } = "";
    public float PX { get; set; } public float PY { get; set; } public float PZ { get; set; }
    public float RX { get; set; } public float RY { get; set; } public float RZ { get; set; }
}

class SpawnBroadcastPacket
{
    public string NetworkObjectId { get; set; } = "";
    public string PrefabKey { get; set; } = "";
    public string RequestId { get; set; } = "";
    public float PX { get; set; } public float PY { get; set; } public float PZ { get; set; }
    public float RX { get; set; } public float RY { get; set; } public float RZ { get; set; }
}