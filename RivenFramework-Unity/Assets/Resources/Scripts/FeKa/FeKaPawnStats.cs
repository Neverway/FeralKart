//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FeKaPawnStats : PawnStats
{    
    public override object Clone()
    {
        return MemberwiseClone();
    }
    
    public ControlMode controlMode;

    [Header("FeKa Vehicle Stats")] 
    public float accelSpeed = 600;
    [Tooltip("How fast the vehicle accelerates")]
    public float maxAcceleration = 25.0f;
    [Tooltip("How quickly the vehicle slows down when the accelerator is pressed")]
    public float brakeAcceleration = 1.0f;
    [Tooltip("How much control the driver has over the turn radius")]
    public float turnSensitivity = 0.8f;
    [Tooltip("How much the steering wheels can be turned")]
    public float maxSteerAngle = 30.0f;
    [Tooltip("The torque to apply to the vehichle's z axis when tilting")]
    public float tiltTorque = 2f;
    [Tooltip("The target rotation amount to tilt the vehicle")]
    public float targetTiltAngle = 15f;
    [Tooltip("Override for the rigidbody's center of mass")]
    public Vector3 _centerOfMass = new Vector3(0.0f, -1.0f, 0.0f);
    [Tooltip("The wheels that drive this vehicle")]
    public List<Wheel> wheels;

    [Header("FeKa Character Stats")] 
    public float respawnTime = 3;

    public int stocks = 3;
    public float shield = 0;
    public float MaxShield = 50;

    [Header("Traits")] 
    public float throwForce=350;
    
    [Header("Movement Speeds")]
    public float movementSpeed = 10;
    public float airMovementMultiplier = 0.25f;
    public float crouchMovementMultiplier = 0.25f;
    [Header("Movement Acceleration")]
    public float groundAccelerationRate = 0.1f;
    public float airAccelerationRate = 0.2f;
    public float slopeAccelerationRate = 0.25f;
    [Header("Movement Drag")]
    public float groundDrag = 8;
    public float airDrag = 0;
    public float slopeDrag = 8;

    [Header("Ground Detection & Jumping")] 
    public float slopeCheckDistance = 0.3f;
    public LayerMask groundMask = 0;
    public float groundCheckRadius = 0.25f;
    public Vector3 groundCheckOffset = new Vector3();
    public float jumpForce = 2600;

    [Header("Head Detection & Crouching")]
    [Tooltip("The radius of the sphere-cast to check if the head is clear, (this value should be smaller than the radius of the body collider to avoid getting false-positives when a pawn is up against a wall)")]
    public float headCheckRadius = 0.25f;
    [Tooltip("The offset from the pawn's origin to start the sphere-cast to check if the head is clear")]
    public Vector3 headCheckOffset = new Vector3(0, 1.75f, 0);
    [Tooltip("The distance of the sphere-cast to check if the head is clear, (This value should normally match, or be greater than, the crouchDistance)")]
    public float headCheckDistance = 0.5f;
    [Tooltip("The amount of height to add/subtract from the collider's height when uncrouching/crouching")]
    public float crouchDistance = 0.5f;
    public Vector3 crouchColliderOffset = new Vector3(0, 0.25f, 0);
}

public enum ControlMode
{
    LocalPlayer,
    CPU,
    NetworkPlayer
};
 
public enum Axel
{
    Front,
    Rear
}
 
[Serializable]
public struct Wheel
{
    [Tooltip("The visual object that represents the wheel, this is used to rotate the wheels when turning")]
    public GameObject wheelModel;
    [Tooltip("The wheel collider for this wheel (Who coulda guessed?)")]
    public WheelCollider wheelCollider;
    [Tooltip("The line renderer fx object for the skidmark FX")]
    public GameObject wheelEffectObj;
    [Tooltip("The particle FX emitter for the tire smoke")]
    public ParticleSystem smokeParticle;
    [Tooltip("Specifies weather this wheel is part of the front or back of the vehicle")]
    public Axel axel;
}