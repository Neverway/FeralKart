using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class FeKaPawn_Base : FeKaPawn
{
    private new FeKaPawnActions action = new FeKaPawnActions();
    private InputActions.FEKAActions inputActions;
    private GI_WidgetManager widgetManager;
    [SerializeField] private GameObject DeathScreenWidget, RespawnScreenWidget, deathFX;

    private new void Awake()
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
        var steerInput = new Vector3(inputActions.Steer.ReadValue<Vector2>().x, 0,
            inputActions.Steer.ReadValue<Vector2>().y);
        float moveInput = 0;

        if (inputActions.Acelerate.IsPressed() && !inputActions.Decelerate.IsPressed())
        {
            moveInput = 1;
        }

        else if (!inputActions.Acelerate.IsPressed() && inputActions.Decelerate.IsPressed())
        {
            moveInput = -1;
        }

        else if (inputActions.Acelerate.IsPressed() && inputActions.Decelerate.IsPressed())
        {
            moveInput = 0.5f;
        }

        action.Move(this, moveInput);

        action.Steer(this, steerInput.x);

        action.Brake(this, moveInput, inputActions.Handbreak.IsPressed());

        // Jumping
        if (inputActions.HopDedicated.IsPressed() ||
            inputActions.TiltLeft.IsPressed() && inputActions.TiltRight.IsPressed())
        {
            action.Jump(this);
        }

        // Leaning
        if (inputActions.TiltLeft.IsPressed())
        {
            action.Tilt(this, FeKaCurrentStats.targetTiltAngle);
        }
        if (inputActions.TiltRight.IsPressed())
        {
            action.Tilt(this, -FeKaCurrentStats.targetTiltAngle);
        }

        // Item usage
        
        // CarFX
        action.WheelEffects(this, inputActions.Handbreak.IsPressed());
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
        var respawnTransform = WorldSettings.GetPlayerStartPoint().transform;
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
        
        var respawnTransform = FindObjectOfType<CheckpointTracker>().raceCheckpoints[lastCheckpoint].transform;
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
    }
}
