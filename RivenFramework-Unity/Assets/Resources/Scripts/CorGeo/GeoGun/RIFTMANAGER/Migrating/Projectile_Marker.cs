//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//  Connorses, Errynei, Soulex
//
//====================================================================================================================//

using System;
using System.Collections.Generic;
using DG.Tweening;
using RivenFramework;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Handles the travel, placement, and shattering of CorGeo markers
/// </

public class Projectile_Marker : UProjectile
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("The offset to move the projectile away from the wall so it's not clipping")]
    [SerializeField] public float pinOffset;
    [Tooltip("Snapping size of the grid that markers try to adjust to")]
    [SerializeField] public float gridSize = 1;

    
    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public bool pinned;
    [Tooltip("Set by the geogun when the projectile is fired, Allows a marker to be placed on any material")]
    public bool allowMarkerPlacementAnywhere;
    
    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private float storedSpeed;

    
    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private RiftManager riftManager;
    private RaycastHit hit2;
    [Tooltip("This is the mesh on the object that is shown through walls, the reference is needed so we can hide the highlight if the projectile is not pinned")]
    [SerializeField] private GameObject outlineFX;
    [Tooltip("Reference to the gun so the projectile can check for valid placement (this is set by the gun when it spawns the projectile)")]
    [HideInInspector] public Item_Utility_Geogun geogun;

    #endregion


    #region=======================================( Functions )======================================================= //
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public new void Start()
    {
        base.Start();
        riftManager = GameInstance.Get<RiftManager>();
    }

    private void OnDestroy()
    {
        MarkerBreak();
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    protected override void OnProjectileCollision(RaycastHit _hit)
    {
        storedSpeed = moveSpeed;
        base.OnProjectileCollision(_hit);
        
        // Check if the projectile hit a BulbCollisionBehaviour (like a socket)
        if (GetHitBulbCollisionBehaviour(_hit)) return;

        // Test for pinning to mesh colliders via their material
        if (GetHitConductiveMaterial(_hit)) return;
        
        if (GetHitReboundMaterial(_hit)) return;
        
        // We hit something, but it wasn't pinnable, so let's just break
        MarkerBreak();
    }

    /// <summary>
    /// If we hit a BulbCollisionBehaviour, (such as an Outlet), call method to determine what to do (such as attach bulb to position)
    /// </summary>
    /// <param name="_hit">The raycast hit to check</param>
    /// <returns>Returns if the raycast hit a BulbCollisionBehaviour</returns>
    private bool GetHitBulbCollisionBehaviour(RaycastHit _hit)
    {
        if (_hit.collider.gameObject.TryGetComponent<BulbCollisionBehaviour> (out var bulbBehaviourObj))
        {
            // Stop logic if OnBulbCollision returns true, denoting that it has overriden the collision behaviour, and we should stop checking logic
            if (bulbBehaviourObj.OnBulbCollision(this, _hit)) return true;
        }
        else
        {
            var bulbCollisionBehaviour = _hit.collider.gameObject.GetComponentInParent<BulbCollisionBehaviour>();
            if (bulbCollisionBehaviour != null)
            {
                // Stop logic if OnBulbCollision returns true, denoting that it has overriden the collision behaviour, and we should stop checking logic
                if (bulbCollisionBehaviour.OnBulbCollision(this, _hit)) return true;
            }
        }

        return false;
    }

    private bool GetHitConductiveMaterial(RaycastHit _hit)
    {
        // If the collider isn't a mesh, we can't get material data from it, so we assume the bulb cannot attach
        if (_hit.collider is not MeshCollider)
        {
            return false;
        }
        
        // TODO I don't think it matters if the object we hit is sliceable or not, we only really care if it has a mesh collider ~Liz
        //if (_hit.collider.gameObject.TryGetComponent<CorGeo_SliceableMesh>(out var _out))
        //{
            // todo: Commented out this line of code, actually ended up throwing an IndexOutOfRangeException
            // DisplayDebugTriangle(colMesh, triIndex, hit.collider.transform);
            
            // Check Marker logic
            if (geogun.GetIsValidTarget(_hit))
            {
                // Set rotation to match face normal
                transform.DORotateQuaternion(Quaternion.LookRotation(-_hit.normal),0.08f);
            
                // Snap to a grid along the face
                // Get the hit position and normal
                Vector3 hitPosition = _hit.point + _hit.normal * pinOffset;
                // Figure out the relative Vector3.direction from the hit.point
                Vector3 relativeRight, relativeUp;
                if (Mathf.Abs(_hit.normal.y) > 0.99f) // Nearly vertical surface (e.g., floor or ceiling)
                {
                    relativeRight = Vector3.right; // Use world X axis for snapping
                    relativeUp = Vector3.forward;  // Use world Z axis for snapping
                }
                else
                {
                    // Calculate relative axes based on the hit normal
                    relativeRight = Vector3.Cross(_hit.normal, Vector3.up);
                    relativeUp = Vector3.Cross(_hit.normal, relativeRight);
                }
                // Project the hit position onto these axes and snap along them
                hitPosition += relativeRight * (Mathf.Round(Vector3.Dot(hitPosition, relativeRight) / gridSize) * gridSize - Vector3.Dot(hitPosition, relativeRight));
                hitPosition += relativeUp * (Mathf.Round(Vector3.Dot(hitPosition, relativeUp) / gridSize) * gridSize - Vector3.Dot(hitPosition, relativeUp));
            
                // Update the marker's position to the snapped position
                MarkerPinAt(hitPosition += _hit.normal * pinOffset, _hit.normal);
                transform.position = hitPosition;
                transform.localPosition += _hit.normal * pinOffset;

                //Audio_FMODAudioManager.PlayOneShot(Audio_FMODEvents.Instance.nixieTubePin, transform.position);
                //EndOfDemoStatsTracker.instance.AddBulbCount(); //This is also called in Attach(). Just make sure this isnt called twice if this code is changed
                return true;
            }
        //}
        return false;
    }

    private bool GetHitReboundMaterial(RaycastHit _hit)
    {
        // If the collider isn't a mesh, we can't get material data from it, so we assume the bulb cannot attach
        if (_hit.collider is not MeshCollider)
        {
            return false;
        }

        if (geogun.GetIsValidTarget(_hit, true))
        {
            StopProjectile();
            var v = transform.forward.normalized;
            var n = -_hit.normal;
            Debug.DrawRay(transform.position, v, Color.red, 25);
            Debug.DrawRay(_hit.point, n, Color.blue, 25);
            var bounceTrajectory = Vector3.Reflect(v, n);
            Debug.DrawRay(_hit.point, bounceTrajectory, Color.magenta, 25);
            
            print($"Bouncy bounce! {storedSpeed}");
            transform.rotation = Quaternion.LookRotation(bounceTrajectory);
            
            
            Physics.Raycast(_hit.point, bounceTrajectory, out RaycastHit hitNew, 255, layerMask);
            InitializeProjectile(storedSpeed, _hit.point, Vector3.Distance(_hit.point, hitNew.point));
            return true;
        }

        
        return false;
    }
    
    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    private void MarkerPin()
    {
        MarkerPinAt(hit2.point, -hit2.normal);
    }
    
    /// <summary>
    /// Handles the placement logic of the marker projectile and sets its position and rotation
    /// </summary>
    /// <param name="_position">The position to place the marker</param>
    /// <param name="_direction">The direction to point the marker</param>
    public void MarkerPinAt(Vector3 _position, Vector3 _direction)
    {
        StopProjectile();
        pinned = true;
        transform.position = _position;
        transform.rotation = Quaternion.LookRotation(_direction);

        outlineFX.SetActive(true);
        
        // Add itself to the rift manager if possible
        if (riftManager.markerA == null) riftManager.markerA = this.transform;
        else if (riftManager.markerB == null) riftManager.markerB = this.transform;
    }
    
    /// <summary>
    /// Handles all the logic for cleanly destroying the marker projectile
    /// </summary>
    public void MarkerBreak()
    {
        // Remove itself from the rift manager if present
        if (pinned)
        {
            if (riftManager.markerA == this.transform) riftManager.markerA = null;
            else if (riftManager.markerB == this.transform) riftManager.markerB = null;
        }
        Destroy(gameObject, 0.01f);
    }


    #endregion
}
