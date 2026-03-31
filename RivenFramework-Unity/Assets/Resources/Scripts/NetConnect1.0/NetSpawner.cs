//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public static class NetSpawner
{
    /// <summary>
    /// Request the server to spawn a networked object.
    /// The server will confirm and broadcast to all clients.
    /// <paramref name="onSpawned"/> is called on this client once the server confirms,
    /// with the newly instantiated GameObject and its assigned network ID.
    /// </summary>
    /// <param name="prefabKey">Key registered in NetPrefabRegistry.</param>
    /// <param name="position">World-space spawn position.</param>
    /// <param name="rotation">Spawn rotation.</param>
    /// <param name="onSpawned">Optional callback fired on the requesting client after the object is instantiated.</param>
    public static void Spawn(string prefabKey, Vector3 position, Quaternion rotation, Action<GameObject, string> onSpawned = null)
    {
        var nm = GameInstance.Get<GI_NetworkManager>();
        if (nm == null)
        {
            Debug.LogError("[NetSpawner] GI_NetworkManager not found.");
            return;
        }
        nm.RequestSpawn(prefabKey, position, rotation, onSpawned);
    }
 
    /// <summary>
    /// Request the server to despawn a networked object identified by its network ID.
    /// The server will confirm and broadcast to all clients.
    /// </summary>
    /// <param name="networkObjectId">The ID assigned at spawn time.</param>
    public static void Despawn(string networkObjectId)
    {
        var nm = GameInstance.Get<GI_NetworkManager>();
        if (nm == null)
        {
            Debug.LogError("[NetSpawner] GI_NetworkManager not found.");
            return;
        }
        nm.RequestDespawn(networkObjectId);
    }
}

/// <summary>
/// ScriptableObject that maps a string key to a prefab.
/// Create one via right-click > Create > FeralKart > Net Prefab Registry.
/// </summary>
[CreateAssetMenu(menuName = "Neverway/Networking/Net Prefab Registry", fileName = "NetPrefabRegistry")]
public class NetPrefabRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string     Key;
        public GameObject Prefab;
    }
 
    public List<Entry> Entries = new List<Entry>();
 
    private Dictionary<string, GameObject> _lookup;
 
    /// <summary>Returns the prefab for the given key, or null if not found.</summary>
    public GameObject GetPrefab(string key)
    {
        if (_lookup == null)
            BuildLookup();
        return _lookup.TryGetValue(key, out var go) ? go : null;
    }
 
    private void BuildLookup()
    {
        _lookup = new Dictionary<string, GameObject>();
        foreach (var e in Entries)
        {
            if (!string.IsNullOrEmpty(e.Key) && e.Prefab != null)
                _lookup[e.Key] = e.Prefab;
        }
    }
}

[Serializable]
public class SpawnRequestPacket
{
    public string RequestId;
    public string PrefabKey;        // Which prefab to instantiate
    public float  PX, PY, PZ;      // Spawn position
    public float  RX, RY, RZ;      // Spawn rotation (Euler)
}
 
[Serializable]
public class SpawnBroadcastPacket
{
    public string RequestId;
    public string NetworkObjectId;  // Server-assigned unique ID
    public string PrefabKey;
    public string OwnerEndpoint;    // The endpoint string of the client that requested the spawn
    public float  PX, PY, PZ;
    public float  RX, RY, RZ;
}
 
[Serializable]
public class DespawnPacket
{
    public string NetworkObjectId;
}
