//==========================================( Neverway 2025 )=========================================================//
// Author
//  Andre Blunt
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Dev_FizzlerAutoResizer : MonoBehaviour
{
#if UNITY_EDITOR
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [SerializeField] private Transform fizzlerStart;
    [SerializeField] private Transform fizzlerEnd;
    [SerializeField] private Transform fizzler;
    [SerializeField] private Vector3 offsetMultiplier;
    [SerializeField] private float thickness = 0.025f;

    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Update()
    {
        if (Application.isPlaying)
            return;

        if(fizzlerStart && fizzlerEnd && fizzler)
        {
            fizzler.position = (fizzlerStart.position + fizzlerEnd.position + offsetMultiplier) * 0.5f;
            fizzler.localScale = new Vector3(Vector3.Distance(fizzlerStart.position, fizzlerEnd.position), 3.9f, thickness);
        }
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
#endif
}
