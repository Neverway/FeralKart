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

public class WB_Textbox : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public Image portrait;
    public new TMP_Text name;
    public TMP_Text chat;
    public TextboxDisplayMode displayMode;
    public bool allowPlayerToAdvanceText = true;

    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Update()
    {
        switch (displayMode)
        {
            case TextboxDisplayMode.monologue:
                portrait.enabled = false;
                name.enabled = false;
                chat.rectTransform.offsetMin = new Vector2(15, 15);
                chat.rectTransform.offsetMax = new Vector2(-15, -15);
                break;
            case TextboxDisplayMode.dialogueNoPortrait:
                portrait.enabled = false;
                name.enabled = true;
                chat.rectTransform.offsetMin = new Vector2(15, 15);
                chat.rectTransform.offsetMax = new Vector2(-15, -15);
                break;
            case TextboxDisplayMode.dialogue:
                portrait.enabled = true;
                name.enabled = true;
                chat.rectTransform.offsetMin = new Vector2(100, 15);
                chat.rectTransform.offsetMax = new Vector2(-15, -15);
                break;
            case TextboxDisplayMode.shopMono:
                SetDrawInBack();
                portrait.enabled = false;
                name.enabled = false;
                chat.rectTransform.offsetMin = new Vector2(15, 15);
                chat.rectTransform.offsetMax = new Vector2(-200, -15);
                break;
            case TextboxDisplayMode.shopDia:
                SetDrawInBack();
                portrait.enabled = true;
                name.enabled = true;
                chat.rectTransform.offsetMin = new Vector2(100, 15);
                chat.rectTransform.offsetMax = new Vector2(-200, -15);
                break;
            case TextboxDisplayMode.centered:
                SetDrawInBack();
                portrait.enabled = false;
                name.enabled = false;
                chat.rectTransform.offsetMin = new Vector2(200, 15);
                chat.rectTransform.offsetMax = new Vector2(-200, -15);
                break;
        }
    }

    private void OnDestroy()
    {
        //GameInstance.Get<GI_TextboxManager>().textEventActive = false;
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void SetDrawInBack()
    {
        gameObject.transform.SetAsFirstSibling();
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
