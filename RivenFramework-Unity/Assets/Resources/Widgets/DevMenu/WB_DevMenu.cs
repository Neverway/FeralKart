//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using RivenFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WB_DevMenu : WidgetBlueprint
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Header("Menu Item Prefabs")]
    public DevMenuItem_Button prefab_button;

    [Header("Container References")]
    public Transform container_tabs;
    public Transform container_menuItems;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        GI_WidgetManager widgetManager = GameInstance.Get<GI_WidgetManager>();

        DevMenuItemInfo[] devMenuItemInfos = DevMenuAttributes.GetDevMenuItemInfos();
        foreach (DevMenuItemInfo menuItemInfo in devMenuItemInfos)
            CreateMenuItem(menuItemInfo);
    }

    private void Update()
    {
    	Cursor.lockState = CursorLockMode.None;
    	Cursor.visible = true;
	}

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void CreateButton(DevMenuItemInfo menuItemInfo)
    {
        DevMenuItem_Button newButton = Instantiate(prefab_button, container_menuItems);
        newButton.SetupInfo(menuItemInfo);
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void CreateMenuItem(DevMenuItemInfo menuItemInfo)
    {
        var attribute = menuItemInfo.attributeInfo.Attribute as DevMenuItemAttribute;
        attribute.CheckForInvalidUsageErrors(menuItemInfo.attributeInfo.Member);

        if (attribute is DevMenuButtonAttribute buttonAttribute)
            CreateButton(menuItemInfo);
    }

    #endregion
}