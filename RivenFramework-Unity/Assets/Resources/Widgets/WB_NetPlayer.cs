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
using TMPro;
using UnityEngine;

public class WB_NetPlayer : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public TMP_InputField nameInput;
    private GI_NetworkManager networkManager;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Start()
    {
        networkManager ??= GameInstance.Get<GI_NetworkManager>();
        networkManager.LoadNetProfile();
        nameInput.text = networkManager.localProfile.playerName;

        nameInput.onEndEdit.AddListener(_ => SetName());
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void SetName()
    {
        networkManager.localProfile.playerName = nameInput.text;
        networkManager.SaveNetProfile();
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
