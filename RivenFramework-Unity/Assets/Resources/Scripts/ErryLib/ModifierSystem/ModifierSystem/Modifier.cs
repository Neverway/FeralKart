using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ErryLib.GameEvents;

/// <summary>
/// This class is used to modify the values of <see cref="Modifiable"/>s as well as react to <see cref="GameEvent"/>s by implementing 
/// <see cref="ListensToGameEvent{T}"/>. 
/// <para><see cref="Modifier"/>s must be registered to <see cref="ActiveModifiers"/> via
/// <see cref="RegisterModifier"/> or <see cref="UnregisterModifier"/> for them to have any effect</para>
/// </summary>
[Serializable]
public abstract class Modifier
{
    /// <summary>
    /// Registers the <see cref="Modifier"/> to the global list <see cref="ActiveModifiers"/>. This will <b>enable</b> the
    /// <see cref="Modifier"/> to affect <see cref="Modifiable"/>s and react to <see cref="GameEvent"/>s
    /// </summary>
    public void RegisterModifier()
    {
        //Dont add modifier if it already exists in ActiveModifiers (unless multiRegister is true)
        if (ActiveModifiers.Contains(this))
            return;

        if (ActiveModifiers is ListensToGameEvent gameEventListener)
            GameEventSystem.Register(gameEventListener);

        ActiveModifiers.Add(this);
        OnRegisterModifier();
    }

    /// <summary>
    /// Unregisters the <see cref="Modifier"/> from the global list <see cref="ActiveModifiers"/>. This will <b>disable</b> the
    /// <see cref="Modifier"/> from affecting <see cref="Modifiable"/>s and reacting to <see cref="GameEvent"/>s
    /// </summary>
    public void UnregisterModifier()
    {
        if (ActiveModifiers.Remove(this))
            OnUnregisterModifier();

        if (ActiveModifiers is ListensToGameEvent gameEventListener)
            GameEventSystem.UnRegister(gameEventListener);
    }
    /// <summary>Called when this modifier is successfully registered as active modifier</summary>
    protected virtual void OnRegisterModifier() { }
    /// <summary>Called when this modifier is successfully removed from active modifiers</summary>
    protected virtual void OnUnregisterModifier() { }
    /// <summary>
    /// Whenever <see cref="Modifiable{T}.Get"/> is called to retrieve the modifiable's value, all
    /// <see cref="ActiveModifiers"/> will call this method to modify that <paramref name="modifiableValue"/> IF this <see cref="Modifier"/>
    /// has passed the check from <see cref="Modifiable{TValue}.CanBeModifiedBy(Modifier)"/>
    /// </summary>
    public abstract void ModifyValue(Modifiable modifiableValue);


    private static List<Modifier> _activeModifiers;

    /// <summary>
    /// <see cref="Modifier"/>s must be in this list in order to affect <see cref="Modifiable"/>s and react to <see cref="GameEvent"/>s.
    /// <para>You can register and unregister <see cref="Modifier"/>s to this list by calling <see cref="RegisterModifier"/> and 
    /// <see cref="UnregisterModifier"/>. (Try to avoid calling <see cref="List{T}.Add(T)"/> or <see cref="List{T}.Remove(T)"/> directly)</para>
    /// </summary>
    public static List<Modifier> ActiveModifiers
    {
        get 
        { 
            //Instantiate ActiveModifiers if it is ever null when referenced
            if (_activeModifiers == null)
                _activeModifiers = new List<Modifier>();
            return _activeModifiers;
        }
        private set { }
    }

    /// <summary>This is only ever called on loading of game to reset</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ClearAllActiveModifiers()
    {
        _activeModifiers = null;
    }

    /// <summary>
    /// Attempts to modify the value of <paramref name="modifiable"/> via all <see cref="ActiveModifiers"/>
    /// <para><b>This should only be called through <see cref="Modifiable{T}.Get"/></b></para>
    /// </summary>
    /// <param name="modifiable"> The <see cref="Modifiable"/> that you wish to modify through all <see cref="ActiveModifiers"/></param>
    public static void RunModifiableThroughActiveModifiers(Modifiable modifiable)
    {
        //For each active modifier, check if the modifiable CAN be modified by it, and if it can, call Modifier.ModifyValue()
        foreach (Modifier mod in ActiveModifiers)
            if (modifiable.CanBeModifiedBy(mod))
                mod.ModifyValue(modifiable);
    }
}

public interface ListensToGameEvent
{
    private static List<ListensToGameEvent> timedOutModifiers;
    public static List<ListensToGameEvent> GetTimedOutModifiers()
    {
        if (timedOutModifiers == null)
            timedOutModifiers = new List<ListensToGameEvent>();

        return timedOutModifiers;
    }
    [RuntimeInitializeOnLoadMethod]
    public static void ClearTimeOuts() => GetTimedOutModifiers().Clear();
    public void TimeOut() => GetTimedOutModifiers().Add(this);

    public bool TryReactToEvent<T>(T gameEvent, InvokeTiming timing) where T : GameEvent<T>
    {
        if (this is ListensToGameEvent<T> && !GetTimedOutModifiers().Contains(this))
        {
            if ((this as ListensToGameEvent<T>).ReactToEvent(gameEvent, timing))
                TimeOut();
            return true;
        }
        return false;
    }
}
public interface ListensToGameEvent<T> : ListensToGameEvent where T : GameEvent<T>
{
    public bool ReactToEvent(T gameEvent, InvokeTiming timing);
}