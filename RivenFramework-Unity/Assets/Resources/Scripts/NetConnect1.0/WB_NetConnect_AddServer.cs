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
using RivenFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WB_NetConnect_AddServer : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    [SerializeField] private Button buttonConfirm, buttonCancel;
    [SerializeField] private TMP_InputField serverName, serverAddress;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        buttonConfirm.onClick.AddListener(delegate { OnClick("buttonConfirm"); });
        buttonCancel.onClick.AddListener(delegate { OnClick("buttonCancel"); });
    }



    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    
    private void OnClick(string button)
    {
        switch (button)
        {
            case "buttonConfirm":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                GameInstance.Get<GI_NetworkManager>().AddServerEntry(serverName.text, serverAddress.text);
                FindObjectOfType<WB_NetConnect>().Init();
                widgetManager.ToggleWidget(this.gameObject.name);
                break;
            case "buttonCancel":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                widgetManager.ToggleWidget(this.gameObject.name);
                break;
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
