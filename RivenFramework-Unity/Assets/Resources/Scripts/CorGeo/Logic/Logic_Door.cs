//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Logic_Door : MonoBehaviour
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public LogicInput<bool> powerDoor = new(false);
    public UnityEvent onPowered;
    public UnityEvent onUnpowered;


    //=-----------------=
    // Private Variables
    //=-----------------=
    


    //=-----------------=
    // Reference Variables
    //=-----------------=


    //=-----------------=
    // Mono Functions
    //=-----------------=
    private void Start()
    {
        // TODO: CallOnSourceChange needs HasLogicOutputSource to fix possible null refs for unlinked logic I/Os
        // Using this 'if' statement as a quick fix for now ~Liz
        if (powerDoor.HasLogicOutputSource is false) return;
        powerDoor.CallOnSourceChanged(Toggle);
    }

    //=-----------------=
    // Internal Functions
    //=-----------------=
    private void Toggle()
    {
        if (powerDoor.Get()) onPowered.Invoke();
        else onUnpowered.Invoke();
    }


    //=-----------------=
    // External Functions
    //=-----------------=
}
