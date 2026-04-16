//===================== (Neverway 2026) Written by Connorses. =====================
//
// Purpose: For use in the Editor, when you want to automatically set an object's height above the ground.
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetPropHeight : MonoBehaviour
{
    //=-----------------=
    // Public Variables
    //=-----------------=

    public float height = 1f;

    //=-----------------=
    // Private Variables
    //=-----------------=


    //=-----------------=
    // Reference Variables
    //=-----------------=


    //=-----------------=
    // Mono Functions
    //=-----------------=

    //=-----------------=
    // Internal Functions
    //=-----------------=

    public void SetHeightWithRaycast ()
    {
        Physics.Raycast (transform.position, Vector3.down, out RaycastHit hit, 100);
        if (hit.collider != null)
        {
            transform.position = hit.point + (Vector3.up * height);
        }
    }
    

    //=-----------------=
    // External Functions
    //=-----------------=
}