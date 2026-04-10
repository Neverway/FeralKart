//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;


public class NetVariableOwner : MonoBehaviour
{
    /// <summary>
    /// The network object ID, this mirrors the NetTransform on this GameObject.
    /// </summary>
    public string NetworkObjectId
    {
        get { return _netTransform != null ? _netTransform.networkObjectUId : ""; }
    }

    private readonly Dictionary<string, INetVarInternal> _vars = new Dictionary<string, INetVarInternal>();
    private NetTransform _netTransform;
    private GI_NetworkManager _netManager;
 
    private void Awake()
    {
        _netTransform = GetComponent<NetTransform>();
        if (_netTransform == null)
            Debug.LogWarning($"[NetVarOwner] No NetTransform found on {name}. NetworkObjectId will be empty.");
    }
 
    private void Start()
    {
        _netManager = GameInstance.Get<GI_NetworkManager>();
        if (_netManager != null)
            _netManager.RegisterNetVarOwner(this);
    }
 
    private void OnDestroy()
    {
        if (_netManager != null)
            _netManager.UnregisterNetVarOwner(this);
    }
 
    /// <summary>
    /// Register a new synced variable on this object.
    /// Call this from Awake() on any component that needs synced state.
    /// </summary>
    public NetVariable<T> Register<T>(string key, T initialValue, Action<T> onChanged = null)
    {
        var wrapper = new NetVariableTyped<T>(key, initialValue, this, onChanged);
        _vars[key] = wrapper;
        return wrapper._var;
    }
 
 
    /// <summary>
    /// Collect all dirty vars into a list of wire entries and mark them clean.
    /// </summary>
    internal List<VariableEntry> FlushDirtyVars()
    {
        var result = new List<VariableEntry>();
        foreach (var kv in _vars)
        {
            if (!kv.Value.IsDirty) continue;
            result.Add(kv.Value.ToWireEntry());
            kv.Value.ClearDirty();
        }
        return result;
    }
 
    /// <summary>Apply a received wire entry to the matching var.</summary>
    internal void ApplyRemoteEntry(VariableEntry entry)
    {
        if (_vars.TryGetValue(entry.Key, out var netVarInternal))
            netVarInternal.ApplyFromWire(entry);
        else
            Debug.LogWarning($"[NetVarOwner] Received unknown var key '{entry.Key}' on object '{NetworkObjectId}'");
    }
}