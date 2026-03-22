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

public class Graphics_StretchSmear : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public float stretchFactor = 0.3f;
    public float speedThreshold = 2f;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private Rigidbody objectRigidbody;
    private Vector3 originalScale;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    void Awake()
    {
        objectRigidbody = GetComponent<Rigidbody>();
        originalScale = transform.localScale;
    }
    
    void LateUpdate()
    {
        Vector3 velocity = objectRigidbody != null ? objectRigidbody.velocity : Vector3.zero;
        float speed = velocity.magnitude;

        if (speed > speedThreshold)
        {
            Vector3 normalizedVelocity = velocity.normalized;
            float stretch = 1f + stretchFactor * (speed - speedThreshold);

            // squash in perpendicular axes
            transform.localScale = new Vector3(
                originalScale.x * (1f / stretch),
                originalScale.y * (1f / stretch),
                originalScale.z * stretch
            );

            // align with velocity
            transform.rotation = Quaternion.LookRotation(normalizedVelocity);
        }
        else
        {
            transform.localScale = originalScale;
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
