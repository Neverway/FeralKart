//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumePain : Volume
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    [SerializeField]
    private DamageInfo damageInfo = new DamageInfo(amount: 10);


    //=-----------------=
    // Private Variables
    //=-----------------=


    //=-----------------=
    // Reference Variables
    //=-----------------=


    //=-----------------=
    // Mono Functions
    //=-----------------=
    private void Update()
    {
        foreach (var entity in pawnsInTrigger)
        {
            // If no team specified, or self-infliction enabled, hurt everyone (Kinda metal huh)
            if (unaffectedTeams.Count == 0 || ignoreUnaffectedTeamsFilter)
            {
                entity.ModifyHealth(damageInfo);
            }
            else
            {
                if (ignoreUnaffectedTeamsFilter) return;
                
                // If teams match allow healing only
                if (unaffectedTeams.Contains(entity.currentStats.team) && damageInfo.amount < 0)
                {
                    entity.ModifyHealth(damageInfo);
                }
                // If teams don't match allow pain only
                if (!unaffectedTeams.Contains(entity.currentStats.team) && damageInfo.amount > 0)
                {
                    entity.ModifyHealth(damageInfo);
                }
            }
        }
    }

    //=-----------------=
    // Internal Functions
    //=-----------------=


    //=-----------------=
    // External Functions
    //=-----------------=
}
