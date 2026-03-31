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

public class NetVariable<T>
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    private T            _value;
    private bool         _dirty = false;
    private NetVarOwner  _owner;
    private string       _key;
    public event Action<T> OnChanged;
    internal NetVariable(string key, T initialValue, NetVarOwner owner, Action<T> onChanged)
    {
        _key   = key;
        _value = initialValue;
        _owner = owner;
        if (onChanged != null) OnChanged += onChanged;
    }


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    /// <summary>
    /// Read or write the value.
    /// Setting marks the var dirty so it will be included in the next VARSYNC send.
    /// OnChanged fires immediately on the owning client; remote clients fire it when they receive the packet.
    /// </summary>
    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            _dirty = true;
            OnChanged?.Invoke(_value);
        }
    }
 
    /// <summary>Called by GI_NetworkManager on a remote client to update the value without triggering another send.</summary>
    internal void SetRemote(T value)
    {
        if (EqualityComparer<T>.Default.Equals(_value, value)) return;
        _value = value;
        OnChanged?.Invoke(_value);
    }
 
    internal bool IsDirty   => _dirty;
    internal void ClearDirty() => _dirty = false;
    internal string Key     => _key;
 
    /// <summary>Serialize the current value to a string for the wire packet.</summary>
    internal string Serialize() => _value?.ToString() ?? "";
 
    /// <summary>The type tag sent in the packet so the receiver knows how to parse it.</summary>
    internal string TypeTag => typeof(T).Name;   // "Int32", "Single", "Boolean", "String"


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}

public class NetVarOwner : MonoBehaviour
{
    // ---- Public ----
    /// <summary>The network object ID — mirrors the NetTransform on this GameObject.</summary>
    public string NetworkObjectId => _netTransform != null ? _netTransform.networkObjectUId : "";
 
    // ---- Internal registry ----
    // Stored as an untyped interface so we can iterate without generics
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
 
    // ---- Registration ----
 
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
 
    // ---- Internals used by GI_NetworkManager ----
 
    /// <summary>Collect all dirty vars into a list of wire entries and mark them clean.</summary>
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
        if (_vars.TryGetValue(entry.Key, out var v))
            v.ApplyFromWire(entry);
        else
            Debug.LogWarning($"[NetVarOwner] Received unknown var key '{entry.Key}' on object '{NetworkObjectId}'");
    }
}
internal interface INetVarInternal
{
    bool      IsDirty  { get; }
    void      ClearDirty();
    VariableEntry  ToWireEntry();
    void      ApplyFromWire(VariableEntry entry);
}
 
internal class NetVariableTyped<T> : INetVarInternal
{
    internal readonly NetVariable<T> _var;
 
    internal NetVariableTyped(string key, T initial, NetVarOwner owner, Action<T> onChanged)
    {
        _var = new NetVariable<T>(key, initial, owner, onChanged);
    }
 
    public bool IsDirty    => _var.IsDirty;
    public void ClearDirty() => _var.ClearDirty();
 
    public VariableEntry ToWireEntry() => new VariableEntry
    {
        Key     = _var.Key,
        TypeTag = _var.TypeTag,
        Value   = _var.Serialize()
    };
 
    public void ApplyFromWire(VariableEntry entry)
    {
        try
        {
            T parsed = (T)Convert.ChangeType(entry.Value, typeof(T));
            _var.SetRemote(parsed);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetVarTyped] Failed to parse '{entry.Value}' as {typeof(T).Name}: {e.Message}");
        }
    }
}
 
[Serializable]
public class VariableEntry
{
    public string Key;
    public string TypeTag;
    public string Value;
}
 
[Serializable]
public class VariableSyncPacket
{
    public string         ObjectId;
    public List<VariableEntry> Vars;
}