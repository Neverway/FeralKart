//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose: Used to define the "modes" for a gamemode, as an example you might have a gamemode for Minecraft
//      and that gamemode would have the following pawns "Pawn_Minecraft_Survival", "Pawn_Minecraft_Spectator", "Pawn_Minecraft_Creative", 
// Notes: These objects are used in the WorldSettings
//
//=============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Neverway.Framework.PawnManagement
{
    [CreateAssetMenu(fileName = "GameMode", menuName = "Neverway/ScriptableObjects/Pawns & Gamemodes/GameMode")]
    public class GameMode : ScriptableObject
    {
        //=-----------------=
        // Public Variables
        //=-----------------=
        [Tooltip("This represents what type of pawn will be spawned depending on what gamemode value is set. So using Minecraft as an example 0 would be the default pawn, 1 would be spectator, 2 would be creative.")]
        public GameObject[] gamemodePawns;


        //=-----------------=
        // Private Variables
        //=-----------------=


        //=-----------------=
        // Reference Variables
        //=-----------------=


        //=-----------------=
        // Mono Functions
        //=-----------------=

        //=-----------------=
        // Internal Functions
        //=-----------------=


        //=-----------------=
        // External Functions
        //=-----------------=
    }
}
