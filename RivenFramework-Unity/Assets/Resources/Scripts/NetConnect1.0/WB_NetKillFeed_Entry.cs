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
using UnityEngine.UI;

public class WB_NetKillFeed_Entry : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public Sprite defaultDeathIcon;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public Image instigatorEffect;
    public TMP_Text instigator;
    public Image source;
    public Image instigatorMethod;
    public TMP_Text recipient;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Start()
    {
        Destroy(gameObject, 3f);
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void Initialize(Sprite _instigatorEffectIcon=null, string _instigatorName=null, Sprite _sourceIcon=null, Sprite _instigatorMethodIcon=null, bool _recipientEliminated=false, string _recipientName=null)
    {
        // Enable used elements
        instigatorEffect.gameObject.SetActive(_instigatorEffectIcon != null);
        instigator.gameObject.SetActive(_instigatorName != null);
        source.gameObject.SetActive(_sourceIcon != null);
        instigatorMethod.gameObject.SetActive(_instigatorMethodIcon != null);
        
        // Set their values
        if (_instigatorEffectIcon != null) instigatorEffect.sprite = _instigatorEffectIcon;
        if (_instigatorName != null) instigator.text = _instigatorName;
        if (_instigatorMethodIcon != null) instigatorMethod.sprite = _instigatorMethodIcon;
        if (_recipientName != null) recipient.text = _recipientName;
    }


    #endregion
}
