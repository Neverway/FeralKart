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
    private T tValue;
    private bool dirty = false;
    private NetVariableOwner owner;
    private string key;
    public event Action<T> OnChanged;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    internal NetVariable(string _key, T _initialValue, NetVariableOwner _owner, Action<T> _onChanged)
    {
        key = _key;
        tValue = _initialValue;
        owner = _owner;
        if (_onChanged != null) OnChanged += _onChanged;
    }


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    /// <summary>
    /// Read or write the value.
    /// Setting marks the var dirty so it will be included in the next VARSYNC send.
    /// OnChanged fires immediately on the owning client, remote clients fire it when they receive the packet.
    /// </summary>
    public T Value
    {
        get => tValue;
        set
        {
            if (EqualityComparer<T>.Default.Equals(tValue, value)) return;
            tValue = value;
            dirty = true;
            OnChanged?.Invoke(tValue);
        }
    }
 
    /// <summary>
    /// Called by GI_NetworkManager on a remote client to update the value without triggering another send.
    /// </summary>
    internal void SetRemote(T value)
    {
        if (EqualityComparer<T>.Default.Equals(tValue, value)) return;
        tValue = value;
        OnChanged?.Invoke(tValue);
    }
 
    internal bool IsDirty   => dirty;
    internal void ClearDirty() => dirty = false;
    internal string Key     => key;
 
    /// <summary>
    /// Serialize the current value to a string for the wire packet.
    /// </summary>
    internal string Serialize() => tValue?.ToString() ?? "";
 
    /// <summary>
    /// The type tag sent in the packet so the receiver knows how to parse it.
    /// </summary>
    internal string TypeTag => typeof(T).Name;   // TODO Right now this only supports these types: Int32, Single, Boolean, String


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}

internal interface INetVarInternal
{
    bool IsDirty  { get; }
    void ClearDirty();
    VariableEntry ToWireEntry();
    void ApplyFromWire(VariableEntry entry);
}
 
internal class NetVariableTyped<T> : INetVarInternal
{
    internal readonly NetVariable<T> _var;
 
    internal NetVariableTyped(string key, T initial, NetVariableOwner owner, Action<T> onChanged)
    {
        _var = new NetVariable<T>(key, initial, owner, onChanged);
    }
 
    public bool IsDirty => _var.IsDirty;
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
    public string ObjectId;
    public List<VariableEntry> Vars;
}