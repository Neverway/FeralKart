using System.Collections.Generic;
using System.Net;

public interface IGameRulesEngine
{
    /// <summary>Sends a packet to every connected player.</summary>
    void BroadcastToAll(string message);

    /// <summary>Sends a packet to one specific connected player.</summary>
    void SendToPlayer(ConnectedPlayer player, string message);

    /// <summary>Sends a packet to a specific endpoint. Used for sending the welcome state on join.</summary>
    void SendToEndpoint(IPEndPoint endpoint, string message);

    /// <summary>Returns a snapshot of the currently connected player list.</summary>
    List<ConnectedPlayer> GetConnectedPlayers();

    /// <summary>Requests the server spawn a networked object and broadcast it to all clients.</summary>
    void RequestSpawn(string prefabKey, float positionX, float positionY, float positionZ, float rotationX, float rotationY, float rotationZ);

    /// <summary>
    /// Requests the server spawn a networked object and returns the assigned network ID.
    /// Use this overload when you need to track the object for later despawning.
    /// </summary>
    string RequestSpawnAndGetId(string prefabKey, float positionX, float positionY, float positionZ, float rotationX, float rotationY, float rotationZ);

    /// <summary>Requests the server despawn a networked object by its network ID.</summary>
    void RequestDespawn(string networkObjectId);

    /// <summary>Asks GameRules to broadcast the current game state to all clients.</summary>
    void BroadcastState();
}