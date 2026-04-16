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
using UnityEngine;

[CreateAssetMenu(menuName = "Neverway/Networking/Net Image Registry", fileName = "NetImageRegistry")]
public class NetImageRegistry : ScriptableObject
{

    [Serializable]
    public class Entry
    {
        public string Key;
        public Sprite Image;
    }
 
    public List<Entry> Entries = new List<Entry>();
 
    private Dictionary<string, Sprite> _lookup;
 
    /// <summary>Returns the image for the given key, or null if not found.</summary>
    public Sprite GetImage(string key)
    {
        
        if (_lookup == null)
            BuildLookup();
        return _lookup.TryGetValue(key, out var sprite) ? sprite : null;
    }
 
    private void BuildLookup()
    {
        _lookup = new Dictionary<string, Sprite>();
        foreach (var entry in Entries)
        {
            if (!string.IsNullOrEmpty(entry.Key) && entry.Image != null)
                _lookup[entry.Key] = entry.Image;
            else
                Debug.LogWarning($"[NetPrefabRegistry] Skipping entry: key='{entry.Key}' prefab={entry.Image}");
        }
        Debug.Log($"[NetPrefabRegistry] Built lookup with {_lookup.Count} entries.");
    }
    
    private void OnEnable()
    {
        BuildLookup();
    }
}
