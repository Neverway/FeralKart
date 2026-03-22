//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose: Data structure for the stats on a pawn
// Notes:
//
//=============================================================================

using System;
using UnityEngine;

namespace Neverway.Framework.PawnManagement
{
    public class CharacterData : Actor
    {
        public CharacterStats stats;
    }
    
    [Serializable]
    public class CharacterStats
    {
        [Header("Appearance")]
        public RuntimeAnimatorController animationController;
        public CharacterSounds sounds;
        public string name;

        [Header("Stats")] 
        public string team;
        public float invulnerabilityTime;
        public float health;
        public float movementSpeed;
        
        [Header("Logic Checks")]
        public Vector3 groundCheckOffset;
        public float groundCheckRadius;
        [Tooltip("The collision layers that will be checked when testing if the entity is grounded")]
        public LayerMask groundMask;
    }

    [Serializable]
    public class CharacterSounds
    {
        public AudioClip hurt;
        public AudioClip heal;
        public AudioClip death;

        public AudioClip alerted;
    }
}