using System;
using System.Collections.Generic;
using System.Net;

// Sent by a client to request that a networked object be spawned
public class SpawnRequestPacket
{
    public string PrefabKey { get; set; } = "";
    public string RequestId { get; set; } = "";
    public float PX { get; set; }
    public float PY { get; set; }
    public float PZ { get; set; }
    public float RX { get; set; }
    public float RY { get; set; }
    public float RZ { get; set; }
}

// Broadcast by the server to all clients to confirm a networked object spawn
public class SpawnBroadcastPacket
{
    public string NetworkObjectId { get; set; } = "";
    public string PrefabKey { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string OwnerEndpoint { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public float PX { get; set; }
    public float PY { get; set; }
    public float PZ { get; set; }
    public float RX { get; set; }
    public float RY { get; set; }
    public float RZ { get; set; }
}

// Sent by a client to request that a networked object be despawned
public class DespawnPacket
{
    public string NetworkObjectId { get; set; } = "";
}
