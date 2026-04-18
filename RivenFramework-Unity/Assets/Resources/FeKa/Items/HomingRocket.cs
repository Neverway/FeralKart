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
    private NetVariableOwner netVariableOwner;
    private NetVariable<string> netDeathState;
    private bool isDying = false;
    private bool suppressOnDestroyDespawn = false;

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Awake()
    {
        netVariableOwner = GetComponent<NetVariableOwner>();
        netDeathState = netVariableOwner.Register<string>("rocket_death", "", OnNetDeathReceived);
    }

    private void Start()
    {
        
        StartCoroutine(FriendlyFireCooldown());
        StartCoroutine(DelayBeforeCollisionEnabled());
        bounces = maxBounces;
    }

    private void Update()
    {
        if (!GetComponent<NetTransform>().hasAuthority) return;
        if (isDying) return;
        _age += Time.deltaTime;
        if (_age >= lifetime)
        {
            TriggerDeath(transform.position);
            return;
        }

        if (_target != null)
        {
            var toTarget = ((_target.transform.position+targetAimOffset) - transform.position).normalized;
            var targetRot = Quaternion.LookRotation(toTarget);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnRate * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (!GetComponent<NetTransform>().hasAuthority) return;
        if (isDying) return;
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
                        Debug.Log("Destroyed because of destroy");
                        TriggerDeath(hit.point);
                        break;
                    case ObjectCollisionBehaviour.stick:
                        gameObject.transform.parent = hit.transform;
                        break;
                    case ObjectCollisionBehaviour.bounce:
                        if (maxBounces > 0)
                        {
                            if (bounces <= 0)
                            {
                                TriggerDeath(hit.point);
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
            TriggerDeath(transform.position);
            Debug.Log("Destroyed because of hit");
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

    private void TriggerDeath(Vector3 position)
    {
        if (isDying) return;
        isDying = true;

        if (GetComponent<NetTransform>().hasAuthority)
        {
            netDeathState.Value = $"{position.x},{position.y},{position.z}";
            Instantiate(explosionEffect, position, transform.rotation, null);
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var s in GetComponentsInChildren<SpriteRenderer>()) s.enabled = false;
            foreach (var l in GetComponentsInChildren<LineRenderer>()) l.enabled = false;
            StartCoroutine(DestroyAfterSync(position));
        }
        else
        {
            PlayDeathAt(position);
        }
    }
    
    private IEnumerator DestroyAfterSync(Vector3 position)
    {
        yield return new WaitForSeconds(0.25f);
        suppressOnDestroyDespawn = true;
        NetSpawner.Despawn(GetComponent<NetTransform>().networkObjectUId);
        Destroy(gameObject);
    }
    
    private void OnNetDeathReceived(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (isDying) return;
        isDying = true;

        var parts = value.Split(',');
        if (parts.Length == 3 &&
            float.TryParse(parts[0], out float x) &&
            float.TryParse(parts[1], out float y) &&
            float.TryParse(parts[2], out float z))
        {
            PlayDeathAt(new Vector3(x, y, z));
        }
        else
        {
            PlayDeathAt(transform.position);
        }
    }

    private void PlayDeathAt(Vector3 position)
    {
        Instantiate(explosionEffect, position, transform.rotation, null);

        if (GetComponent<NetTransform>().hasAuthority)
        {
            NetSpawner.Despawn(GetComponent<NetTransform>().networkObjectUId);
        }

        suppressOnDestroyDespawn = true;
        Destroy(gameObject);
    }
    
    private void OnDestroy()
    {
        if (!suppressOnDestroyDespawn)
        {
            Instantiate(explosionEffect, transform.position, transform.rotation, null);
            var netTransform = GetComponent<NetTransform>();
            if (netTransform != null && netTransform.hasAuthority)
            {
                NetSpawner.Despawn(netTransform.networkObjectUId);
            }
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void SetTarget(FeKaPawn target)
    {
        _target = target;
    }


    #endregion
}
