using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Logic_Elevator : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3;
    
    [Tooltip("When powered, the elevator is allowed to move")]
    public LogicInput<bool> EnableElevator = new(false);
    [Tooltip("When powered, the elevator will start moving")]
    public LogicInput<bool> StartMovement = new(false);
    [Tooltip("When powered, the elevator will stop moving")]
    public LogicInput<bool> StopMovement = new(false);
    [Tooltip("When powered, the elevator doors will open")]
    public LogicInput<bool> OpenDoor = new(false);
    [Tooltip("Powered when the elevator is on its target floor")]
    public LogicOutput<bool> OnFloorReached = new(false);
    [Tooltip("What 'Floor' the elevator is moving towards, (The floor is the index in floorTargets)")]
    public int targetFloor;
    public List<Transform> elevatorFloorTargets;
    
    private bool elevatorIsMoving;
    [SerializeField] private Animator animator;

    
    private void Start()
    {
        if (StartMovement.HasLogicOutputSource) StartMovement.CallOnSourceChanged(FuncStartMovement);
        if (StopMovement.HasLogicOutputSource) StopMovement.CallOnSourceChanged(FuncStopMovement);
        if (OpenDoor.HasLogicOutputSource) OpenDoor.CallOnSourceChanged(FuncOpenDoor);
    }

    private IEnumerator MoveElevator()
    {
        elevatorIsMoving = true;
        OnFloorReached.Set(false);
        Vector3 targetPos = elevatorFloorTargets[targetFloor].position;

        while (transform.position != targetPos)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return new WaitForEndOfFrame();
        }

        elevatorIsMoving = false;
        OnFloorReached.Set(true);
        targetFloor++;

        if (targetFloor == elevatorFloorTargets.Count)
        {
            targetFloor = 0;
        }
    }

    private void FuncStartMovement()
    {
        if (EnableElevator.HasLogicOutputSource) if (EnableElevator.Get() == false) return;
        if (elevatorIsMoving) return;
        StartCoroutine(MoveElevator());
    }

    private void FuncStopMovement()
    {
        if (EnableElevator.HasLogicOutputSource) if (EnableElevator.Get() == false) return;
        elevatorIsMoving = false;
        StopAllCoroutines();
    }

    private void FuncOpenDoor()
    {
        animator.SetBool("Powered", OpenDoor.Get());
    }
}
