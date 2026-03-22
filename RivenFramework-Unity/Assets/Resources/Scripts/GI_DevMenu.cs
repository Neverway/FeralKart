//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using RivenFramework;
using System;
using UnityEngine;

public class GI_DevMenu : GameInstanceModule
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public DevMenuLayoutSettings layoutSettings;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private InputActions.UIActions inputActions;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private FPPawnActions action = new FPPawnActions();
    private GI_WidgetManager widgetManager;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Start()
    {
        // Setup inputs
        inputActions = new InputActions().UI;
        inputActions.Enable();

        // Get Widget Manager
        widgetManager = GetComponent<GI_WidgetManager>();
    }

    private void Update()
    {
        if (inputActions.DevMenu.WasPressedThisFrame())
        {
            widgetManager.ToggleWidget<WB_DevMenu>();
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}

[Serializable]
public class DevMenuLayoutSettings
{
    public DevMenuTabLayout[] tabs;
}
[Serializable]
public class DevMenuTabLayout
{
    public string tabName;
    [Polymorphic, SerializeReference] public DevMenuLayoutGroup[] layoutGroups;
}
[Serializable]
public abstract class DevMenuLayoutGroup
{
    [Serializable]
    public struct Reference
    {
        [Polymorphic, SerializeReference] public DevMenuLayoutGroup group;
    }
}
[Serializable]
public class DevMenuLayoutGroupButtonGrid : DevMenuLayoutGroup
{
    public int columns;
}