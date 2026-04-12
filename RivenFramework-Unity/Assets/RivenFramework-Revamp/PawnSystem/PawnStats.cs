//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PawnStats : ICloneable
{
    public List<StatusEffect> statusEffects = new List<StatusEffect>();
    
    [Header("Health")]
    public float health = 100f;
    public float invulnerabilityTime = 1f;

    [Header("Teaming")] 
    public string team = "";
    public List<string> alliedTeams = new List<string>();
    public List<string> opposedTeams = new List<string>();
    
    public abstract object Clone();
}

public enum ControlMode
{
    LocalPlayer,
    CPU,
    NetworkPlayer
};

public abstract class StatusEffect
{
    public Pawn targetPawn;
    public void Apply(Pawn _targetPawn)
    {
        targetPawn =  _targetPawn;
        targetPawn.currentStats.statusEffects.Add(this);
        OnApply();
    }

    public void Remove()
    {
        targetPawn.currentStats.statusEffects.Remove(this);
        OnRemove();
        targetPawn =  null;
    }
    
    public abstract void OnApply();
    public abstract DamageInfo OnModifyHealth(DamageInfo _info);
    public abstract void OnUpdate();
    public abstract void OnRemove();
}
