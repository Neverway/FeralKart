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

public class Pawn_RegenHealth : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("The amount of time to wait after taking damage before healing begins")]
    public float regenDelay=2;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private Pawn pawn;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Awake()
    {
        pawn = GetComponent<Pawn>();
        
        pawn.OnPawnHurt -= RegenHealth;
        pawn.OnPawnHurt += RegenHealth;
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void RegenHealth()
    {
        StopAllCoroutines();
        StartCoroutine(RegenDelay());
    }

    private IEnumerator RegenDelay()
    {
        yield return new WaitForSeconds(regenDelay);
        pawn.ModifyHealth(pawn.defaultStats.health);
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
