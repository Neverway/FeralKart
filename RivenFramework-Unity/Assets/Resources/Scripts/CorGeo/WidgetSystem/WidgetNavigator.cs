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
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Uses button inputs to navigate through selectable elements on a widget
/// </summary>
[Todo_AddComments("Ported from AuHo, Needs tidying")]
public class WidgetNavigator : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("If enabled, this menu is currently able to be navigated using button inputs")]
    public bool activelyNavigating;
    [Tooltip("Which directional inputs to use to navigate the menu")]
    [SerializeField] private NavigationMode navigationMode;
    private enum NavigationMode { Vertical, Horizontal }
    [Tooltip("If enabled, stops pressing right from activating the menu item (for the inspect menu mainly)")]
    [SerializeField] private bool pressingRightDoesNotTryNavigate = true;
    [Tooltip("If enabled, reaching either end of the button list will wrap back around when navigating")]
    [SerializeField] private bool enableWrapping;
    [Tooltip("If enabled, all elements will appear unselected when this menu is not set as activelyNavigating")]
    [SerializeField] private bool hideIndicatorOnInactive;

    [SerializeField] private bool resetSelectedOnInactive;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("The current position in the menu")]
    public int currentIndex = 0;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private bool initialized;
    [Tooltip("If this is enabled, navigation inputs are disabled")]
    private bool initialInputDelay;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public List<WidgetSelectable> selectableElements;
    public UnityEvent OnNavigatable, OnBack;


    #endregion


    #region=======================================( Functions )======================================================= //
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/

    private void Update()
    {/*
        if (!initialized && activelyNavigating)
        {
            initialized = true;
            // Initialize the element states
            SetElementStates();
        }
        if (activelyNavigating)
        {
            GetIndexingInputs();
            //selectableElements[currentIndex].SetSelected(true); 
            //Debug.Log("This is the SpaceCat Lore"); // This line is required here to fix a bug caused by the hideIndicatorOnInactive statement below
        }
        /*
        else if (hideIndicatorOnInactive)
        {
            selectableElements[currentIndex].SetSelected(false);
        }*/
    }
    private void OnEnable()
    {/*
        //This just gives the widgetnavigator a change
        if (selectableElements.IsIndexInRange(currentIndex) && selectableElements[currentIndex] is WidgetSelectable_Animator)
            selectableElements[currentIndex].SetSelected(true);*/
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Update the appearances of the elements
    /// </summary>
    private void SetElementStates()
    {/*
        foreach (var selectable in selectableElements)
        {
            selectable.SetSelected(false);
        }
        if (selectableElements.Count == 0) return;
        selectableElements[currentIndex].SetSelected(true);*/
    }

    /// <summary>
    /// Get the button inputs for navigating through the index
    /// </summary>
    private void GetIndexingInputs()
    {/*
        if (initialInputDelay) return;
        switch (navigationMode)
        {
            case NavigationMode.Vertical:
                CheckMove(GameInstance.Inputs.MoveUp, -1);
                CheckMove(GameInstance.Inputs.MoveDown, 1);
                if (GameInstance.Inputs.MoveLeft.WasPressedThisFrame()) AdjacentNavigation(true);
                if (GameInstance.Inputs.MoveRight.WasPressedThisFrame()) AdjacentNavigation(false);
                break;
            case NavigationMode.Horizontal:
                CheckMove(GameInstance.Inputs.MoveLeft, -1);
                CheckMove(GameInstance.Inputs.MoveRight, 1);
                if (GameInstance.Inputs.MoveUp.WasPressedThisFrame()) AdjacentNavigation(true);
                if (GameInstance.Inputs.MoveDown.WasPressedThisFrame()) AdjacentNavigation(false);
                break;
        }

        if (GameInstance.Inputs.Interact.WasPressedThisFrame() || 
            ((!pressingRightDoesNotTryNavigate) && GameInstance.Inputs.MoveRight.WasPressedThisFrame()))
        {
            if (selectableElements.Count != 0) selectableElements[currentIndex].Interact();
        }

        if (GameInstance.Inputs.Action.WasPressedThisFrame() || 
            (!pressingRightDoesNotTryNavigate && GameInstance.Inputs.MoveLeft.WasPressedThisFrame()))
        {
            OnBack.Invoke();
        }*/
    }    
    
    private void CheckMove(InputAction inputAction, int incrementIndex)
    {/*
        if (inputAction.WasPressedThisFrame())
        {
            if (selectableElements.Count == 0) return;
            if (enableWrapping)
            {
                currentIndex += incrementIndex;
                if (currentIndex < 0) currentIndex = selectableElements.Count-1;
                if (currentIndex >= selectableElements.Count) currentIndex = 0;
            }
            else
            {
                currentIndex += incrementIndex;
                currentIndex = Mathf.Clamp(currentIndex, 0, selectableElements.Count-1);
            }
            
            // Update the selectable elements
            SetElementStates();
        }*/
    }

    private void AdjacentNavigation(bool _isNegative)
    {/*
        selectableElements[currentIndex].NavigateAdjacent(_isNegative);*/
    }

    private IEnumerator StartInitialInputDelay()
    {
        initialInputDelay = true;
        yield return new WaitForSeconds(0.2f);
        initialInputDelay = false;
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void SetIsNavigating(bool _isNavigating)
    {/*
        //print($"Navigation set to {_isNavigating} on {gameObject.name}");
        activelyNavigating = _isNavigating;
        if (_isNavigating)
        {
            GameInstance.SendCoroutine(StartInitialInputDelay());
            OnNavigatable.Invoke();
            if (hideIndicatorOnInactive && selectableElements.Count != 0)
            {
                selectableElements[currentIndex].SetSelected(true);
            }
        }
        else
        {
            if (hideIndicatorOnInactive && selectableElements.Count != 0)
            {
                selectableElements[currentIndex].SetSelected(false);
            }

            if (resetSelectedOnInactive)
            {
                ResetSelectedIndex();
            }
        }*/
    }

    public void ResetSelectedIndex()
    {
        currentIndex = 0;
        if (selectableElements.Count != 0)
        {
            SetElementStates();
        }
    }


    #endregion
}
