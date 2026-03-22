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
using UnityEngine;

public abstract class RiftController : Item
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Allows rifts to expand past the start position")]
    public bool allowExpandingRift;

    public enum CollapseBehavior
    {
        Default,                //Standard behavior, collapsing rift removes geometry.
        MirrorWhenCollapsed     //Collapsing rift can go past 0 into negative numbers, where it becomes mirrored.
    }
    
    [Tooltip("Decides the behavior when the rift is collapsed, allowing for alternate modes of the geogun.")]
    public CollapseBehavior collapseBehavior = CollapseBehavior.Default;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("Subscribed to by rift manager to tell when controller wants to collapse")]
    public abstract event Action OnCollapseHeld;
    [Tooltip("Subscribed to by rift manager to tell when controller wants to stop collapsing")]
    public abstract event Action OnCollapseReleased;
    [Tooltip("Subscribed to by rift manager to tell when controller wants to expand")]
    public abstract event Action OnExpandHeld;
    [Tooltip("Subscribed to by rift manager to tell when controller wants to stop expanding")]
    public abstract event Action OnExpandReleased;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
