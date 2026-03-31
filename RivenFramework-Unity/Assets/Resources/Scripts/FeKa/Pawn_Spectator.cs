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
using RivenFramework;
using UnityEngine;

public class Pawn_Spectator : Pawn
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public ControlMode controlMode;
    [Header("Freecam Movement")]
    [Tooltip("Base movement speed in units per second")]
    public float moveSpeed = 10f;
    [Tooltip("Multiplier applied when holding the sprint input")]
    public float sprintMultiplier = 3f;
    [Tooltip("Mouse look sensitivity")]
    public float lookSensitivity = 2f;



    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private float _yaw   = 0f;
    private float _pitch = 0f;

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private InputActions.FEKAActions inputActions;
    private Camera _camera;
    private GI_WidgetManager widgetManager;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>();
        
        // Setup inputs
        inputActions = new InputActions().FEKA;
        inputActions.Enable();
    }
 
    private void Start()
    {
        // Initialise yaw/pitch from whatever rotation we spawned at so there's no snap
        _yaw   = transform.eulerAngles.y;
        _pitch = _camera != null ? _camera.transform.localEulerAngles.x : 0f;
    }
 
    public void Update()
    {
        if (controlMode != ControlMode.LocalPlayer) return;
 
        UpdatePauseMenu();
        if (isPaused || isDead) { return; }
        HandleLook();
        HandleMove();
    }
    
    private void UpdatePauseMenu()
    {
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();


        if (widgetManager.GetExistingWidget("WB_NetPlayerlist"))
        {
            isPaused = widgetManager.GetExistingWidget("WB_Pause") || widgetManager.GetExistingWidget("WB_NetPlayerlist").gameObject.activeInHierarchy || FindObjectOfType<WB_NetChat>().isTyping;
        }
        else
        {
            isPaused = widgetManager.GetExistingWidget("WB_Pause") || FindObjectOfType<WB_NetChat>().isTyping;
        }
        
        // Pause Game
        if (inputActions.Pause.WasPressedThisFrame())
        {
            widgetManager.ToggleWidget("WB_Pause");
        }

        if (inputActions.Playerlist.IsPressed())
        {
            var widgetPlayerList = widgetManager.GetExistingWidget("WB_NetPlayerlist");
            if (widgetPlayerList == null)
                widgetManager.AddWidget("WB_NetPlayerlist");
            else
                widgetPlayerList.SetActive(true);
        }
        else
        {
            var widgetPlayerList = widgetManager.GetExistingWidget("WB_NetPlayerlist");
            if (widgetPlayerList != null)
                widgetPlayerList.SetActive(false);
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



    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void HandleLook()
    {
        // Raw mouse delta — Input.GetAxis uses Unity's smoothed mouse input
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;
 
        _yaw   += mouseX;
        _pitch -= mouseY;
        _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
 
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_camera != null)
            _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
 
    private void HandleMove()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool sprinting = Input.GetKey(KeyCode.LeftShift);
        float speed    = moveSpeed * (sprinting ? sprintMultiplier : 1f);

        Vector3 camForward = _camera != null ? _camera.transform.forward : transform.forward;
        Vector3 camRight   = _camera != null ? _camera.transform.right   : transform.right;

        Vector3 direction = camRight * h + camForward * v;
        transform.position += direction.normalized * (speed * Time.deltaTime);
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
