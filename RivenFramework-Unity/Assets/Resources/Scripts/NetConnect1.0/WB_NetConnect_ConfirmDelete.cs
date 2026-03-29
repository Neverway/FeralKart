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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WB_NetConnect_ConfirmDelete : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    private WB_NetConnect netConnect;
    public TMP_Text serverName;
    [SerializeField] private Button buttonDelete, buttonCancel;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        buttonDelete.onClick.AddListener(delegate { OnClick("buttonDelete"); });
        buttonCancel.onClick.AddListener(delegate { OnClick("buttonCancel"); });
        netConnect ??= FindObjectOfType<WB_NetConnect>();
        var currentData = netConnect.serverEntryList[netConnect.currentlySelectedServerEntry];
        serverName.text = currentData.serverName.text;
    }



    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    
    private void OnClick(string button)
    {
        switch (button)
        {
            case "buttonDelete":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                FindObjectOfType<WB_NetConnect>().DeleteSelectedServer();
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
