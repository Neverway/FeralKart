using System.Collections.Generic;
using System.Net;

public interface IGameRules
{
    // Engine callbacks
    // Called by the engine at key moments in the server lifecycle

    /// <summary>Called after a player successfully joins the server.</summary>
    void OnPlayerJoined(ConnectedPlayer player);

    /// <summary>Called after a player leaves, times out, or is kicked or banned.</summary>
    void OnPlayerLeft(ConnectedPlayer player);

    /// <summary>Called when a player sends a READY packet with their game-specific choice payload.</summary>
    void OnPlayerReady(ConnectedPlayer player, string choice);

    /// <summary>Called when all connected players have readied up.</summary>
    void OnAllPlayersReady();

    /// <summary>Called after the server confirms and broadcasts a networked object spawn.</summary>
    void OnObjectSpawned(SpawnBroadcastPacket spawnedObject);

    /// <summary>
    /// Called when the UDP listener receives a packet with an unrecognised protocol prefix.
    /// Use this to handle game-specific incoming packets such as FeKa:FINISH.
    /// </summary>
    void OnUnknownPacket(string message, IPEndPoint remote);


    // Engine queries

    /// <summary>Returns the map name and game mode to show in the server browser.</summary>
    ServerBrowserInfo GetServerBrowserInfo();


    // State broadcasting

    /// <summary>
    /// Called by the engine when it needs to push a fresh game state to all clients.
    /// The engine provides a snapshot of the current player list.
    /// </summary>
    void BroadcastGameState(List<ConnectedPlayer> players);

    /// <summary>
    /// Called by the engine to send the initial game state to a newly joined client.
    /// </summary>
    void SendWelcomeState(IPEndPoint endpoint, List<ConnectedPlayer> players);
}

public class ServerBrowserInfo
{
    public string MapName { get; set; } = "";
    public string GameMode { get; set; } = "";
}