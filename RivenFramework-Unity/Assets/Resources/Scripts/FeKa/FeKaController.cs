using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FeKaController : MonoBehaviour
{
     public enum ControlMode
     {
         Keyboard,
         Buttons
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
 
     public ControlMode control;
 
     [Tooltip("How fast the vehicle accelerates")]
     public float maxAcceleration = 25.0f;
     [Tooltip("How quickly the vehicle slows down when the accelerator is pressed")]
     public float brakeAcceleration = 1.0f;
     [Tooltip("How much control the driver has over the turn radius")]
     public float turnSensitivity = 0.8f;
     [Tooltip("How much the steering wheels can be turned")]
     public float maxSteerAngle = 30.0f;
     [Tooltip("Override for the rigidbody's center of mass")]
     public Vector3 _centerOfMass = new Vector3(0.0f, -1.0f, 0.0f);
     [Tooltip("The wheels that drive this vehicle")]
     public List<Wheel> wheels;
 
     float moveInput;
     float steerInput;
 
     private Rigidbody carRb;
 
 
     void Start()
     {
         carRb = GetComponent<Rigidbody>();
         carRb.centerOfMass = _centerOfMass;
 
     }
 
     void Update()
     {
         GetInputs();
         AnimateWheels();
         WheelEffects();
     }
 
     void LateUpdate()
     {
         Move();
         Steer();
         Brake();
     }
 
     public void MoveInput(float input)
     {
         moveInput = input;
     }
 
     public void SteerInput(float input)
     {
         steerInput = input;
     }
 
     void GetInputs()
     {
         if(control == ControlMode.Keyboard)
         {
             moveInput = Input.GetAxis("Vertical");
             steerInput = Input.GetAxis("Horizontal");
         }
     }
 
     void Move()
     {
         foreach(var wheel in wheels)
         {
             wheel.wheelCollider.motorTorque = moveInput * maxAcceleration;
         }
     }
 
     void Steer()
     {
         foreach(var wheel in wheels)
         {
             if (wheel.axel == Axel.Front)
             {
                 var _steerAngle = steerInput * turnSensitivity * maxSteerAngle;
                 wheel.wheelCollider.steerAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, _steerAngle, 0.6f);
             }
         }
     }
 
     void Brake()
     {
         if (Input.GetKey(KeyCode.Space) || moveInput == 0)
         {
             foreach (var wheel in wheels)
             {
                 wheel.wheelCollider.brakeTorque = 300 * brakeAcceleration * Time.deltaTime;
             }
         }
         else
         {
             foreach (var wheel in wheels)
             {
                 wheel.wheelCollider.brakeTorque = 0;
             }
         }
     }
 
     void AnimateWheels()
     {
         foreach(var wheel in wheels)
         {
             Quaternion rot;
             Vector3 pos;
             wheel.wheelCollider.GetWorldPose(out pos, out rot);
             if (wheel.wheelModel) wheel.wheelModel.transform.position = pos;
             if (wheel.wheelModel) wheel.wheelModel.transform.rotation = rot;
         }
     }
 
     void WheelEffects()
     {
         foreach (var wheel in wheels)
         {
             if (Input.GetKey(KeyCode.Space) && wheel.axel == Axel.Rear && wheel.wheelCollider.isGrounded == true && carRb.velocity.magnitude >= 10.0f)
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
 }
