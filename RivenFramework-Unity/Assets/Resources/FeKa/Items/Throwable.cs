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

public class Throwable : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Header("Flight")]
    public float explosionForce = 10f;
    public DamageInfo damageInfo = new DamageInfo(-50);
    public GameObject explosionEffect;
    public Vector3 targetAimOffset = new Vector3(0f, 0.15f, 0f); 
    public List<FeKaPawn> exemptPawns;
    private FeKaPawn _target;
    public Vector2 throwForce;
    
    [Header("Collision Tests")]
    public float timeUntilFriendlyFireEnabled;
    public ObjectCollisionBehaviour collisionBehaviour;
    public float collisionRadius;
    public float delayBeforeCollisionEnabled;
    public LayerMask collisionMask;
    private bool collisionCheckEnabled;
    public Rigidbody physicsBody;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private NetVariable<string> netInstigatorId;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Awake()
    {
        var netVarOwner = GetComponent<NetVariableOwner>();
        netInstigatorId = netVarOwner.Register<string>("throwable_instigatorId", "", null);

        physicsBody = GetComponent<Rigidbody>();
        physicsBody.AddForce((transform.forward*throwForce.x)+(Vector3.up*throwForce.y),  ForceMode.Impulse);
        StartCoroutine(FriendlyFireCooldown());
        StartCoroutine(DelayBeforeCollisionEnabled());
    }

    private void OnTriggerEnter(Collider other)
    {
        var pawn = other.GetComponentInParent<FeKaPawn>();
        
        if (pawn != null && !exemptPawns.Contains(pawn))
        {
            if (damageInfo.instigator == null && netInstigatorId != null && !string.IsNullOrEmpty(netInstigatorId.Value))
            {
                var raceManager = GameInstance.Get<GI_RaceManager>();
                if (raceManager != null)
                {
                    foreach (var racer in raceManager.racers)
                    {
                        var owner = racer.GetComponent<NetVariableOwner>();
                        if (owner != null && owner.NetworkObjectId == netInstigatorId.Value)
                        {
                            damageInfo.instigator = racer;
                            break;
                        }
                    }
                }
            }

            
            pawn.ModifyHealth(damageInfo);
            pawn.physicsbody.AddForce(Vector3.up * explosionForce, ForceMode.Impulse);
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!collisionCheckEnabled) return;
        var hit = Physics.OverlapSphere(transform.position, collisionRadius, collisionMask);
        if (hit.IsNotEmptyOrNull())
        {
            switch (collisionBehaviour)
            {
                case ObjectCollisionBehaviour.none:
                    break;
                case ObjectCollisionBehaviour.destroy:
                    Destroy(gameObject);
                    break;
                case ObjectCollisionBehaviour.stick:
                    physicsBody.isKinematic = true;
                    gameObject.transform.parent = hit[0].transform;
                    break;
                case ObjectCollisionBehaviour.bounce:
                    break;
            }
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


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
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
    

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void SetTarget(FeKaPawn target)
    {
        _target = target;
    }
    
    public void SetInstigator(FeKaPawn instigator)
    {
        damageInfo.instigator = instigator;
        if (netInstigatorId != null)
            netInstigatorId.Value = instigator?.GetComponent<NetVariableOwner>()?.NetworkObjectId ?? "";
    }


    #endregion
}
[Serializable]
public enum ObjectCollisionBehaviour
{
    none,
    destroy,
    stick,
    bounce
}
