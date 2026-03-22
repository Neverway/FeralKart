//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class Graphics_MotionSmear : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public float speedThreshold = 2f;
    public float maxTrailTime = 0.2f;
    public float maxTrailWidth = 0.5f;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private Rigidbody objectRigidbody;
    private TrailRenderer trail;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    void Start()
    {
        objectRigidbody = GetComponent<Rigidbody>();
        trail = GetComponent<TrailRenderer>();

        trail.time = 0f;
        trail.startWidth = 0f;
        trail.endWidth = 0f;
    }
    
    void Update()
    {
        var speed = 0.0f;
        
        if (!objectRigidbody) speed = 0f; 
        else speed = objectRigidbody.velocity.magnitude;
        
        if (speed > speedThreshold)
        {
            float time = Mathf.InverseLerp(speedThreshold, speedThreshold * 5f, speed);
            trail.time = Mathf.Lerp(0f, maxTrailTime, time);
            trail.startWidth = Mathf.Lerp(0f, maxTrailWidth, time);
            trail.endWidth = 0f;
        }
        else
        {
            trail.time = 0f;
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
