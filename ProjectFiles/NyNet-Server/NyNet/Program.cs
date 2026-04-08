// =========================================================================================================
// NyNet - Dedicated Game Server
// =========================================================================================================
// Handles all engine-level logic: player connections, session management, admin system, and packet relay.
// Communicates with clients over UDP using the NyNet protocol.
// Game-specific logic is handled by the IGameRules implementation (e.g. FeralKartGameRules).
//
// Protocol packet format: NyNet:<COMMAND>[:<JSON_PAYLOAD>]
//
// Incoming (client -> server):
//   NyNet:QUERY                  - Server browser ping, no auth required
//   NyNet:JOIN:<n>:<token>       - Request to join the session, token is empty on first join
//   NyNet:LEAVE                  - Graceful disconnect
//   NyNet:HEARTBEAT              - Keep-alive, sent every 5s
//   NyNet:PONG                   - Response to a server PING, used to measure RTT
//   NyNet:READY:<choice>         - Player signals ready, with their game-specific choice payload
//   NyNet:CHAT:<text>            - Player sends a chat message (name is resolved server-side)
//   NyNet:SPAWNREQ:<json>        - Player requests the server spawn a networked object
//   NyNet:DESPAWNREQ:<json>      - Player requests the server despawn a networked object
//   NyNet:TSYNC:<json>           - Player sends a transform sync packet
//   NyNet:VARSYNC:<json>         - Player sends a variable sync packet
//   NyNet:VOTEKICKREQ:<json>     - Player requests a vote kick against another player
//   NyNet:VOTEKICKCAST:<json>    - Player casts their vote in an active vote kick
//
// Outgoing (server -> client):
//   NyNet:RESPONSE:<json>        - Reply to a QUERY
//   NyNet:JOIN:ACCEPTED:<token>  - Join approved, token is the session token for this player
//   NyNet:JOIN:REJECTED:<reason> - Join denied, reason appended
//   NyNet:PING                   - Server-initiated RTT probe
//   NyNet:KICKED:<reason>        - Player was removed from the session
//   NyNet:CHAT:<n>:<text>        - Chat message broadcast to all clients
//   NyNet:SPAWN:<json>           - Tell all clients to spawn a networked object
//   NyNet:DESPAWN:<json>         - Tell all clients to despawn a networked object
//   NyNet:OPPED                  - Tell a client they have been granted OP status
//   NyNet:DEOPPED                - Tell a client their OP status has been revoked
//   NyNet:VOTEKICK:<json>        - Tell all clients a vote kick has started
//   NyNet:VOTEKICKRESULT:<json>  - Tell all clients the result of a vote kick
//
// Game-specific packets (e.g. FeKa:STATE, FeKa:LOADMAP) are sent by the IGameRules implementation.
// =========================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;


// =========================================================================================================
// SECTION 1 - CONFIGURATION
// Load or create config files, then apply any command-line overrides.
// =========================================================================================================

const string NYNET_CONFIG_PATH = "server.config";
const string FEKA_CONFIG_PATH = "feka.config";
const string OPLIST_PATH = "oplist.json";
const string BANLIST_PATH = "banlist.json";

// Create a default NyNet config if one does not exist yet
if (!File.Exists(NYNET_CONFIG_PATH))
{
    var defaultNyNetConfig = new ServerConfig
    {
        ProtocolMagic = "NyNet",
        ServerName = "NyNet Server",
        Port = 27015,
        MaxPlayers = 16,
        IconPath = "",
        AllowChatCommands = true,
        VoteKickDuration = 30f,
        VoteKickThreshold = 0.6f,
        VoteKickRepeatDelay = 120f,
        KickRejoinCooldown = 120f,
        AnnounceJoin = true,
        AnnounceLeave = true,
        AnnounceKick = true,
        AnnounceBan = true
    };
    File.WriteAllText(NYNET_CONFIG_PATH, JsonSerializer.Serialize(defaultNyNetConfig, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Created default NyNet config at '{NYNET_CONFIG_PATH}' - edit it then restart the server.");
}

// Create a default FeralKart config if one does not exist yet
if (!File.Exists(FEKA_CONFIG_PATH))
{
    var defaultFeKaConfig = new FeKa_ServerConfig
    {
        GameMode = "Race",
        IntermissionDuration = 30,
        MapPool = new List<string>
        {
            "midwen_midnightcity",
            "da_howelfen",
            "auho_forrest",
            "wico_cabin",
            "corgeo_facility",
            "fo_mechanyon"
        }
    };
    File.WriteAllText(FEKA_CONFIG_PATH, JsonSerializer.Serialize(defaultFeKaConfig, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Created default FeralKart config at '{FEKA_CONFIG_PATH}' - edit it then restart the server.");
}

var config = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(NYNET_CONFIG_PATH)) ?? new ServerConfig();
var fekaConfig = JsonSerializer.Deserialize<FeKa_ServerConfig>(File.ReadAllText(FEKA_CONFIG_PATH)) ?? new FeKa_ServerConfig();

string PROTOCOL_MAGIC = config.ProtocolMagic;

// Load the OP list, or create an empty one if it does not exist
var opList = File.Exists(OPLIST_PATH)
    ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(OPLIST_PATH)) ?? new List<string>()
    : new List<string>();

// Load the ban list, or create an empty one if it does not exist
var banList = File.Exists(BANLIST_PATH)
    ? JsonSerializer.Deserialize<List<BanEntry>>(File.ReadAllText(BANLIST_PATH)) ?? new List<BanEntry>()
    : new List<BanEntry>();

void SaveOpList() => File.WriteAllText(OPLIST_PATH, JsonSerializer.Serialize(opList, new JsonSerializerOptions { WriteIndented = true }));
void SaveBanList() => File.WriteAllText(BANLIST_PATH, JsonSerializer.Serialize(banList, new JsonSerializerOptions { WriteIndented = true }));

int queryPort = config.Port;

// Load server icon, if one is configured
string iconBase64 = "";
if (!string.IsNullOrEmpty(config.IconPath) && File.Exists(config.IconPath))
{
    iconBase64 = Convert.ToBase64String(File.ReadAllBytes(config.IconPath));
    Console.WriteLine($"Loaded icon from '{config.IconPath}'");
}

// Command-line argument overrides (--port <n> and --icon <path>)
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
    ServerName = config.ServerName,
    MaxPlayers = config.MaxPlayers,
    PlayerCount = 0,
    IconBase64 = iconBase64
};

// Connected players, guarded by playersLock
var players = new List<ConnectedPlayer>();
var playersLock = new object();

// Live networked object spawns, guarded by liveSpawnsLock
var liveSpawns = new List<SpawnBroadcastPacket>();
var liveSpawnsLock = new object();

// Vote kick state, guarded by activeVoteLock
ActiveVoteKick? activeVote = null;
var activeVoteLock = new object();
var voteKickCooldowns = new Dictionary<string, DateTime>();
var voteKickCooldownsLock = new object();

// Tracks when a kicked player is allowed to rejoin, keyed by session token, guarded by kickCooldownsLock
var kickCooldowns = new Dictionary<string, DateTime>();
var kickCooldownsLock = new object();

// Single UDP socket shared by all threads
var querySocket = new UdpClient(new IPEndPoint(IPAddress.Any, queryPort));
Log($"UDP listener started on port {queryPort}");

// Instantiate the engine wrapper and game rules
// To run a different game on this server, swap out FeralKartGameRules for another IGameRules implementation
var engine = new NyNetEngine(querySocket, players, playersLock, liveSpawns, liveSpawnsLock, state, PROTOCOL_MAGIC);
var gameRules = new FeralKartGameRules(engine, fekaConfig);
engine.GameRules = gameRules;


// =========================================================================================================
// SECTION 3 - LOGGING
// =========================================================================================================

void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");


// =========================================================================================================
// SECTION 4 - BROADCAST HELPERS
// Convenience methods for pushing packets out to connected clients.
// =========================================================================================================

// Sends a raw UTF-8 string packet to every connected player.
void BroadcastToAll(string message)
{
    byte[] data = Encoding.UTF8.GetBytes(message);
    lock (playersLock)
    {
        foreach (var player in players)
        {
            if (player.EndPoint == null) continue;
            try { querySocket.Send(data, data.Length, player.EndPoint); }
            catch { /* Player will time out naturally */ }
        }
    }
}

// Asks GameRules to broadcast the current game state to all clients.
void BroadcastState()
{
    List<ConnectedPlayer> snapshot;
    lock (playersLock)
        snapshot = new List<ConnectedPlayer>(players);
    gameRules.BroadcastGameState(snapshot);
}


// =========================================================================================================
// SECTION 5 - MESSAGE HANDLERS
// One method per incoming packet type. Called from the UDP listener thread.
// Each handler receives the full raw message string and the sender's endpoint.
// =========================================================================================================

void HandleQuery(IPEndPoint remote)
{
    var browserInfo = gameRules.GetServerBrowserInfo();
    var payload = new ServerQueryResponse
    {
        ServerName = state.ServerName,
        MapName = browserInfo.MapName,
        GameMode = browserInfo.GameMode,
        MaxPlayers = state.MaxPlayers,
        PlayerCount = state.PlayerCount,
        IconBase64 = state.IconBase64
    };

    byte[] reply = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":RESPONSE:" + JsonSerializer.Serialize(payload));
    querySocket.Send(reply, reply.Length, remote);
    Log($"Query answered: {remote.Address}");
}

void HandleJoin(string message, IPEndPoint remote)
{
    string prefix = PROTOCOL_MAGIC + ":JOIN:";
    string body = message.Length > prefix.Length ? message[prefix.Length..].Trim() : "";

    // The body is either "PlayerName" on first join, or "PlayerName:previousToken" on reconnect
    string playerName = body;
    string priorToken = "";
    int separatorIndex = body.IndexOf(':');
    if (separatorIndex >= 0)
    {
        playerName = body[..separatorIndex];
        priorToken = body[(separatorIndex + 1)..];
    }

    if (string.IsNullOrEmpty(playerName))
        playerName = "NetPlayer";

    // Reject banned players before doing anything else
    // Players with no prior token (first join) pass through automatically
    // TODO: Potentially exploitable if a player deletes their token to bypass a ban ~Liz
    if (!string.IsNullOrEmpty(priorToken) && banList.Any(b => b.Token == priorToken))
    {
        var ban = banList.First(b => b.Token == priorToken);
        Send(PROTOCOL_MAGIC + ":JOIN:REJECTED:You are banned. Reason: " + ban.Reason, remote);
        Log($"Rejected banned player '{playerName}' ({remote.Address})");
        return;
    }

    // Check whether this player is still within their kick rejoin cooldown
    if (!string.IsNullOrEmpty(priorToken))
    {
        lock (kickCooldownsLock)
        {
            if (kickCooldowns.TryGetValue(priorToken, out DateTime kickTime))
            {
                double secondsRemaining = config.KickRejoinCooldown - (DateTime.UtcNow - kickTime).TotalSeconds;
                if (secondsRemaining > 0)
                {
                    Send(PROTOCOL_MAGIC + ":JOIN:REJECTED:You were recently kicked. Please wait " + (int)Math.Ceiling(secondsRemaining) + " second(s) before rejoining.", remote);
                    Log($"Rejected '{playerName}' ({remote.Address}) - kick cooldown, {(int)Math.Ceiling(secondsRemaining)}s remaining");
                    return;
                }
                else
                {
                    // Cooldown has expired, clean up the entry
                    kickCooldowns.Remove(priorToken);
                }
            }
        }
    }

    bool didJoin = false;
    ConnectedPlayer? newPlayer = null;
    lock (playersLock)
    {
        if (players.Count >= state.MaxPlayers)
        {
            Send(PROTOCOL_MAGIC + ":JOIN:REJECTED:Server is full", remote);
            Log($"Rejected '{playerName}' ({remote.Address}) - server is full");
        }
        else
        {
            // Remove any stale entry sharing this endpoint (handles reconnect case)
            players.RemoveAll(p => p.EndPoint!.ToString() == remote.ToString());

            newPlayer = new ConnectedPlayer
            {
                Name = playerName,
                EndPoint = remote,
                LastSeen = DateTime.UtcNow,
                // Reuse the player's prior token if they have one so their OP status persists across sessions
                SessionToken = string.IsNullOrEmpty(priorToken) ? Guid.NewGuid().ToString("N") : priorToken
            };

            newPlayer.IsOp = opList.Contains(newPlayer.SessionToken);
            players.Add(newPlayer);
            state.PlayerCount = players.Count;

            // Include the session token in the accepted reply so the client can store it for future joins
            Send(PROTOCOL_MAGIC + ":JOIN:ACCEPTED:" + newPlayer.SessionToken, remote);
            Log($"'{playerName}' joined ({remote.Address}) - {players.Count}/{state.MaxPlayers} players");
            didJoin = true;

            if (config.AnnounceJoin)
                BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:Server:" + playerName + " joined the game.");

            // Send the initial game state and any existing spawns after a short delay,
            // giving the client's listen loop time to start up after receiving the ACCEPTED reply
            var joinedEndpoint = remote;
            var currentPlayers = new List<ConnectedPlayer>(players);
            new Thread(() =>
            {
                Thread.Sleep(500);

                // Ask GameRules to send the welcome state to this specific client
                gameRules.SendWelcomeState(joinedEndpoint, currentPlayers);

                // Replay all currently live spawns so the new player sees existing networked objects
                lock (liveSpawnsLock)
                {
                    foreach (var spawn in liveSpawns)
                    {
                        if (spawn.OwnerEndpoint == joinedEndpoint.ToString()) continue;

                        var replaySpawn = new SpawnBroadcastPacket
                        {
                            NetworkObjectId = spawn.NetworkObjectId,
                            PrefabKey = spawn.PrefabKey,
                            RequestId = "",
                            PX = spawn.PX, PY = spawn.PY, PZ = spawn.PZ,
                            RX = spawn.RX, RY = spawn.RY, RZ = spawn.RZ
                        };

                        byte[] spawnBytes = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":SPAWN:" + JsonSerializer.Serialize(replaySpawn));
                        try { querySocket.Send(spawnBytes, spawnBytes.Length, joinedEndpoint); }
                        catch { }
                    }
                }
            }) { IsBackground = true }.Start();
        }
    }

    if (didJoin && newPlayer != null)
    {
        gameRules.OnPlayerJoined(newPlayer);
        BroadcastState();
    }
}

void HandleLeave(IPEndPoint remote)
{
    bool didLeave = false;
    ConnectedPlayer? leavingPlayer = null;

    lock (playersLock)
    {
        leavingPlayer = players.Find(p => p.EndPoint!.ToString() == remote.ToString());
        if (leavingPlayer != null)
        {
            players.Remove(leavingPlayer);
            state.PlayerCount = players.Count;
            Log($"'{leavingPlayer.Name}' left ({remote.Address}) - {players.Count}/{state.MaxPlayers} players");

            if (config.AnnounceLeave)
                BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:Server:" + leavingPlayer.Name + " left the game.");

            didLeave = true;
        }
    }

    if (didLeave && leavingPlayer != null)
    {
        gameRules.OnPlayerLeft(leavingPlayer);
        BroadcastState();
    }
}

void HandleHeartbeat(IPEndPoint remote)
{
    lock (playersLock)
    {
        var player = FindPlayer(remote);
        if (player == null) return;

        // Use the time since the last heartbeat as a rough latency estimate
        player.LastPingMs = (int)(DateTime.UtcNow - player.LastSeen).TotalMilliseconds;
        player.LastSeen = DateTime.UtcNow;
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

void HandleReady(string message, IPEndPoint remote)
{
    // The READY packet carries the player's game-specific choice as a payload
    // e.g. NyNet:READY:kart_liz  or  NyNet:READY:spectate
    string prefix = PROTOCOL_MAGIC + ":READY:";
    string choice = message.Length > prefix.Length ? message[prefix.Length..].Trim() : "spectate";

    ConnectedPlayer? player;
    lock (playersLock)
    {
        player = FindPlayer(remote);
    }

    if (player == null) return;

    Log($"'{player.Name}' is ready (choice: {choice})");
    gameRules.OnPlayerReady(player, choice);
}

void HandleChat(string message, IPEndPoint remote)
{
    string prefix = PROTOCOL_MAGIC + ":CHAT:";
    string text = message.Length > prefix.Length ? message[prefix.Length..].Trim() : "";
    if (string.IsNullOrEmpty(text)) return;
    if (text.Length > 200) text = text[..200];

    ConnectedPlayer? sender;
    lock (playersLock)
    {
        sender = FindPlayer(remote);
        if (sender == null) return;
    }

    // If the message starts with / and the sender is an OP, treat it as a server command
    // The message is not echoed to other clients
    if (config.AllowChatCommands && text.StartsWith("/") && sender.IsOp)
    {
        string rawCommand = text[1..].Trim();
        string[] commandParts = rawCommand.Split(' ', 2);
        string commandName = commandParts[0].ToLower();
        string commandArg = commandParts.Length > 1 ? commandParts[1] : "";

        Log($"[CHATCMD] OP '{sender.Name}' ran: /{rawCommand}");
        ExecuteCommand(commandName, commandArg, sender);
        return;
    }

    Log($"[CHAT] {sender.Name}: {text}");
    BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:" + sender.Name + ":" + text);
}

void HandleSpawnRequest(string message, IPEndPoint remote)
{
    string json = message[(PROTOCOL_MAGIC + ":SPAWNREQ:").Length..];
    var request = JsonSerializer.Deserialize<SpawnRequestPacket>(json);
    if (request == null) return;

    var broadcast = new SpawnBroadcastPacket
    {
        NetworkObjectId = Guid.NewGuid().ToString(),
        PrefabKey = request.PrefabKey,
        RequestId = request.RequestId,
        OwnerEndpoint = remote.ToString(),
        PX = request.PX, PY = request.PY, PZ = request.PZ,
        RX = request.RX, RY = request.RY, RZ = request.RZ
    };

    lock (liveSpawnsLock) { liveSpawns.Add(broadcast); }
    BroadcastToAll(PROTOCOL_MAGIC + ":SPAWN:" + JsonSerializer.Serialize(broadcast));
    gameRules.OnObjectSpawned(broadcast);
}

void HandleDespawn(string message, IPEndPoint remote)
{
    string body = message[(PROTOCOL_MAGIC + ":DESPAWNREQ:").Length..];
    var despawnPacket = JsonSerializer.Deserialize<DespawnPacket>(body);

    if (despawnPacket != null)
    {
        lock (liveSpawnsLock)
            liveSpawns.RemoveAll(s => s.NetworkObjectId == despawnPacket.NetworkObjectId);
    }

    BroadcastToAll(PROTOCOL_MAGIC + ":DESPAWN:" + body);
}

void HandleTransformSync(string message, IPEndPoint remote)
{
    // Relay the packet to every client except the sender
    byte[] data = Encoding.UTF8.GetBytes(message);
    lock (playersLock)
    {
        foreach (var player in players)
        {
            if (player.EndPoint == null) continue;
            if (player.EndPoint.ToString() == remote.ToString()) continue;
            try { querySocket.Send(data, data.Length, player.EndPoint); }
            catch { }
        }
    }
}

void HandleVarSync(string message, IPEndPoint remote)
{
    // Relay the packet to every client except the sender
    byte[] data = Encoding.UTF8.GetBytes(message);
    lock (playersLock)
    {
        foreach (var player in players)
        {
            if (player.EndPoint == null) continue;
            if (player.EndPoint.ToString() == remote.ToString()) continue;
            try { querySocket.Send(data, data.Length, player.EndPoint); }
            catch { }
        }
    }
}

void HandleVoteKickRequest(string message, IPEndPoint remote)
{
    string json = message[(PROTOCOL_MAGIC + ":VOTEKICKREQ:").Length..];
    var request = JsonSerializer.Deserialize<VoteKickRequest>(json);
    if (request == null) return;

    ConnectedPlayer? initiator;
    ConnectedPlayer? target;
    lock (playersLock)
    {
        initiator = FindPlayer(remote);
        target = players.Find(p => p.Name == request.TargetName);
    }

    if (initiator == null || target == null) return;

    if (target.IsOp)
    {
        Send(PROTOCOL_MAGIC + ":CHAT:Server:You cannot vote-kick an OP.", remote);
        return;
    }

    // Check whether this initiator has already tried to kick this target recently
    string cooldownKey = initiator.SessionToken + ":" + target.SessionToken;
    lock (voteKickCooldownsLock)
    {
        if (voteKickCooldowns.TryGetValue(cooldownKey, out DateTime lastVoteTime))
        {
            double secondsRemaining = config.VoteKickRepeatDelay - (DateTime.UtcNow - lastVoteTime).TotalSeconds;
            if (secondsRemaining > 0)
            {
                Send(PROTOCOL_MAGIC + ":CHAT:Server:You must wait " + (int)secondsRemaining + "s before vote-kicking that player again.", remote);
                return;
            }
        }
        voteKickCooldowns[cooldownKey] = DateTime.UtcNow;
    }

    // Only one vote kick can be active at a time
    lock (activeVoteLock)
    {
        if (activeVote != null)
        {
            Send(PROTOCOL_MAGIC + ":CHAT:Server:A vote kick is already in progress.", remote);
            return;
        }

        activeVote = new ActiveVoteKick
        {
            TargetToken = target.SessionToken,
            TargetName = target.Name,
            Deadline = DateTime.UtcNow.AddSeconds(config.VoteKickDuration)
        };
    }

    Log($"Vote kick started: '{initiator.Name}' wants to kick '{target.Name}'");

    var broadcastPacket = new VoteKickPacket
    {
        TargetName = target.Name,
        TimeSeconds = (int)config.VoteKickDuration
    };

    BroadcastToAll(PROTOCOL_MAGIC + ":VOTEKICK:" + JsonSerializer.Serialize(broadcastPacket));

    // Resolve the vote after the time limit expires
    new Thread(() =>
    {
        Thread.Sleep((int)(config.VoteKickDuration * 1000));
        ResolveVoteKick();
    }) { IsBackground = true }.Start();
}

void HandleVoteKickCast(string message, IPEndPoint remote)
{
    string json = message[(PROTOCOL_MAGIC + ":VOTEKICKCAST:").Length..];
    var cast = JsonSerializer.Deserialize<VoteKickCast>(json);
    if (cast == null) return;

    ConnectedPlayer? voter;
    lock (playersLock) { voter = FindPlayer(remote); }
    if (voter == null) return;

    lock (activeVoteLock)
    {
        if (activeVote == null) return;

        // Remove any previous vote from this player before applying the new one
        activeVote.VotedYes.Remove(voter.SessionToken);
        activeVote.VotedNo.Remove(voter.SessionToken);

        if (cast.VotedYes)
            activeVote.VotedYes.Add(voter.SessionToken);
        else
            activeVote.VotedNo.Add(voter.SessionToken);

        Log($"'{voter.Name}' voted {(cast.VotedYes ? "YES" : "NO")} on kick of '{activeVote.TargetName}' ({activeVote.VotedYes.Count} yes / {activeVote.VotedNo.Count} no)");
    }
}

void ResolveVoteKick()
{
    ActiveVoteKick? vote;
    lock (activeVoteLock)
    {
        vote = activeVote;
        activeVote = null;
    }

    if (vote == null) return;

    int playerCount;
    lock (playersLock) { playerCount = players.Count; }

    // The vote passes if enough of the current players voted yes
    bool passed = playerCount > 0 && vote.VotedYes.Count >= Math.Ceiling(playerCount * config.VoteKickThreshold);
    Log($"Vote kick on '{vote.TargetName}' resolved: {(passed ? "PASSED" : "FAILED")} ({vote.VotedYes.Count}/{playerCount} yes)");

    var result = new VoteKickResult { TargetName = vote.TargetName, Passed = passed };
    BroadcastToAll(PROTOCOL_MAGIC + ":VOTEKICKRESULT:" + JsonSerializer.Serialize(result));

    if (passed)
    {
        ConnectedPlayer? kickedPlayer = null;
        lock (playersLock)
        {
            kickedPlayer = players.Find(p => p.SessionToken == vote.TargetToken);
            if (kickedPlayer != null)
            {
                Send(PROTOCOL_MAGIC + ":KICKED:You were vote-kicked.", kickedPlayer.EndPoint!);
                RecordKickCooldown(kickedPlayer.SessionToken);
                players.Remove(kickedPlayer);
                state.PlayerCount = players.Count;
                Log($"Vote-kicked '{kickedPlayer.Name}'");

                if (config.AnnounceKick)
                    BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:Server:" + kickedPlayer.Name + " was vote-kicked.");
            }
        }

        if (kickedPlayer != null)
        {
            gameRules.OnPlayerLeft(kickedPlayer);
            BroadcastState();
        }
    }
}


// =========================================================================================================
// SECTION 6 - CONSOLE COMMANDS
// Callable from both the server console and in-game chat (for OPs).
// When source is null the command was run from the console.
// When source is a ConnectedPlayer the command was run via in-game chat.
// =========================================================================================================

void ExecuteCommand(string cmd, string arg, ConnectedPlayer? source = null)
{
    switch (cmd)
    {
        case "help":
        {
            if (source != null)
            {
                Send(PROTOCOL_MAGIC + ":CHAT:Server:Commands: /say, /kick, /status, /players, /op, /deop, /ban, /unban, /banlist", source.EndPoint!);
                break;
            }
            Console.WriteLine("  status                        show current server info");
            Console.WriteLine("  players                       list connected players");
            Console.WriteLine("  say <text>                    send a chat message as the server");
            Console.WriteLine("  setname <text>                set the server name");
            Console.WriteLine("  setmaxplayers <n>             set the maximum player count");
            Console.WriteLine("  seticon <base64>              set the server icon (base64-encoded PNG)");
            Console.WriteLine("  kick <index or name>          remove a player from the server");
            Console.WriteLine("  op <index or name>            grant a player OP status");
            Console.WriteLine("  deop <index or name>          revoke a player's OP status");
            Console.WriteLine("  ban <index or name> [reason]  ban a player");
            Console.WriteLine("  unban <token or name>         unban a player");
            Console.WriteLine("  banlist                       list all bans");
            Console.WriteLine("  quit                          shut down the server");
            break;
        }

        case "status":
        {
            var browserInfo = gameRules.GetServerBrowserInfo();
            if (source != null)
            {
                Send(PROTOCOL_MAGIC + ":CHAT:Server:Name: " + state.ServerName + " | Map: " + browserInfo.MapName + " | Mode: " + browserInfo.GameMode + " | Players: " + state.PlayerCount + "/" + state.MaxPlayers, source.EndPoint!);
                break;
            }
            Console.WriteLine($"  Name:      {state.ServerName}");
            Console.WriteLine($"  Map:       {browserInfo.MapName}");
            Console.WriteLine($"  Game Mode: {browserInfo.GameMode}");
            Console.WriteLine($"  Players:   {state.PlayerCount}/{state.MaxPlayers}");
            Console.WriteLine($"  Icon set:  {(string.IsNullOrEmpty(state.IconBase64) ? "No" : "Yes")}");
            break;
        }

        case "players":
        {
            if (source != null)
            {
                lock (playersLock)
                {
                    string playerList = players.Count == 0
                        ? "No players connected."
                        : string.Join(", ", players.ConvertAll(p => p.Name + (p.IsOp ? " [OP]" : "")));
                    Send(PROTOCOL_MAGIC + ":CHAT:Server:" + playerList, source.EndPoint!);
                }
                break;
            }
            lock (playersLock)
            {
                if (players.Count == 0) { Console.WriteLine("  No players connected."); break; }
                for (int i = 0; i < players.Count; i++)
                {
                    string flags = players[i].IsOp ? " [OP]" : "";
                    Console.WriteLine($"  [{i}] {players[i].Name}{flags}  ({players[i].EndPoint})  {players[i].LastPingMs}ms");
                }
            }
            break;
        }

        case "say":
        {
            if (string.IsNullOrEmpty(arg))
            {
                if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:Usage: /say <message>", source.EndPoint!);
                else Console.WriteLine("  Usage: say <message>");
                break;
            }
            Log($"[CHAT] Server: {arg}");
            BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:Server:" + arg);
            break;
        }

        case "setname":
        {
            if (source != null) { Send(PROTOCOL_MAGIC + ":CHAT:Server:This command is console-only.", source.EndPoint!); break; }
            state.ServerName = arg;
            Log($"Server name set to '{arg}'");
            break;
        }

        case "setmaxplayers":
        {
            if (source != null) { Send(PROTOCOL_MAGIC + ":CHAT:Server:This command is console-only.", source.EndPoint!); break; }
            if (int.TryParse(arg, out int maxPlayers))
            {
                state.MaxPlayers = maxPlayers;
                Log($"Max players set to {maxPlayers}");
            }
            else
            {
                Console.WriteLine($"  Could not parse '{arg}' as a number.");
                Console.WriteLine("  Usage: setmaxplayers <number>");
            }
            break;
        }

        case "seticon":
        {
            if (source != null) { Send(PROTOCOL_MAGIC + ":CHAT:Server:This command is console-only.", source.EndPoint!); break; }
            state.IconBase64 = arg;
            Log("Server icon updated");
            break;
        }

        case "kick":
        {
            lock (playersLock)
            {
                if (players.Count == 0)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No players connected.", source.EndPoint!);
                    else Console.WriteLine("  No players connected.");
                    break;
                }

                ConnectedPlayer? target = null;
                if (int.TryParse(arg, out int kickIndex))
                {
                    if (kickIndex >= 0 && kickIndex < players.Count)
                        target = players[kickIndex];
                }
                else
                {
                    target = players.Find(p => p.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
                }

                if (source != null && target?.IsOp == true)
                {
                    Send(PROTOCOL_MAGIC + ":CHAT:Server:You cannot kick an OP.", source.EndPoint!);
                    break;
                }

                if (target == null)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No player found: " + arg, source.EndPoint!);
                    else Console.WriteLine($"  No player named '{arg}'.");
                    break;
                }

                Send(PROTOCOL_MAGIC + ":KICKED:You were kicked by " + (source?.Name ?? "the server"), target.EndPoint!);
                RecordKickCooldown(target.SessionToken);
                players.Remove(target);
                state.PlayerCount = players.Count;
                gameRules.OnPlayerLeft(target);
                Log($"Kicked '{target.Name}' (by {(source?.Name ?? "console")})");

                if (config.AnnounceKick)
                    BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:Server:" + target.Name + " was kicked.");
            }
            BroadcastState();
            break;
        }

        case "op":
        {
            lock (playersLock)
            {
                if (players.Count == 0)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No players connected.", source.EndPoint!);
                    else Console.WriteLine("  No players connected.");
                    break;
                }

                ConnectedPlayer? target = null;
                if (int.TryParse(arg, out int playerIndex))
                {
                    if (playerIndex >= 0 && playerIndex < players.Count)
                        target = players[playerIndex];
                }
                else
                {
                    target = players.Find(p => p.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
                }

                if (target == null)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No player found: " + arg, source.EndPoint!);
                    else Console.WriteLine($"  No player '{arg}'.");
                    break;
                }

                if (!opList.Contains(target.SessionToken))
                {
                    opList.Add(target.SessionToken);
                    SaveOpList();
                }

                target.IsOp = true;
                Send(PROTOCOL_MAGIC + ":OPPED", target.EndPoint!);
                Log($"'{target.Name}' is now OP (by {(source?.Name ?? "console")})");
            }
            break;
        }

        case "deop":
        {
            lock (playersLock)
            {
                if (players.Count == 0)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No players connected.", source.EndPoint!);
                    else Console.WriteLine("  No players connected.");
                    break;
                }

                ConnectedPlayer? target = null;
                if (int.TryParse(arg, out int playerIndex))
                {
                    if (playerIndex >= 0 && playerIndex < players.Count)
                        target = players[playerIndex];
                }
                else
                {
                    target = players.Find(p => p.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
                }

                if (target == null)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No player found: " + arg, source.EndPoint!);
                    else Console.WriteLine($"  No player '{arg}'.");
                    break;
                }

                opList.Remove(target.SessionToken);
                SaveOpList();
                target.IsOp = false;
                Send(PROTOCOL_MAGIC + ":DEOPPED", target.EndPoint!);
                Log($"'{target.Name}' is no longer OP (by {(source?.Name ?? "console")})");
            }
            break;
        }

        case "ban":
        {
            string[] banParts = arg.Split(' ', 2);
            string banTargetName = banParts[0];
            string banReason = banParts.Length > 1 ? banParts[1] : "Banned by admin";

            lock (playersLock)
            {
                if (players.Count == 0)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No players connected.", source.EndPoint!);
                    else Console.WriteLine("  No players connected.");
                    break;
                }

                ConnectedPlayer? target = null;
                if (int.TryParse(banTargetName, out int playerIndex))
                {
                    if (playerIndex >= 0 && playerIndex < players.Count)
                        target = players[playerIndex];
                }
                else
                {
                    target = players.Find(p => p.Name.Equals(banTargetName, StringComparison.OrdinalIgnoreCase));
                }

                if (source != null && target?.IsOp == true)
                {
                    Send(PROTOCOL_MAGIC + ":CHAT:Server:You cannot ban an OP.", source.EndPoint!);
                    break;
                }

                if (target == null)
                {
                    if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No player found: " + banTargetName, source.EndPoint!);
                    else Console.WriteLine($"  No player '{banTargetName}'.");
                    break;
                }

                if (!banList.Any(b => b.Token == target.SessionToken))
                {
                    banList.Add(new BanEntry { Token = target.SessionToken, Name = target.Name, Reason = banReason });
                    SaveBanList();
                }

                Send(PROTOCOL_MAGIC + ":KICKED:You have been banned. Reason: " + banReason, target.EndPoint!);
                players.Remove(target);
                state.PlayerCount = players.Count;
                gameRules.OnPlayerLeft(target);
                Log($"Banned '{target.Name}' (by {(source?.Name ?? "console")}) reason: {banReason}");

                if (config.AnnounceBan)
                    BroadcastToAll(PROTOCOL_MAGIC + ":CHAT:Server:" + target.Name + " was banned.");
            }
            BroadcastState();
            break;
        }

        case "unban":
        {
            int removedCount = banList.RemoveAll(b =>
                b.Token == arg ||
                b.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));

            if (removedCount > 0)
            {
                SaveBanList();
                if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:Unbanned '" + arg + "'.", source.EndPoint!);
                else Log($"Unbanned '{arg}'");
            }
            else
            {
                if (source != null) Send(PROTOCOL_MAGIC + ":CHAT:Server:No ban found for '" + arg + "'.", source.EndPoint!);
                else Console.WriteLine($"  No ban found for '{arg}'.");
            }
            break;
        }

        case "banlist":
        {
            if (source != null)
            {
                string banListText = banList.Count == 0
                    ? "Ban list is empty."
                    : string.Join(" | ", banList.ConvertAll(b => b.Name + ": " + b.Reason));
                Send(PROTOCOL_MAGIC + ":CHAT:Server:" + banListText, source.EndPoint!);
                break;
            }
            if (banList.Count == 0) { Console.WriteLine("  Ban list is empty."); break; }
            for (int i = 0; i < banList.Count; i++)
                Console.WriteLine($"  [{i}] {banList[i].Name} - {banList[i].Reason} (token: {banList[i].Token})");
            break;
        }

        case "quit":
        {
            if (source != null) { Send(PROTOCOL_MAGIC + ":CHAT:Server:This command is console-only.", source.EndPoint!); break; }
            Log("Shutting down...");
            lock (playersLock)
            {
                byte[] shutdownPacket = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":KICKED:The server has shut down.");
                foreach (var player in players)
                {
                    if (player.EndPoint == null) continue;
                    try { querySocket.Send(shutdownPacket, shutdownPacket.Length, player.EndPoint); }
                    catch { }
                }
            }
            querySocket.Close();
            Environment.Exit(0);
            break;
        }

        default:
        {
            if (source != null)
                Send(PROTOCOL_MAGIC + ":CHAT:Server:Unknown command '" + cmd + "'. Try /help.", source.EndPoint!);
            else
                Console.WriteLine($"  Unknown command '{cmd}'. Type 'help' for a list of commands.");
            break;
        }
    }
}


// =========================================================================================================
// SECTION 7 - SHARED HELPERS
// Small utility methods used across multiple handlers.
// =========================================================================================================

// Find a connected player by their endpoint address.
// NOTE: The caller must already hold playersLock before calling this.
ConnectedPlayer? FindPlayer(IPEndPoint remote)
{
    return players.Find(p => p.EndPoint!.ToString() == remote.ToString());
}

// Send a single UTF-8 string packet to one specific endpoint.
void Send(string message, IPEndPoint remote)
{
    byte[] data = Encoding.UTF8.GetBytes(message);
    querySocket.Send(data, data.Length, remote);
}

// Records a kick cooldown for the given session token so the player cannot rejoin immediately.
void RecordKickCooldown(string sessionToken)
{
    if (string.IsNullOrEmpty(sessionToken) || config.KickRejoinCooldown <= 0) return;
    lock (kickCooldownsLock)
        kickCooldowns[sessionToken] = DateTime.UtcNow;
}


// =========================================================================================================
// SECTION 8 - BACKGROUND THREADS
// All long-running server tasks. Each thread has exactly one responsibility.
// =========================================================================================================

// UDP Listener
// Receives all incoming packets and dispatches them to the appropriate handler.
new Thread(() =>
{
    while (true)
    {
        try
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            string message = Encoding.UTF8.GetString(querySocket.Receive(ref remote));

            if (message.StartsWith(PROTOCOL_MAGIC + ":QUERY")) HandleQuery(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":JOIN:")) HandleJoin(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":LEAVE")) HandleLeave(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":HEARTBEAT")) HandleHeartbeat(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":PONG")) HandlePong(remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":READY")) HandleReady(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":CHAT:")) HandleChat(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":SPAWNREQ:")) HandleSpawnRequest(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":DESPAWNREQ:")) HandleDespawn(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":TSYNC:")) HandleTransformSync(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":VARSYNC:")) HandleVarSync(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":VOTEKICKREQ:")) HandleVoteKickRequest(message, remote);
            else if (message.StartsWith(PROTOCOL_MAGIC + ":VOTEKICKCAST:")) HandleVoteKickCast(message, remote);
            // Any unrecognised prefix is forwarded to GameRules for game-specific packet handling
            else gameRules.OnUnknownPacket(message, remote);
        }
        catch (Exception e)
        {
            Log($"[UDP] Error: {e.Message}");
        }
    }
}) { IsBackground = true }.Start();

// Heartbeat Timeout
// Evicts players who have not sent a heartbeat in over 15 seconds.
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(5000);

        var timedOutPlayers = new List<ConnectedPlayer>();
        lock (playersLock)
        {
            timedOutPlayers = players.FindAll(p => (DateTime.UtcNow - p.LastSeen).TotalSeconds > 15);
            foreach (var player in timedOutPlayers)
                players.Remove(player);

            if (timedOutPlayers.Count > 0)
            {
                state.PlayerCount = players.Count;
                Log($"Timed out {timedOutPlayers.Count} player(s) - {players.Count}/{state.MaxPlayers} players");
            }
        }

        foreach (var player in timedOutPlayers)
            gameRules.OnPlayerLeft(player);

        if (timedOutPlayers.Count > 0)
            BroadcastState();
    }
}) { IsBackground = true }.Start();

// Ping
// Sends a PING to each player every 5 seconds to measure true round-trip time.
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(5000);
        lock (playersLock)
        {
            byte[] pingPacket = Encoding.UTF8.GetBytes(PROTOCOL_MAGIC + ":PING");
            foreach (var player in players)
            {
                player.LastPingSent = DateTime.UtcNow;
                try { querySocket.Send(pingPacket, pingPacket.Length, player.EndPoint); }
                catch { }
            }
        }
    }
}) { IsBackground = true }.Start();


// =========================================================================================================
// SECTION 9 - CONSOLE COMMAND LOOP
// Blocking loop that reads admin commands from stdin while the server runs.
// =========================================================================================================

Log("Server ready! Type 'help' for a list of commands.");

while (true)
{
    Console.Write("==> ");
    string? raw = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(raw)) continue;

    string[] parts = raw.Split(' ', 2);
    string cmd = parts[0].ToLower();
    string arg = parts.Length > 1 ? parts[1] : "";

    ExecuteCommand(cmd, arg);
}