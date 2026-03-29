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

public class WB_NetConnect_EditServer : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    private WB_NetConnect netConnect;
    [SerializeField] private Button buttonConfirm, buttonCancel;
    [SerializeField] public TMP_InputField serverName, serverAddress;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        buttonConfirm.onClick.AddListener(delegate { OnClick("buttonConfirm"); });
        buttonCancel.onClick.AddListener(delegate { OnClick("buttonCancel"); });
        netConnect ??= FindObjectOfType<WB_NetConnect>();
        var currentData = netConnect.serverEntryList[netConnect.currentlySelectedServerEntry];
        serverName.text = currentData.serverName.text;
        serverAddress.text = currentData.serverAddress;
    }



    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    
    private void OnClick(string button)
    {
        switch (button)
        {
            case "buttonConfirm":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                netConnect ??= FindObjectOfType<WB_NetConnect>();
                GameInstance.Get<GI_NetworkManager>().EditServerEntry(netConnect.currentlySelectedServerEntry, serverName.text, serverAddress.text);
                netConnect.Init();
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
