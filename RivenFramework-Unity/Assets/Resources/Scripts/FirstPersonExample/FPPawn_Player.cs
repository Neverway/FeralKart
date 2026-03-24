//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
// 
// Contributors: 
//  Connorses, Errynei, Soulex
//
//====================================================================================================================//

using System;
using RivenFramework;
using UnityEngine;

public class FPPawn_Player : FPPawn
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    /*----------------------------------------------------------------------------------------------------------------*/
    
    
    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    /*----------------------------------------------------------------------------------------------------------------*/

    
    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    /*----------------------------------------------------------------------------------------------------------------*/
    private Vector3 moveDirection;
    private Vector2 lookRotation;
    
    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    /*----------------------------------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    private new FPPawnActions action = new FPPawnActions();
    private InputActions.FirstPersonActions inputActions;
    [SerializeField] private GameObject DeathScreenWidget;
    [SerializeField] private Pawn_Inventory playerInventory;
    private ApplicationSettings applicationSettings;
    private RiftManager riftManager;
    
    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    /*----------------------------------------------------------------------------------------------------------------*/
    private void UpdatePauseMenu()
    {
        if (!widgetManager)
        {
            widgetManager = GameInstance.Get<GI_WidgetManager>();
            if (!widgetManager) return;
        }
        isPaused = widgetManager.GetExistingWidget("WB_Pause");
        
        // Pause Game
        if (inputActions.Pause.WasPressedThisFrame())
        {
            widgetManager.ToggleWidget("WB_Pause");
        }
        
        // Lock mouse when unpaused, unlock when paused
        if (isPaused)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public new void Awake()
    {
        base.Awake();
        
        // Subscribe to events
        OnPawnDeath += () => { OnDeath(); };
        
        // Setup inputs
        inputActions = new InputActions().FirstPerson;
        inputActions.Enable();
        
        // Enable the view camera
        action.EnableViewCamera(this, true);
    }

    [Todo("Are you able to remove GetComponentInChildren call on Update? ~erry", Owner = "liz")]
    public void Update()
    {
        // Pausing
        UpdatePauseMenu();
        
        
        if (isPaused || isDead) return;
        UpdateMovement();
        UpdateRotation();
        
        // Kill bind
        if (Input.GetKeyDown(KeyCode.Delete)) Kill();
        
        // Jumping
        if (inputActions.Jump.WasPressedThisFrame()) action.Jump(this);
        
        // Crouching
        if (inputActions.Crouch.IsPressed())
        {
            action.Crouch(this, true);
        }
        else
        {
            action.Crouch(this, false);
        }
        
        // Interact 
        if (inputActions.Interact.WasPressedThisFrame())
        {
            if (physObjectAttachmentPoint.attachedObject)
            {
                action.DropPhysProp(this);
            }
            else
            {
                action.Interact(this, interactionPrefab, viewPoint.transform);
            }
        }
        
        // Switch item
        if (inputActions.ItemSwapNext.WasPressedThisFrame()) action.ItemSwapNext(this);
        if (inputActions.ItemSwapPrevious.WasPressedThisFrame()) action.ItemSwapPrevious(this);
        
        // Use Item
        if (!playerInventory)
        {
            throw new Exception("playerInventory reference has not been set in the inspector! The inventory should be on one of the child objects under the player prefab, please manually assign it!");
        }
        if (inputActions.ItemAction1.WasPressedThisFrame())
        {
            // Throw held object, or Item Use Action 0
            if (physObjectAttachmentPoint.attachedObject)
            {
                action.ThrowPhysProp(this);
            }
            else
            {
                action.ItemUseAction(playerInventory, 0);
            }
        }
        if (inputActions.ItemAction2.WasPressedThisFrame()) action.ItemUseAction(playerInventory, 1);
        if (inputActions.ItemAction3.WasPressedThisFrame()) action.ItemUseAction(playerInventory, 2);
        if (inputActions.ItemAction1.WasReleasedThisFrame()) action.ItemUseAction(playerInventory, 0, "release");
        if (inputActions.ItemAction2.WasReleasedThisFrame()) action.ItemUseAction(playerInventory, 1, "release");
        if (inputActions.ItemAction3.WasReleasedThisFrame()) action.ItemUseAction(playerInventory, 2, "release");
    }

    public void FixedUpdate()
    {
        if (isPaused || isDead) return;
        ApplyMovement();
        ApplyRotation();
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /*----------------------------------------------------------------------------------------------------------------*/

    
    private void UpdateMovement()
    {
        moveDirection = new Vector3(inputActions.Move.ReadValue<Vector2>().x, 0, inputActions.Move.ReadValue<Vector2>().y);
    }
    private void ApplyMovement()
    {
        action.Move(this, moveDirection);
    }

    private void UpdateRotation()
    {
        if (applicationSettings == null) applicationSettings = GameInstance.Get<ApplicationSettings>();
        
        // Get the look speed
        float horizontalLookSpeed = applicationSettings.currentSettingsData.horizontalLookSpeed;
        float verticalLookSpeed = applicationSettings.currentSettingsData.verticalLookSpeed;
        
        // Separate multipliers for mouse and joystick
        float mouseMultiplier = applicationSettings.currentSettingsData.mouseLookSensitivity;
        float joystickMultiplier = applicationSettings.currentSettingsData.joystickLookSensitivity;

        // Determine the input method (mouse or joystick)
        bool isUsingMouse = false;
        if (inputActions.LookAxis.IsInProgress())
        {
            if (inputActions.LookAxis.activeControl.device.name == "Mouse")
            {
                isUsingMouse = true;
            }
        }

        // Apply the appropriate multiplier
        var multiplier = isUsingMouse ? mouseMultiplier : joystickMultiplier;
        
        // Store the rotation values
        lookRotation.x -= inputActions.LookAxis.ReadValue<Vector2>().y * (10 * verticalLookSpeed) * (multiplier/10);
        lookRotation.y += inputActions.LookAxis.ReadValue<Vector2>().x * (10 * horizontalLookSpeed) * (multiplier/10);
        lookRotation.x = Mathf.Clamp(lookRotation.x, -90f, 90f);
    }
    private void ApplyRotation()
    {
        action.FaceTowardsDirection(this, viewPoint, lookRotation);
    }


    private void OnDeath()
    {
        // Remove any rifts
        //riftManager = GameInstance.Get<RiftManager>();
        //riftManager.DestroyRiftExternal();
        
        // Drop held props
        if (physObjectAttachmentPoint)
        {
            if (physObjectAttachmentPoint.attachedObject)
            {
                if (physObjectAttachmentPoint.attachedObject.TryGetComponent(out Object_PhysPickup physPickup))
                {
                    physPickup.ToggleHeld();
                }
            }
        }

        // Remove the HUD
        Destroy(widgetManager.GetExistingWidget("WB_HUD"));
        // Add the respawn HUD
        widgetManager.AddWidget(DeathScreenWidget);

        // Play the death animation
        if (TryGetComponent(out Animator animator)) animator.Play("Death");
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /*----------------------------------------------------------------------------------------------------------------*/
    public bool IsCrouched()
    {
        return action.isCrouching;
    }


    #endregion
}
