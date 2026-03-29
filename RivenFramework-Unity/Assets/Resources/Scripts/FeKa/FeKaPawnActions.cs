//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RivenFramework;

public class FeKaPawnActions : PawnActions
{
    //=-----------------=
    // Public Variables
    //=-----------------=


    //=-----------------=
    // Private Variables
    //=-----------------=
    private RaycastHit slopeHit;
    public bool isCrouching;
    private GameObject viewCamera;


    //=-----------------=
    // Reference Variables
    //=-----------------=


    //=-----------------=
    // Mono Functions
    //=-----------------=
    

    //=-----------------=
    // Internal Functions
    //=-----------------=


    //=-----------------=
    // External Functions
    //=-----------------=
    public void Move(FeKaPawn _pawn, float _moveInput)
    {
        var rb = _pawn.physicsbody;
        var currentSpeed = rb.velocity.magnitude;
        var maxSpeed = _pawn.FeKaCurrentStats.maxSpeed;
        
        //var speedFactor = Mathf.Clamp01(1f - (currentSpeed / maxSpeed));
        var speedFactor = Mathf.Pow(1f - Mathf.Clamp01(currentSpeed / maxSpeed), 1.5f);
        
        foreach(var wheel in _pawn.FeKaCurrentStats.wheels)
        {
            if (wheel.axel == Axel.Rear)
            {
                //var ct = _moveInput * _pawn.FeKaCurrentStats.accelSpeed * _pawn.FeKaCurrentStats.maxAcceleration * Time.deltaTime;
                var ct = _moveInput * _pawn.FeKaCurrentStats.moveTorque * speedFactor;
                wheel.wheelCollider.motorTorque = ct;
            }
        }
    }
 
    public void Steer(FeKaPawn _pawn, float _steerInput)
    {
        var rb = _pawn.physicsbody;
        var currentSpeed = rb.velocity.magnitude;
        var maxSpeed = _pawn.FeKaCurrentStats.maxSpeed;
        float lerpSpeed = 8f; 
        
        var speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);
        var minSteerFraction = _pawn.FeKaCurrentStats.highSpeedSteerFraction;
        var steerScale = Mathf.Lerp(1f, minSteerFraction, speedRatio);
        
        foreach(var wheel in _pawn.FeKaCurrentStats.wheels)
        {
            if (wheel.axel == Axel.Front)
            {
                var _steerAngle = _steerInput * _pawn.FeKaCurrentStats.turnSensitivity * _pawn.FeKaCurrentStats.maxSteerAngle * steerScale;
                //var newAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, _steerAngle, 0.6f);
                var newAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, _steerAngle, lerpSpeed * Time.fixedDeltaTime);
                if (Mathf.Abs(newAngle - _steerAngle) < 0.01f) newAngle = _steerAngle;
                wheel.wheelCollider.steerAngle = newAngle;
            }
        }
    }
 
    public void Brake(FeKaPawn _pawn, float _moveInput, bool _isBraking)
    {
        if (_isBraking)
        {
            foreach (var wheel in _pawn.FeKaCurrentStats.wheels)
            {
                wheel.wheelCollider.motorTorque = 0;
                wheel.wheelCollider.brakeTorque = _pawn.FeKaCurrentStats.breakForce;
            }
        }
        else if (_moveInput == 0)
        {
            foreach (var wheel in _pawn.FeKaCurrentStats.wheels)
            {
                wheel.wheelCollider.motorTorque = 0;
                wheel.wheelCollider.brakeTorque = _pawn.FeKaCurrentStats.coastBreakForce;
            }
        }
        else
        {
            foreach (var wheel in _pawn.FeKaCurrentStats.wheels)
            {
                wheel.wheelCollider.brakeTorque = 0;
            }
        }
    }
    
    public void WheelEffects(FeKaPawn _pawn, bool _isBraking)
    {
        foreach (var wheel in _pawn.FeKaCurrentStats.wheels)
        {
            if (_isBraking && wheel.axel == Axel.Rear && wheel.wheelCollider.isGrounded && _pawn.GetComponent<Rigidbody>().velocity.magnitude >= 10.0f)
            {
                wheel.wheelEffectObj.GetComponentInChildren<TrailRenderer>().emitting = true;
                wheel.smokeParticle.Emit(1);
            }
            else
            {
                wheel.wheelEffectObj.GetComponentInChildren<TrailRenderer>().emitting = false;
            }
        }
    }
    
    public bool IsHeadClear(FeKaPawn _pawn)
    {
        RaycastHit hit;
        if (Physics.SphereCast(_pawn.transform.position + ((FeKaPawnStats)_pawn.currentStats).headCheckOffset, ((FeKaPawnStats)_pawn.currentStats).headCheckRadius, _pawn.transform.up, out hit, ((FeKaPawnStats)_pawn.currentStats).headCheckDistance, ((FeKaPawnStats)_pawn.currentStats).groundMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }
        return true;
    }
    
    public bool IsOnGround(FeKaPawn _pawn)
    {
        // Move the ground check position upwards if the pawn is crouching to account for their change in height
        Vector3 crouchingOffset = new Vector3(0,0,0);
        if (isCrouching) crouchingOffset = new Vector3(0, ((FeKaPawnStats)_pawn.currentStats).crouchDistance, 0);
        
        return Physics.CheckSphere(_pawn.transform.position - ((FeKaPawnStats)_pawn.currentStats).groundCheckOffset + crouchingOffset, ((FeKaPawnStats)_pawn.currentStats).groundCheckRadius, ((FeKaPawnStats)_pawn.currentStats).groundMask, QueryTriggerInteraction.Ignore);
    }

    public bool IsOnSlope(FeKaPawn _pawn)
    {
        /*
        This function does not account for crouching offsets. Meaning if a pawn is crouched, the slope detection will likely fail and the pawn will slip off the slope.
        This is a bug, but I'm deciding to keep it in since it's super fun to be able to crouch when falling at a slope to slide down it!
        If this needs to be patched out for any reason, update this function to account for the crouch offset. If you're not sure how to do that, check IsOnGround function above. It correctly accounts for the crouch offset.
        Happy sliding! ~Liz
        //*/
        if (Physics.Raycast(_pawn.transform.position, Vector3.down, out slopeHit, ((FeKaPawnStats)_pawn.currentStats).slopeCheckDistance, ((FeKaPawnStats)_pawn.currentStats).groundMask, QueryTriggerInteraction.Ignore))
        {
            return slopeHit.normal != Vector3.up;
        }

        return false;
    }

    public void EnableViewCamera(FeKaPawn _pawn, bool _setActive)
    {
        if (viewCamera is null)
        {
            // Try to get a view camera
            viewCamera =_pawn.GetComponentInChildren<Camera>(true).gameObject;
            if (viewCamera is null) return;
        }
        
        viewCamera.SetActive(_setActive);
    }
    
    /// <summary>
    /// Make the pawn jump using a force applied to the rigidbody
    /// </summary>
    /// <param name="_pawn">A reference to the pawn to get its jump force & IsOnGround state</param>
    /// <param name="_rigidbody"></param>
    public void Jump(FeKaPawn _pawn)
    {
        
        if (IsOnGround(_pawn) is false) return;
        var rigidbody = _pawn.GetComponent<Rigidbody>();
        rigidbody.velocity = new Vector3(rigidbody.velocity.x, 0, rigidbody.velocity.z);
        rigidbody.AddForce(_pawn.FeKaCurrentStats.tiltVisualMesh.transform.up * ((FeKaPawnStats)_pawn.currentStats).jumpForce, ForceMode.Impulse);
    }

    public void Tilt(FeKaPawn _pawn, float _targetZRotation, Transform visualMesh, float targetYShift = 0.2f, float _speed = 5f)
    {
        var currentRot = visualMesh.localEulerAngles;
        var currentPos = visualMesh.localPosition;
        var currentZRot = currentRot.z > 180f ? currentRot.z - 360f : currentRot.z;

        var newZRot = Mathf.Lerp(currentZRot, _targetZRotation, Time.deltaTime * _speed);
        var newY = Mathf.Lerp(currentPos.y, targetYShift, Time.deltaTime * _speed);
        
        visualMesh.localEulerAngles = new Vector3(currentRot.x, currentRot.y, newZRot);
        visualMesh.localPosition = new Vector3(currentPos.x, newY, currentPos.z);
    }
    
    public void TiltReturnToNeutral(FeKaPawn _pawn, Transform visualMesh, float _speed = 5f)
    {
        Tilt(_pawn, 0f, visualMesh, 0, _speed);
    }
    
}
