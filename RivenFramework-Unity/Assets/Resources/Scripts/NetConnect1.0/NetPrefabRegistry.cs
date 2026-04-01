using System;
using System.Collections.Generic;
using UnityEngine;

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
            else
                Debug.LogWarning($"[NetPrefabRegistry] Skipping entry: key='{e.Key}' prefab={e.Prefab}");
        }
        Debug.Log($"[NetPrefabRegistry] Built lookup with {_lookup.Count} entries.");
    }
    
    private void OnEnable()
    {
        BuildLookup();
    }
}