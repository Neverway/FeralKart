//===================== (Neverway 2024) Written by Connorses =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class BulbOutlet : MonoBehaviour, BulbCollisionBehaviour
{
    //=-----------------=
    // Public Variables
    //=-----------------=


    //=-----------------=
    // Private Variables
    //=-----------------=


    //=-----------------=
    // Reference Variables
    //=-----------------=

    [field:SerializeField] public Transform attachPoint { get; private set; }

    //=-----------------=
    // Mono Functions
    //=-----------------=


    //=-----------------=
    // Internal Functions
    //=-----------------=


    //=-----------------=
    // External Functions
    //=-----------------=
    public bool OnBulbCollision(Projectile_Marker bulb, RaycastHit hit)
    {
        bulb.MarkerPinAt(attachPoint.position, attachPoint.forward);
        return true;
    }
}