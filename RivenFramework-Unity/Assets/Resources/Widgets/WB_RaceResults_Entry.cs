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
using TMPro;
using UnityEngine;

public class WB_RaceResults_Entry : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public TMP_Text placement, score, playerName, finishTime,  damageDealt, kills, livesRemaining;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void Init(RaceResultEntry result)
    {
        if (placement != null)
            placement.text = result.Failed ? "DNF" : result.Placement.ToString();

        if (playerName != null)
            playerName.text = result.PlayerName;

        if (finishTime != null)
        {
            var t = TimeSpan.FromSeconds(result.FinishTime);
            finishTime.text = result.Failed ? "--" : $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        if (damageDealt != null)
            damageDealt.text = result.DamageDealt.ToString("F0");

        if (kills != null)
            kills.text = result.Kills.ToString();

        if (livesRemaining != null)
            livesRemaining.text = result.LivesRemaining.ToString();

        if (score != null)
            score.text = WB_RaceResults.CalculateScore(result).ToString();
    }


    #endregion
}
