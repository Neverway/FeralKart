//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M., Connorses, Errynei, Soulex
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections;
using ErryLib.MonoTasks;
using RivenFramework;
using UnityEngine;

/// <summary>
/// Handles what functions are actually called when a rift is in each state
/// </summary>
[Serializable]
public class RiftManager_StateHandler : ILoggable
{
    /// <summary>
    /// Class constructor
    /// </summary>
    public RiftManager_StateHandler(RiftManager riftManager)
    {
        this.riftManager = riftManager;
        EnableRuntimeLogging = riftManager.EnableRuntimeLogging;
    }
    
    
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public bool EnableRuntimeLogging { get; set; }


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("Tells things what state the rift is changing from")]
    [SerializeReference, Polymorphic] public N_RiftState previousState = new RiftState_None();
    [Tooltip("The current rift state  :O")]
    [SerializeReference, Polymorphic] public N_RiftState currentState = new RiftState_None();


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Link to parent class for logging")]
    public RiftManager riftManager;
    
    [Tooltip("This event allows scripts to respond to any changes in the RiftState, such as the animated plane visuals or the rift audio effects")]
    public delegate void StateChanged ();
    public static event StateChanged OnStateChanged;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Update()
    {
        currentState.OnUpdate();
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public T SetState<T>() where T: N_RiftState, new ()
    {
        T _riftState = new ()
        {
            handler = this
        };

        if (currentState == _riftState)
        {
            return _riftState;
        }
        previousState = currentState;
        currentState = _riftState;
        
        previousState.OnStateExit();
        currentState.OnStateEnter();

        OnStateChanged?.Invoke ();
        return _riftState;
    }
    
    public bool IsState<T>() where T: N_RiftState => currentState is T;


    #endregion
}

public abstract class N_RiftState
{
    [NonSerialized] public RiftManager_StateHandler handler;
    public RiftManager _RiftManager => handler?.riftManager;
    
    public virtual void OnStateEnter()
    {
        
    }

    public virtual void OnUpdate()
    {
        
    }

    public virtual void OnStateExit()
    {
        
    }
}

/// <summary>
/// There is no rift
/// </summary>
public class RiftState_None : N_RiftState
{
    public override void OnStateEnter()
    {
    }

    public override void OnUpdate()
    {
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift is being created
/// </summary>
public class RiftState_Preview : N_RiftState
{
    public override void OnStateEnter()
    {
    }

    public override void OnUpdate()
    {
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift is not moving
/// </summary>
public class RiftState_Idle : N_RiftState
{
    public override void OnStateEnter()
    {
        _RiftManager.currentRiftMoveSpeed = _RiftManager.minRiftSpeed;
    }

    public override void OnUpdate()
    {
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift is collapsing inwards
/// </summary>
public class RiftState_Collapsing : N_RiftState
{
    public override void OnStateEnter()
    {
    }

    public override void OnUpdate()
    {
        _RiftManager.MoveRiftByDistance (-_RiftManager.currentRiftMoveSpeed * Time.deltaTime);
        //_RiftManager.AccelerateRift ();
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift is expanding outwards
/// </summary>
public class RiftState_Expanding : N_RiftState
{
    public override void OnStateEnter()
    {
    }

    public override void OnUpdate()
    {
        _RiftManager.MoveRiftByDistance (_RiftManager.currentRiftMoveSpeed * Time.deltaTime);
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift is fully compressed and nullspace is hidden
/// </summary>
public class RiftState_Closed : N_RiftState
{
    public override void OnStateEnter()
    {
        // Disable null-space objects
        handler.riftManager.spaceController.DisableCollapsedObject();
    }

    public override void OnUpdate()
    {
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift just left a fully compressed state and nullspace is unhidden
/// </summary>
public class RiftState_Opened : N_RiftState
{
    public override void OnStateEnter()
    {
        // Enable null-space objects
        handler.riftManager.spaceController.EnableCollapsedObject();
        handler.SetState<RiftState_Expanding>();
    }

    public override void OnUpdate()
    {
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift is snapping outwards to free a crushed entity
/// </summary>
public class RiftState_ExpandingFromCrush : N_RiftState
{
    public override void OnStateEnter()
    {
        // Start a timer until we leave the state
        test();
    }

    private async void test()
    {
        // Expand for 0.15 seconds
        await For.Seconds(0.15f);
        RiftManager.expandDueToCrush = false;
        // Switch to idle state
        handler.SetState<RiftState_Idle>();
    }

    public override void OnUpdate()
    {
        // Expand the rift
        _RiftManager.MoveRiftByDistance (_RiftManager.maxRiftSpeed * Time.deltaTime);
    }
}

/// <summary>
/// The rift is being destroyed and will smoothly return back to no distortion
/// </summary>
public class RiftState_DestroyRestoring : N_RiftState
{
    public override void OnStateEnter()
    {
    }

    public override void OnUpdate()
    {
        if (RiftManager.currentRiftPercent < 1) handler.riftManager.MoveRiftByDistance(((handler.riftManager.maxRiftSpeed*2) * Time.deltaTime));
        else if (RiftManager.currentRiftPercent > 1) handler.riftManager.MoveRiftByDistance((-(handler.riftManager.maxRiftSpeed*2) * Time.deltaTime));

        if (RiftManager.currentRiftPercent < 1.2 && RiftManager.currentRiftPercent > 0.8f)
        {
            handler.riftManager.stateHandler.SetState<RiftState_Destroy>();
        }
    }

    public override void OnStateExit()
    {
    }
}

/// <summary>
/// The rift has finished returning to it's 1 point and will now self destruct
/// </summary>
public class RiftState_Destroy : N_RiftState
{
    public override void OnStateEnter()
    {
        handler.riftManager.SetRiftPercentage(1);
        handler.riftManager.geometryHandler.SetRiftPlanesVisible(false);
        handler.riftManager.geometryHandler.RestoreCutGeometry();
        handler.riftManager.spaceController.RemoveObjectsFromSpaceContainers();
        handler.riftManager.actorHandler.RestoreActors();
        handler.riftManager.currentRiftMoveSpeed = handler.riftManager.minRiftSpeed;
        handler.riftManager.stateHandler.SetState<RiftState_None>();
    }

    public override void OnUpdate()
    {
    }

    public override void OnStateExit()
    {
    }
}
