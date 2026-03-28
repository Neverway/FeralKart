//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FeKaItem_Shield : ItemBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public float shieldAmount = 50f;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public override ItemBehaviour GetClone()
    {
        return (ItemBehaviour)this.MemberwiseClone();
    }
    
    public override bool OnPickup(FeKaPawn pawn)
    {
        var stats = pawn.FeKaCurrentStats;
        if (stats != null) stats.shield = Mathf.Min(stats.shield + shieldAmount, stats.MaxShield);
        return false;
    }


    #endregion
}
