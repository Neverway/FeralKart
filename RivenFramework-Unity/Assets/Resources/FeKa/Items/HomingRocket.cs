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
using UnityEngine;

public class HomingRocket : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/

    [Header("Flight")]
    public float speed = 28f;
    public float lifetime = 6f;
    public float explosionForce = 10f;
    public float damage = 70;
    public GameObject explosionEffect;
    public Vector3 targetAimOffset = new Vector3(0f, 0.15f, 0f); 
    
    [Header("Homing")]
    public float turnRate = 90f;

    public List<FeKaPawn> exemptPawns;

    private FeKaPawn _target;
    private float _age;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private void Update()
    {
        if (!GetComponent<NetTransform>().hasAuthority) return;
        _age += Time.deltaTime;
        if (_age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_target != null)
        {
            var toTarget = ((_target.transform.position+targetAimOffset) - transform.position).normalized;
            var targetRot = Quaternion.LookRotation(toTarget);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                turnRate * Time.deltaTime);
        }

        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        var pawn = other.GetComponentInParent<FeKaPawn>();
        
        if (pawn != null && !exemptPawns.Contains(pawn))
        {
            pawn.ModifyHealth(-damage);
            pawn.physicsbody.AddForce(Vector3.up * explosionForce, ForceMode.Impulse);
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Instantiate(explosionEffect, transform.position, transform.rotation, null);
        var netTransform = GetComponent<NetTransform>();
        if (netTransform != null && netTransform.hasAuthority)
        {
            NetSpawner.Despawn(GetComponent<NetTransform>().networkObjectUId);
        }
    }

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void SetTarget(FeKaPawn target)
    {
        _target = target;
    }


    #endregion
}
