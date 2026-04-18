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
    public DamageInfo damageInfo = new DamageInfo(-70);
    public GameObject explosionEffect;
    public Vector3 targetAimOffset = new Vector3(0f, 0.15f, 0f); 
    
    [Header("Homing")]
    public float turnRate = 90f;

    public List<FeKaPawn> exemptPawns;

    private FeKaPawn _target;
    private float _age;
    
    [Header("Collision Tests")]
    public float timeUntilFriendlyFireEnabled;
    public ObjectCollisionBehaviour collisionBehaviour;
    public float collisionRadius;
    public float delayBeforeCollisionEnabled;
    public LayerMask collisionMask;
    private bool collisionCheckEnabled;
    public int maxBounces;
    public float damageMultiplyOnBounce;
    private int bounces;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private void Start()
    {
        StartCoroutine(FriendlyFireCooldown());
        StartCoroutine(DelayBeforeCollisionEnabled());
        bounces = maxBounces;
    }

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
    }

    private void FixedUpdate()
    {
        if (!GetComponent<NetTransform>().hasAuthority) return;
        var movement = transform.forward * speed * Time.deltaTime;
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, collisionRadius, transform.forward, out hit, movement.magnitude, collisionMask))
        {
            if (collisionCheckEnabled)
            {
                switch (collisionBehaviour)
                {
                    case ObjectCollisionBehaviour.none:
                        break;
                    case ObjectCollisionBehaviour.destroy:
                        Destroy(gameObject);
                        break;
                    case ObjectCollisionBehaviour.stick:
                        gameObject.transform.parent = hit.transform;
                        break;
                    case ObjectCollisionBehaviour.bounce:
                        if (maxBounces > 0)
                        {
                            if (bounces <= 0)
                            {
                                Destroy(gameObject);
                                return;
                            }
                            bounces--;
                        }
                        damageInfo.amount *= damageMultiplyOnBounce;
                        var reflDirection = Vector3.Reflect(transform.forward, hit.normal);
                        transform.rotation = Quaternion.LookRotation(reflDirection);
                        break;
                }
            }
        }
        else
        {
            transform.position += movement;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        var pawn = other.GetComponentInParent<FeKaPawn>();
        
        if (pawn != null && !exemptPawns.Contains(pawn))
        {
            pawn.ModifyHealth(damageInfo);
            pawn.physicsbody.AddForce(Vector3.up * explosionForce, ForceMode.Impulse);
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Instantiate(explosionEffect, transform.position, transform.rotation, null);
        var netTransform = GetComponent<NetTransform>();
        if (netTransform != null)
        {
            NetSpawner.Despawn(GetComponent<NetTransform>().networkObjectUId);
        }
    }

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private IEnumerator FriendlyFireCooldown()
    {
        yield return new WaitForSeconds(timeUntilFriendlyFireEnabled);
        exemptPawns.Clear();
    }
    private IEnumerator DelayBeforeCollisionEnabled()
    {
        yield return new WaitForSeconds(delayBeforeCollisionEnabled);
        collisionCheckEnabled = true;
    }


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
