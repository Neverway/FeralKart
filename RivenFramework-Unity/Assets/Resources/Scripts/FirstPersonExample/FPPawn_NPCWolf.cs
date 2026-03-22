//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPPawn_NPCWolf : FPPawn
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public bool logBehaviour;


    //=-----------------=
    // Private Variables
    //=-----------------=
    private Vector3 moveDirection;
    private bool isWandering; // Used to avoid constant overwrites of the moveFor function
    private FPPawn closestAlly;
    private FPPawn targetEnemy;
    


    //=-----------------=
    // Reference Variables
    //=-----------------=
    private new FPPawnActions action = new FPPawnActions();


    //=-----------------=
    // Mono Functions
    //=-----------------=
    public new void Awake()
    {
        base.Awake();
        // Get references
        physicsbody = GetComponent<Rigidbody>();
        viewPoint = transform.Find("ViewPoint");
    }

    public void FixedUpdate()
    {
        if (isPaused || isDead) return;
        action.Look(this, ((FPPawnStats)currentStats).lookRange);
        closestAlly = action.GetClosest(this, visibleAllies);
        targetEnemy = action.GetClosest(this, visibleHostiles);
        
        // Get my starting stats
        var currentCourage = ((FPPawnStats)currentStats).courage;
        var collectiveCourage = currentCourage + action.GetCollectiveAllyCourage(this, visibleAllies);
        
        // If I see an enemy
        if (visibleHostiles.Count > 0)
        {
            // I am low on health
            if (((FPPawnStats)currentStats).health <= 25)
            {
                // Courage drops
                currentCourage = 0;
                collectiveCourage = currentCourage + action.GetCollectiveAllyCourage(this, visibleAllies);
            }
            // There is an ally nearby
            if (visibleAllies.Count > 0)
            {
                // The Ally is within a comfortable range
                if (Vector3.Distance(closestAlly.transform.position, this.transform.position) <= ((FPPawnStats)currentStats).comfortableAllyDistance)
                {
                    // Our collective courage is greater or equal to our enemy courage
                    if (collectiveCourage >= ((FPPawnStats)targetEnemy.currentStats).courage)
                    {
                        // If in range
                        if (Vector3.Distance(this.transform.position, targetEnemy.transform.position) <= 3)
                        {
                            SetState("attacking");
                        }
                        else
                        {
                            SetState("approaching");
                        }
                    }
                    // Else
                    else
                    {
                        SetState("fleeing");
                    }
                }
                // The ally is NOT withing a comfortable range
                else
                {
                    SetState("regrouping");
                }
            }
            // There is NOT an ally nearby
            else
            {
                // My courage is greater or equal to enemy courage
                if (currentCourage >= ((FPPawnStats)targetEnemy.currentStats).courage)
                {
                    // If in range
                    if (Vector3.Distance(this.transform.position, targetEnemy.transform.position) <= 3)
                    {
                        SetState("attacking");
                    }
                    else
                    {
                        SetState("approaching");
                    }
                }
                // Else
                else
                {
                    SetState("fleeing");
                }
                // I am cornered
                    // Attack enemy
            }
        }
        else
        {
            if (!isWandering)
            {
                SetState("idle");
                StartCoroutine(AttemptWander());
            }
            else
            {
                SetState("wandering");
            }
        }
        
        action.Move(this, moveDirection, FPCurrentStats.movementSpeed);
    }

    
    //=-----------------=
    // Internal Functions
    //=-----------------=-=
    private IEnumerator AttemptWander()
    {
        isWandering = true;
        
        // Assign random duration to wander for
        float wanderDuration = Random.Range(1, 5);
        // 20% chance to wander
        bool willMove = Random.Range(0, 100) <= 15;
        // Assign random direction to wander
        action.FaceTowardsDirection(this, viewPoint, new Vector2(0, Random.Range(0, 360)));
        while (wanderDuration > 0)
        {
            if (willMove)
            {
                moveDirection = new Vector3(0, 0, 1);
            }
            wanderDuration -= 1f;
            yield return new WaitForSeconds(1);
        }
        isWandering = false;
    }

    private void SetState(string _state)
    {
        switch (_state)
        {
            case "idle":
                if (logBehaviour) print($"State: idle");
                // Halt movement
                moveDirection = new Vector3(0, 0, 0);
                // Uncrouch
                action.Crouch(this, false);
                return;
            case "wandering":
                if (logBehaviour) print($"State: wandering");
                // Uncrouch
                action.Crouch(this, false);
                return;
            case "regrouping":
                if (logBehaviour) print($"State: regrouping");
                // Look at them
                action.FaceTowardsPosition(this, viewPoint, closestAlly.transform.position, 5);
                // Back away
                moveDirection = new Vector3(0, 0, 1);
                // Uncrouch
                action.Crouch(this, false);
                return;
            case "attacking":
                if (logBehaviour) print($"State: attacking");
                // Look at them
                action.FaceTowardsPosition(this, viewPoint, targetEnemy.transform.position, 5);
                // Attack enemy
                action.Jump(this);
                // Uncrouch
                action.Crouch(this, false);
                return;
            case "approaching":
                if (logBehaviour) print($"State: approaching");
                // Look at them
                action.FaceTowardsPosition(this, viewPoint, targetEnemy.transform.position, 5);
                // Approach
                moveDirection = new Vector3(0, 0, 1);
                // Crouch
                action.Crouch(this, true);
                return;
            case "fleeing":
                if (logBehaviour) print($"State: fleeing");
                // Look at the enemy
                action.FaceTowardsPosition(this, viewPoint, targetEnemy.transform.position, 5);
                // Crouch
                action.Crouch(this, true);
                // Back away
                moveDirection = new Vector3(0, 0, -1);
                return;
        }
            
    }


    //=-----------------=
    // External Functions
    //=-----------------=
}
