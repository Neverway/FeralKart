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

public class Pawn : Actor
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    [Header("Pawn Data")]
    [Tooltip("If true, the pawn will not be able to perform any actions")]
    public bool isPaused;
    [Tooltip("If true, the pawn will be considered dead and will be queued for despawning if enabled")]
    public bool isDead;
    [Tooltip("If enabled, the pawn will be despawned when it dies after the despawnOnDeathDelay time passes")]
    public bool despawnOnDeath;
    [Tooltip("How long after a pawn dies until it is despawned (if despawnOnDeath is enabled)")]
    public float despawnOnDeathDelay=3f;


    //=-----------------=
    // Private Variables
    //=-----------------=
    private bool isInvulnerable;


    //=-----------------=
    // Reference Variables
    //=-----------------=
    public PawnStats defaultStats;
    public PawnStats currentStats;
    public PawnActions action;
    public Transform viewPoint;
    public Pawn_AttachmentPoint physObjectAttachmentPoint;

    public event Action<DamageInfo> OnPawnHurt;
    public event Action<DamageInfo> OnPawnHeal;
    public event Action<DamageInfo> OnPawnDeath;

    //=-----------------=
    // Mono Functions
    //=-----------------=
    public virtual void Update()
    {
        foreach (var statusEffect in currentStats.statusEffects)
        {
            statusEffect.OnUpdate();
        }
    }


    //=-----------------=
    // Internal Functions
    //=-----------------=
    private IEnumerator InvulnerabilityCooldown()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(((PawnStats)currentStats).invulnerabilityTime);
        isInvulnerable = false;
    }
    

    //=-----------------=
    // External Functions
    //=-----------------=
    public void ModifyHealth(DamageInfo _info)
    {
        if (isInvulnerable) return;
        StartCoroutine(InvulnerabilityCooldown());

        foreach (var statusEffect in currentStats.statusEffects)
        {
            _info = statusEffect.OnModifyHealth(_info);
        }
        
        switch (_info.amount)
        {
            case > 0:
                OnPawnHeal?.Invoke(_info);
                isDead = false;
                break;
            case < 0:
                if (isDead) return;
                OnPawnHurt?.Invoke(_info);
                break;
        }

        if (currentStats.health + _info.amount <= 0)
        {
            Kill(_info);
        }

        if (currentStats.health + _info.amount > defaultStats.health) currentStats.health = defaultStats.health;
        else if (currentStats.health + _info.amount < 0) currentStats.health = 0;
        else currentStats.health += _info.amount;

        foreach (var statusEffect in _info.statusEffects)
        {
            statusEffect.Apply(this);
        }
    }

    public void ModifyHealth(float _amount)
    {
        ModifyHealth(new DamageInfo(_amount));
    }
    
    public void Kill(DamageInfo _info)
    {
        if (isDead) return;
        // Instantly sets the pawns health to zero, firing its onDeath event
        OnPawnDeath?.Invoke(_info);
        isDead = true;
        if (despawnOnDeath)
        {
            Destroy(gameObject, despawnOnDeathDelay);
        }
    }
    public void Kill()
    {
        Kill(new DamageInfo());
    }
}

[Serializable]
public struct DamageInfo
{
    [Tooltip("The amount of health to modify, negative values damage, positive values heal")]
    public float amount;
    [Tooltip("The type of damage (like normal, electric, fire, poison)")]
    public DamageType type;
    [Tooltip("The owner responsible for the damage (like a player or npc, this can be null for things like spike pits)")]
    public Pawn instigator;
    [Tooltip("The actual source of the damage (like an instigator's gun, a spike pit, leave null for things like the void)")]
    public DamageSource source;
    [Tooltip("The effects to apply to the target when damaged")]
    public List<StatusEffect> statusEffects;

    private DamageInfo(float amount, Pawn instigator, DamageSource source, DamageType type, List<StatusEffect> statusEffects = null)
    {
        this.amount = amount;
        this.instigator = instigator;
        this.source = source;
        this.type = type;
        this.statusEffects = statusEffects;
        this.statusEffects ??= new List<StatusEffect>();
    }

    public DamageInfo(float amount) : this(amount, null, new DamageSource(), DamageType.Normal, new List<StatusEffect>()) { }
}

[Serializable]
public struct DamageSource
{
    public Sprite icon;
    public string name;
    public GameObject gameObject;
}

[Serializable]
public enum DamageType
{
    Normal,
    Explosive, // decreased by Hardened, increased by Fragile
    Electric, // decreased by being airborn, increased by Wet
    Fire, // decreased by Wet, increased by oiled
    Ice, // decreased by OnFire, increased by Wet
    Poison,
    Magic, // decreased by Warded, increased by Cursed
    Suffocation,
}

// Status Effect Ideas
// Hardened
// Fragile

// Oiled
// Wet
// Burning

// Warded
// Cursed

// Slowed
// Accelerated

// Bleeding