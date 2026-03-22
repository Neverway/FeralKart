//===================== (Neverway 2024) Written by Connorses =====================
//
// Purpose: Provides crush detection for pawns
// Notes: can be repurposed for other objects
//
//=============================================================================

using System;
using UnityEngine;
using UnityEngine.Events;
using Neverway.Framework.PawnManagement;
using Neverway.Framework;

public class CrushDetector : MonoBehaviour
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public float overlapPointTop, overlapPointBottom, bottomCrouchOffset, overlapRadius;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private float crushDamageAmount = 40f;


    //=-----------------=
    // Private Variables
    //=-----------------=
    private float currentCrouchOffset;

    //=-----------------=
    // Reference Variables
    //=-----------------=
    public UnityEvent onCrushed {  get; private set; } = new UnityEvent();
    private Pawn pawn;


    //=-----------------=
    // Mono Functions
    //=-----------------=

    private void Start ()
    {
        pawn = GetComponent<Pawn>();
    }


    private void FixedUpdate ()
    {
        //skip detection if objects are frozen to calculate mesh colliders. (avoids false positives)
        //if (Alt_Item_Geodesic_Utility_GeoGun.delayRiftCollapse)
        //{
        //    return;
        //}

        if (pawn)
        {
            var playerPawn = pawn as FPPawn_Player;
            if (playerPawn)
            {
                if (playerPawn.IsCrouched())
                {
                    currentCrouchOffset = bottomCrouchOffset;
                }
                else
                {
                    currentCrouchOffset = 0;
                }
            }
        }

        if (CheckForOverlaps ())
        {
            if (pawn)
            {
                onCrushed?.Invoke ();
                RiftManager.expandDueToCrush = true;
                pawn.ModifyHealth (-crushDamageAmount);
            }
        }
    }

    private void OnDrawGizmos()
    {   
        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + transform.up * overlapPointTop, overlapRadius);
        Gizmos.DrawWireSphere(transform.position - transform.up * overlapPointBottom, overlapRadius);
        Gizmos.color = new Color(1, 0.5f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position - transform.up * (overlapPointBottom - bottomCrouchOffset), overlapRadius);
    }

    //=-----------------=
    // Internal Functions
    //=-----------------=

    private bool CheckForOverlaps ()
    {
        Collider[] colliders = Physics.OverlapCapsule (transform.position + transform.up * overlapPointTop, transform.position - transform.up * (overlapPointBottom - currentCrouchOffset), overlapRadius, layerMask);
        foreach (Collider collider in colliders)
        {
            // Ignore self
            if (collider.gameObject == gameObject) { continue; }
            // Ignore triggers
            if (collider.isTrigger) { continue; }
            // Ignore rigidbody that is not kinematic
            //if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic) { continue; }
            return true;
        }
        return false;
    }

    //=-----------------=
    // External Functions
    //=-----------------=
}