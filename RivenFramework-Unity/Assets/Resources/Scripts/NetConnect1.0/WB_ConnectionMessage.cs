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

public class WB_ConnectionMessage : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private Action onButtonPressed;

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    public TMP_Text messageTitle, messageContent, buttonText;
    public Button button;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        button.onClick.AddListener(OnButtonClicked);
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void OnButtonClicked()
    {
        onButtonPressed?.Invoke();
        widgetManager ??= FindObjectOfType<GI_WidgetManager>();
        widgetManager?.ToggleWidget(this.gameObject.name);
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Sets up the popup with the given text and an optional callback for when the button is pressed
    /// If no callback is provided, the button will simply close the widget
    /// </summary>
    public void Setup(string title, string message, string btnText, Action callback = null)
    {
        messageTitle.text   = title;
        messageContent.text = message;
        buttonText.text     = btnText;
        onButtonPressed    = callback;
    }

    #endregion
}
