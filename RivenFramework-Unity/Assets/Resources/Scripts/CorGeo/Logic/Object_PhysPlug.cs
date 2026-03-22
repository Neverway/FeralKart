//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class Object_PhysPlug : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public bool isPlugged;
    public Transform pinPosition;
    public float maximumDistance;
    public Transform distanceReferencePoint;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private Object_PhysPickupAdvanced physPickupAdvanced;
    private Logic_SocketOutlet logicSocketOutlet;
    

    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        physPickupAdvanced = GetComponent<Object_PhysPickupAdvanced>();
    }

    private void FixedUpdate()
    {
        var distance = Vector3.Distance(transform.position, distanceReferencePoint.position);
        if (distance >= maximumDistance)
        {
            if (physPickupAdvanced)
            {
                if (physPickupAdvanced.isHeld)
                {
                    physPickupAdvanced.Drop();
                }
            }

            if (isPlugged)
            {
                SetPluggedIn(false, pinPosition, logicSocketOutlet);
            }
        }
        
        if (isPlugged)
        {
            physPickupAdvanced.transform.position = pinPosition.position;
            physPickupAdvanced.transform.rotation = pinPosition.rotation;
            physPickupAdvanced.propRigidbody.Get().velocity = new Vector3(0f, 0f, 0f);
            if (physPickupAdvanced.isHeld) SetPluggedIn(false, pinPosition, logicSocketOutlet);
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void SetPluggedIn(bool _isPlugged, Transform _pinPosition=null, Logic_SocketOutlet _logicSocketOutlet=null)
    {
        isPlugged = _isPlugged;
        pinPosition = _pinPosition;
        logicSocketOutlet = _logicSocketOutlet;
        if (_isPlugged)
        {
            physPickupAdvanced.Drop();
        }

        else if (!_isPlugged)
        {
            _logicSocketOutlet.DisconnectPlug();
        }
    }


    #endregion
}
