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
using Cinemachine;
using DG.Tweening;
using UnityEngine;

public class Logic_TrackCartSpeed : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public float newSpeed;
    public float transitionDuration;
    public LogicInput<bool> setCartSpeed = new(false);


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public CinemachineDollyCart targetDollyCart;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        if (setCartSpeed.HasLogicOutputSource is false) return;
        setCartSpeed.CallOnSourceChanged(OnInputChanged);
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void OnInputChanged()
    {
        if (setCartSpeed.Get())
        {
            Sequence sequence = DOTween.Sequence();
            sequence.Append(DOVirtual.Float(targetDollyCart.m_Speed, newSpeed, transitionDuration, currentSpeed => targetDollyCart.m_Speed = currentSpeed));
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
