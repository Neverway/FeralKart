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

public class Logic_SocketOutlet : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public LogicOutput<bool> socketConnected = new LogicOutput<bool>(false);


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private bool reconnectionOnCooldown;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public Transform pinPosition;
    public Object_PhysPlug connectedPlug;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Update()
    {
        socketConnected.Set(connectedPlug != null);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Exit if this is already plugged in
        if (connectedPlug || reconnectionOnCooldown) return;
        
        connectedPlug = other.GetComponentInParent<Object_PhysPlug>();
        if (connectedPlug)
        {
            connectedPlug.SetPluggedIn(true, pinPosition, this);
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private IEnumerator ReconnectionCooldown()
    {
        reconnectionOnCooldown = true;
        yield return new WaitForSeconds(0.5f);
        reconnectionOnCooldown = false;
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void DisconnectPlug()
    {
        connectedPlug = null;
        StartCoroutine(ReconnectionCooldown());
    }


    #endregion
}
