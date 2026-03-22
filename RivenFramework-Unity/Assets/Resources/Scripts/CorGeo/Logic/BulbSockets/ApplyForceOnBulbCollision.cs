using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class ApplyForceOnBulbCollision : MonoBehaviour, BulbCollisionBehaviour
{
    public float force;
    private new Rigidbody rigidbody;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
            Destroy(this);
    }
    
    public bool OnBulbCollision(Projectile_Marker bulb, RaycastHit hit)
    {
        rigidbody.AddForceAtPosition(bulb.moveVector * force, hit.point, ForceMode.Impulse);
        return false;
    }
}
