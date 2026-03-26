using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class FeKaPawn_Base : FeKaPawn
{
    private FeKaPawnActions action2 = new FeKaPawnActions();
    private InputActions.FEKAActions inputActions;
    private GI_WidgetManager widgetManager;
    [SerializeField] private GameObject DeathScreenWidget, RespawnScreenWidget, deathFX;

    private float moveInput;
    private Vector2 steerInput;
    private bool isBreaking;
    
    public override void Awake()
    {
        base.Awake();
        
        // Subscribe to events
        OnPawnDeath += () => { OnDeath(); };
        
        if (FeKaCurrentStats.controlMode != ControlMode.LocalPlayer) return;
        // Setup inputs
        inputActions = new InputActions().FEKA;
        inputActions.Enable();
    }

    public void Update()
    {
        Debug.Log($" movement: {moveInput} | " +
                  $"wheel rpm: {FeKaCurrentStats.wheels[0].wheelCollider.rpm} | " +
                  $"wheel rs: {FeKaCurrentStats.wheels[0].wheelCollider.rotationSpeed} | " +
                  $"wheel bt: {FeKaCurrentStats.wheels[0].wheelCollider.brakeTorque}" +
                  $"wheel mt: {FeKaCurrentStats.wheels[0].wheelCollider.motorTorque}");
        
        switch (FeKaCurrentStats.controlMode)
        {
            case ControlMode.LocalPlayer:
                LocalPlayerUpdate();
                break;
            case ControlMode.CPU:
                break;
            case ControlMode.NetworkPlayer:
                break;
        }
    }

    public void FixedUpdate()
    {
        switch (FeKaCurrentStats.controlMode)
        {
            case ControlMode.LocalPlayer:
                LocalPlayerFixedUpdate();
                break;
            case ControlMode.CPU:
                break;
            case ControlMode.NetworkPlayer:
                break;
        }
    }

    private void LocalPlayerUpdate()
    {
        // Pausing
        UpdatePauseMenu();

        if (isPaused || isDead)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RespawnAtLastCheckpoint();
        }
        
        // Movement
        steerInput = new Vector3(inputActions.Steer.ReadValue<Vector2>().x, 0, inputActions.Steer.ReadValue<Vector2>().y);

        isBreaking = inputActions.Handbreak.IsPressed();

        if (inputActions.Acelerate.IsPressed())
        {
            moveInput = 1;
        }
        else if (inputActions.Decelerate.IsPressed())
        {
            moveInput = -1;
        }
        else
        {
            moveInput = 0;
        }

        // Jumping
        if (inputActions.HopDedicated.IsPressed() || inputActions.TiltLeft.IsPressed() && inputActions.TiltRight.IsPressed())
        {
            action2.Jump(this);
        }

        // Item usage
        
        // CarFX
        action2.WheelEffects(this, isBreaking);
    }
    private void LocalPlayerFixedUpdate()
    {

        if (isPaused || isDead)
        {
            return;
        }
        
        // Movement
        action2.Move(this, moveInput);

        action2.Steer(this, steerInput.x);

        action2.Brake(this, moveInput, isBreaking);

        
    }
    
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

    private void ShowRespawnScreen()
    {
        if (!widgetManager)
        {
            widgetManager = GameInstance.Get<GI_WidgetManager>();
            if (!widgetManager) return;
        }
        widgetManager.AddWidget("WB_Respawning");
    }

    private void OnDeath()
    {
        Instantiate(deathFX, transform.position, transform.rotation, null);
        if (FeKaCurrentStats.stocks <= 0)
        {
            if (!widgetManager)
            {
                widgetManager = GameInstance.Get<GI_WidgetManager>();
                if (!widgetManager) return;
            }
            widgetManager.AddWidget("WB_DeathScreen");
        }
        else
        {
            FeKaCurrentStats.stocks -= 1;
            StartCoroutine(AwaitRespawn());
        }
    }

    private IEnumerator AwaitRespawn()
    {
        ShowRespawnScreen();
        yield return new WaitForSeconds(FeKaCurrentStats.respawnTime);
        var lastCheckpoint = FeKaCurrentStats.currentCheckpoint - 1;
        if (lastCheckpoint < 0) lastCheckpoint = FindObjectOfType<CheckpointTracker>().raceCheckpoints.Count;
        var respawnTransform = FindObjectOfType<CheckpointTracker>().raceCheckpoints[lastCheckpoint].transform;
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        FeKaCurrentStats.health = FeKaDefaultStats.health;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        isDead = false;
    }

    public void Init()
    {

        var respawnTransform = WorldSettings.GetPlayerStartPoint().transform;
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        isDead = false;
        isPaused = false;

        // Restore the default stats to the character
        currentStats = (FeKaPawnStats)FeKaDefaultStats.Clone();
    }

    public void RespawnAtLastCheckpoint()
    {
        var lastCheckpoint = FeKaCurrentStats.currentCheckpoint - 1;
        if (lastCheckpoint < 0) lastCheckpoint = FindObjectOfType<CheckpointTracker>().raceCheckpoints.Count;
        
        ModifyHealth(-FeKaCurrentStats.checkpointResetHealthPenalty);
        
        var respawnTransform = FindObjectOfType<CheckpointTracker>().raceCheckpoints[lastCheckpoint].transform;
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
    }
}
