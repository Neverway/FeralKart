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
using RivenFramework;
using UnityEngine;

public class WB_RaceResults : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public Transform entryRoot;
    public GameObject entryPrefab;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        var finishWidget = widgetManager.GetExistingWidget("WB_RaceFinished");
        if (finishWidget != null) Destroy(finishWidget);
            
        
        var raceManager = GameInstance.Get<GI_RaceManager>();
        if  (raceManager == null) return;
        
        var sorted = new List<RaceResultEntry>(raceManager.lastRaceResults);
        sorted.Sort((a, b) => CalculateScore(b).CompareTo(CalculateScore(a)));

        foreach (var result in raceManager.lastRaceResults)
        {
            var entryObject = Instantiate(entryPrefab, entryRoot);
            var entry = entryObject.GetComponent<WB_RaceResults_Entry>();
            if (entry == null) continue;
            entry.Init(result);
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public static int CalculateScore(RaceResultEntry result)
    {
        if (result.Failed) return 0;
        // Race placement bonus works as follows
        // 1st +1000, 2nd +800, 3rd +600, 4th +400, 5th +200, 6th and so on get +0
        // You can increase the multiplier by changing this   V   value~Liz
        return Mathf.Max(0, 1000 - (result.Placement - 1) * 200) + result.Kills * 50 + result.LivesRemaining * 25;
    }

    #endregion
}
