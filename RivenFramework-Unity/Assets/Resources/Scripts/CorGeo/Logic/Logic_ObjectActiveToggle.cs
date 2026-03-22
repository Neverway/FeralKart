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
using UnityEngine;

/// <summary>
/// Disables or enables a target actor when powered
/// </summary>
public class Logic_ObjectActiveToggle : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public LogicInput<bool> active = new(true);

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [SerializeField] private GameObject targetObject;
    

    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        active.CallOnSourceChanged(Toggle);
        targetObject.SetActive(active.Get());
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void Toggle()
    {
        targetObject.SetActive(active.Get());
    }
    

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
