using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// When a pawn is no longer grounded, check their y position
/// When they are grounded again, if their velocity was over the threshold, and the y distance is lower than the threshold
/// Deal damage based on the multiplication of height
/// </summary>
public class Pawn_FallDamage : MonoBehaviour, ILoggable
{
    [field: SerializeField] public bool EnableRuntimeLogging { get; set; }
    
    [Tooltip("The pawn must be moving downwards faster than this for the damage to count")]
    [SerializeField] private float velocityThreshold = 7;
    [Tooltip("The minimum height the pawn must fall before receiving damage")]
    [SerializeField] private float fallDistanceThreshold = 16;
    [Tooltip("How much damage to apply")]
    [SerializeField] private float damageAmount = 10;
    [Tooltip("How much to multiply damage based on how many times over the distance threshold the pawn fell")]
    [SerializeField] private float damageDistanceMultiplier = 1;
    
    [Tooltip("Tracker for if the pawn is grounded")]
    private bool isPawnGrounded;
    [Tooltip("Tracker for if the pawn is in the process of falling")]
    private bool pawnIsFalling;
    [Tooltip("The Y position the pawn was at when they stopped touching ground")]
    [SerializeField] private float startingGroundHeight;
    [Tooltip("The Y position the pawn is at now that they've touch ground again")]
    [SerializeField] private float endingGroundHeight;
    [Tooltip("This is the highest the player was during the fall and is the value we compare against to get fall distance")]
    [SerializeField] private float peakFallingHeight;
    
    private float PlayerFallingHeight => linkedPawn.transform.position.y;

    private FPPawn linkedPawn;

    private void Start()
    {
        linkedPawn = GetComponent<FPPawn>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        isPawnGrounded = IsOnGround(linkedPawn);

        // Pawn has left the ground, start tracking their fall!
        if (!isPawnGrounded && !pawnIsFalling)
        {
            StartFallingEvent();
        }
        // While pawn is falling
        else if (!isPawnGrounded && pawnIsFalling)
        {
            
            // Get our current falling peak
            if (peakFallingHeight < PlayerFallingHeight) peakFallingHeight = PlayerFallingHeight;
            
            // If our y velocity changes to positive, implying we started moving upwards, reset our falling peak
            if (linkedPawn.physicsbody.velocity.y > 0) peakFallingHeight = PlayerFallingHeight;
        }
        // Pawn has hit the ground, calculate fall damage!
        else if (isPawnGrounded && pawnIsFalling)
        {
            StopFallingEvent();
        }
    }

    private void StartFallingEvent()
    {
        pawnIsFalling = true;
        startingGroundHeight = linkedPawn.transform.position.y;
    }

    private void StopFallingEvent()
    {
        pawnIsFalling = false;
        endingGroundHeight = linkedPawn.transform.position.y;
        DebugConsole.Log(this, $"Fall velocity: {linkedPawn.physicsbody.velocity.y} | Threshold: {-velocityThreshold}");
        DebugConsole.Log(this, $"Fall distance: {peakFallingHeight - endingGroundHeight} | Threshold: {fallDistanceThreshold}");

        // Pawn wasn't moving fast enough for fall damage
        if (linkedPawn.physicsbody.velocity.y > -velocityThreshold)
        {
            return;
        }
        
        // Pawn didn't fall far enough for fall damage
        if (peakFallingHeight - endingGroundHeight < fallDistanceThreshold)
        {
            return;
        }

        var totalFallDistanceMultiplier = Mathf.Abs(startingGroundHeight - endingGroundHeight) / fallDistanceThreshold;
        DebugConsole.Log(this, $"Damage Calculated: {-damageAmount*(totalFallDistanceMultiplier*damageDistanceMultiplier)}");
        
        linkedPawn.ModifyHealth(-damageAmount*(totalFallDistanceMultiplier*damageDistanceMultiplier));
    }
    
    public bool IsOnGround(FPPawn _pawn)
    {
        // Move the ground check position upwards if the pawn is crouching to account for their change in height
        Vector3 crouchingOffset = new Vector3(0,0,0);
        
        return Physics.CheckSphere(_pawn.transform.position - ((FPPawnStats)_pawn.currentStats).groundCheckOffset + crouchingOffset, ((FPPawnStats)_pawn.currentStats).groundCheckRadius, ((FPPawnStats)_pawn.currentStats).groundMask, QueryTriggerInteraction.Ignore);
    }

}
