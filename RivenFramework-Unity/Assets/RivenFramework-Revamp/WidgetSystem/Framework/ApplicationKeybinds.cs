//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose: Stores the lists of input actions to button hint sprites.
//          Also holds functions for rebinding controls
// Notes: Originally this was supposed to also handel saving and loading the controls
//        to a file like controls.config, but I guess unity already saves them somewhere?
//
//=============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ApplicationKeybinds : MonoBehaviour
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public InputActionAsset inputActionAsset;
    public List<KeybindList> keyboardList;
    public List<KeybindList> controllerList;


    //=-----------------=
    // Private Variables
    //=-----------------=
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    public int currentDeviceID = 1;


    //=-----------------=
    // Reference Variables
    //=-----------------=
    private GI_WidgetManager widgetManager;


    //=-----------------=
    // Mono Functions
    //=-----------------=
    private void Update()
    {
        GetCurrentInputDevice();
        if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
    }


    //=-----------------=
    // Internal Functions
    //=-----------------=


    //=-----------------=
    // External Functions
    //=-----------------=
    [Tooltip("1-Keyboard, 2-Controller")]
    public void GetCurrentInputDevice()
    {
        var lastDevice = InputSystem.devices;

        // Check the last active device
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            currentDeviceID = 1; // Keyboard
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            currentDeviceID = 1; // Mouse (treated the same as Keyboard)
            return;
        }

        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            currentDeviceID = 2; // Gamepad
            return;
        }

        if (InputSystem.devices.Count > 0)
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is TrackedDevice)
                {
                    currentDeviceID = 3; // Motion Controller / VR Controller
                    return;
                }
            }
        }
    }

    public Sprite GetKeybindImage(int _deviceID, string _keybindID)
    {
        Sprite _image = null;
        switch (_deviceID)
        {
            case 0:

                break;
            case 1:
                foreach (var keybind in keyboardList)
                {
                    if (_keybindID.ToLower() == keybind.keybindID.ToLower())
                    {
                        return keybind.keybindSprite;
                    }
                }

                break;
            case 2:
                foreach (var keybind in controllerList)
                {
                    if (_keybindID.ToLower() == keybind.keybindID.ToLower())
                    {
                        return keybind.keybindSprite;
                    }
                }

                break;
        }

        Debug.LogWarning($"{_deviceID} {_keybindID}");
        return _image;
    }

    private bool isOperationCompleted; // Class-level field for tracking operation completion.

    private IEnumerator RebindTimeout(float _timeLimit)
    {

        yield return new WaitForSeconds(_timeLimit);
        if (rebindOperation == null || isOperationCompleted)
            yield break; // If already completed or canceled, exit coroutine.

        // Timeout occurred
        if (!isOperationCompleted)
        {
            Debug.LogWarning("Rebind operation timed out.");
            Destroy(widgetManager.GetExistingWidget("WB_Settings_Controls_Rebinding"));
            rebindOperation?.Cancel();
            rebindOperation?.Dispose();
            rebindOperation = null;
        }
    }

    public void SetBinding(string _actionMap, string _action, bool _isComposite)
    {
        Debug.Log(
            $"[{this.name}] Executing function 'SetBinding(actionMap={_actionMap}, action={_action}, isComposite={_isComposite})'");

        rebindOperation?.Cancel();
        isOperationCompleted = false; // Reset completion status.

        void CleanUp()
        {
            rebindOperation?.Dispose();
            rebindOperation = null;
        }

        StartCoroutine(RebindTimeout(2f)); // Start timeout check for 5 seconds.

        if (!_isComposite)
        {
            var action = inputActionAsset.FindActionMap(_actionMap).FindAction(_action);
            print($"_AM [{_actionMap}] _A[{_action}] A[{action}]");
            rebindOperation = action.PerformInteractiveRebinding().Start()
                .OnCancel(something => { CleanUp(); })
                .OnComplete(something =>
                {
                    isOperationCompleted = true; // Set flag to true if operation completed successfully
                    StopAllCoroutines();
                    Destroy(widgetManager.GetExistingWidget("WB_Settings_Controls_Rebinding"));
                    // Save
                    string device = string.Empty;
                    string key = string.Empty;
                    action.GetBindingDisplayString(0, out device, out key);
                    print("OUTPUT " + "<" + device + ">/" + key);
                    action.ChangeBinding(0).WithPath($"<{device}>/{key}");
                    CleanUp();
                });
        }
        else
        {
            string input = _action;
            string[] parts = input.Split(' ');
            var parsedAction = parts[0];
            var parsedPart = string.Join(" ", parts, 1, parts.Length - 1);
            var action = inputActionAsset.FindActionMap(_actionMap).FindAction(parsedAction);
            print($"_AM [{_actionMap}] _A[{_action}] A[{action}] PA[{parsedAction}]");

            int bindingIndex = -1;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].isPartOfComposite &&
                    string.Equals(action.bindings[i].name, parsedPart, StringComparison.OrdinalIgnoreCase) &&
                    action.bindings[i].groups.Contains("Keyboard"))
                {
                    bindingIndex = i;
                    break;
                }
            }

            if (bindingIndex == -1)
            {
                Debug.LogWarning("No binding found for " + parsedAction);
                return;
            }
            
            rebindOperation = action.PerformInteractiveRebinding(bindingIndex).Start()
                .OnCancel(something => { CleanUp(); })
                .OnComplete(something =>
                {
                    isOperationCompleted = true; // Set flag to true if operation completed successfully
                    StopAllCoroutines();
                    Destroy(widgetManager.GetExistingWidget("WB_Settings_Controls_Rebinding"));
                    // Save
                    string device = string.Empty;
                    string key = string.Empty;
                    action.GetBindingDisplayString(bindingIndex, out device, out key);
                    print("OUTPUT " + "<" + device + ">/" + key);
                    action.ChangeBinding(bindingIndex).WithPath($"<{device}>/{key}");
                    CleanUp();
                });
        }
    }



}

[Serializable]
public class KeybindList
{
    public string keybindID;
    public Sprite keybindSprite;
}