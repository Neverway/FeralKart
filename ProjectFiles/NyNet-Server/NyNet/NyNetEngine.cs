// =========================================================================================================
// NyNetEngine
// =========================================================================================================
// Concrete implementation of IGameRulesEngine.
// Wraps the UDP socket and shared server state so GameRules can call back into the server
// without touching any internals directly.
// =========================================================================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class NyNetEngine : IGameRulesEngine
{
    #region ========================================( Variables )======================================================//
    /*-----[ Reference Variables ]----------------------------------------------------------------------------*/
    private readonly UdpClient socket;
    private readonly List<ConnectedPlayer> players;
    private readonly object playersLock;
    private readonly List<SpawnBroadcastPacket> liveSpawns;
    private readonly object liveSpawnsLock;
    private readonly ServerState state;
    private readonly string protocolMagic;

    // GameRules back-reference, set by Program.cs immediately after both objects are created
    public IGameRules? GameRules { get; set; }
    #endregion


    #region =======================================( Functions )======================================================//
    /*-----[ Constructor ]------------------------------------------------------------------------------------*/

    public NyNetEngine(
        UdpClient socket,
        List<ConnectedPlayer> players,
        object playersLock,
        List<SpawnBroadcastPacket> liveSpawns,
        object liveSpawnsLock,
        ServerState state,
        string protocolMagic)
    {
        this.socket = socket;
        this.players = players;
        this.playersLock = playersLock;
        this.liveSpawns = liveSpawns;
        this.liveSpawnsLock = liveSpawnsLock;
        this.state = state;
        this.protocolMagic = protocolMagic;
    }


    /*-----[ IGameRulesEngine Implementation ]----------------------------------------------------------------*/

    public void BroadcastToAll(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        lock (playersLock)
        {
            foreach (var player in players)
            {
                if (player.EndPoint == null) continue;
                try { socket.Send(data, data.Length, player.EndPoint); }
                catch { }
            }
        }
    }

    public void SendToPlayer(ConnectedPlayer player, string message)
    {
        if (player.EndPoint == null) return;
        byte[] data = Encoding.UTF8.GetBytes(message);
        try { socket.Send(data, data.Length, player.EndPoint); }
        catch { }
    }

    public void SendToEndpoint(IPEndPoint endpoint, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        try { socket.Send(data, data.Length, endpoint); }
        catch { }
    }

    public List<ConnectedPlayer> GetConnectedPlayers()
    {
        lock (playersLock)
            return new List<ConnectedPlayer>(players);
    }

    public void RequestSpawn(string prefabKey, float positionX, float positionY, float positionZ, float rotationX, float rotationY, float rotationZ)
    {
        RequestSpawnAndGetId(prefabKey, positionX, positionY, positionZ, rotationX, rotationY, rotationZ);
    }

    public string RequestSpawnAndGetId(string prefabKey, float positionX, float positionY, float positionZ, float rotationX, float rotationY, float rotationZ)
    {
        var broadcast = new SpawnBroadcastPacket
        {
            NetworkObjectId = Guid.NewGuid().ToString(),
            PrefabKey = prefabKey,
            RequestId = "",
            OwnerEndpoint = "",
            PX = positionX, PY = positionY, PZ = positionZ,
            RX = rotationX, RY = rotationY, RZ = rotationZ
        };

        lock (liveSpawnsLock)
            liveSpawns.Add(broadcast);

        BroadcastToAll(protocolMagic + ":SPAWN:" + JsonSerializer.Serialize(broadcast));
        return broadcast.NetworkObjectId;
    }

    public void RequestDespawn(string networkObjectId)
    {
        lock (liveSpawnsLock)
            liveSpawns.RemoveAll(spawn => spawn.NetworkObjectId == networkObjectId);

        var despawnPacket = new DespawnPacket { NetworkObjectId = networkObjectId };
        BroadcastToAll(protocolMagic + ":DESPAWN:" + JsonSerializer.Serialize(despawnPacket));
    }

    public void BroadcastState()
    {
        if (GameRules == null) return;
        List<ConnectedPlayer> snapshot;
        lock (playersLock)
            snapshot = new List<ConnectedPlayer>(players);
        GameRules.BroadcastGameState(snapshot);
    }
    #endregion
}