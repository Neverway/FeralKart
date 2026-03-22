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
using UnityEngine;
using UnityEngine.Events;

public class WidgetSelectable : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Called when the player hits the interact key")]
    public UnityEvent OnInteracted;
    [Tooltip("Called when the player hovers over this selectable")]
    public UnityEvent OnSelected;
    [Tooltip("Called when the player unhovers this selectable")]
    public UnityEvent OnUnselected;
    [Tooltip("Called when the player presses the negative axis navigation button adjacent to the current navigation direction (nav is vertical, this is left | nav is horizontal, this is up)")]
    public UnityEvent OnNavigationAdjacentNegative;
    [Tooltip("Called when the player presses the positive axis navigation button adjacent to the current navigation direction (nav is vertical, this is right | nav is horizontal, this is down)")]
    public UnityEvent OnNavigationAdjacentPositive;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public virtual void Interact()
    {
        OnInteracted?.Invoke();
    }

    public virtual void SetSelected(bool _isSelected)
    {
        if (_isSelected)
        {
            OnSelected?.Invoke();
        }
        else
        {
            OnUnselected?.Invoke();
        }
    }
    
    public virtual void NavigateAdjacent(bool _isNegative)
    {
        if (_isNegative) OnNavigationAdjacentNegative?.Invoke();
        else OnNavigationAdjacentPositive?.Invoke();
    }


    #endregion
}
