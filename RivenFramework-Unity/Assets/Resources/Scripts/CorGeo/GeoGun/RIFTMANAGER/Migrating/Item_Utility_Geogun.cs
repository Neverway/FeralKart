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
using System.Linq;
using RivenFramework;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Spawns & clears the marker projectiles, and sends signals to the rift manager to expand or compress
/// </summary>
public class Item_Utility_Geogun : RiftController, ILoggable
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [field: SerializeField] public bool EnableRuntimeLogging { get; set; }
    [Header("GeoGun Upgrades")]
    [Todo("Need to add nonlinear slicing check to rift manager", Owner = "Liz")]
    [Tooltip("Allows rifts to be placed on walls")]
    public bool allowNonLinearSlicing = true;

    [Todo("Need to add slamming rift check to rift manager", Owner = "Liz")]
    [Tooltip("Allows the player to slam rifts closed, creating a vacuum that flings things out of rifts")]
    public bool allowSlammingRift;
    [Tooltip("Debug parameter to... well, you get it (Allows markers to be placed on any material)")]
    public bool allowMarkerPlacementAnywhere;
    [Header("Projectile Checks")]
    [Tooltip("The materials that markers can be placed on")]
    public List<Material> validPlacementMaterials;
    [Tooltip("The materials that markers can bounce off of")]
    public List<Material> validReboundMaterials;
    [Tooltip("The layermask for firing projectiles")]
    public LayerMask layerMask;
    [Tooltip("How fast marker projectiles travel when fired")]
    public int projectileMarkerSpeed = 50;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("This is set by a rift manager when it has latched onto this gun, " +
             "it's used to avoid multiple rift managers all trying to fight over the same gun link")]
    [HideInInspector] public bool isLinkedToManager;
    [Tooltip("Subscribed to by rift manager to tell when gun wants to collapse")]
    public override event Action OnCollapseHeld;
    [Tooltip("Subscribed to by rift manager to tell when gun wants to stop collapsing")]
    public override event Action OnCollapseReleased;
    [Tooltip("Subscribed to by rift manager to tell when gun wants to expand")]
    public override event Action OnExpandHeld;
    [Tooltip("Subscribed to by rift manager to tell when gun wants to stop expanding")]
    public override event Action OnExpandReleased;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private int maxProjectiles = 2;
    private bool wantsToExpand;
    private bool wantsToCollapse;
    private Transform cachedPawnViewPoint;
    private RaycastHit lastValidationHit;
    private bool lastValidationResult;
    private int lastValidationFrame = -1;
    private RiftManager riftManager;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [HideInInspector] public List<GameObject> spawnedProjectiles = new List<GameObject>();
    private Transform playerViewPoint;
    [Header("References")]
    [Tooltip("This is the object to spawn when firing the gun")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("This is where the raycast for firing the gun starts from")] // <== This is a lie >: ~Liz
    [SerializeField] private Transform gunBarrel;
    [Tooltip("A reference to the gun's, and it's outline's, animator")]
    [SerializeField] private Animator animator1, animator2;
    //[Tooltip("This is what collision layers the raycast will collide with")] 
    //[SerializeField] private LayerMask projectileLayerMask;
    [SerializeField] private GameObject previewPlanePrefab;
    [SerializeField] private List<GameObject> previewPlanes = new List<GameObject>();


    #endregion


    #region=======================================( Functions )======================================================= //
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        /*
        var riftManager = GameInstance.Get<GI_RiftManager>();
        if (riftManager)
        {
            riftManager.RegisterGeogun(this);
        }*/
        
        
        riftManager = GameInstance.Get<RiftManager>();
        if (riftManager)
        {
            riftManager.RegisterRiftController(this);
        }
    }

    private void Update()
    {
        // Process the wants to BLANK requests from item actions
        // (I'm doing it this way for now since we want to be able to hold collapse or expand before a rift might be active)
        if (spawnedProjectiles.Count >= maxProjectiles)
        {
            if (wantsToExpand)
            {
                OnExpandHeld?.Invoke();
            }
            else if (!wantsToExpand)
            {
                OnExpandReleased?.Invoke();
            }
            if (wantsToCollapse)
            {
                OnCollapseHeld?.Invoke();
            }
            else if (!wantsToCollapse)
            {
                OnCollapseReleased?.Invoke();
            }
        }
        
        // Auto-removes null projectiles from the spawnedProjectiles list
        spawnedProjectiles = spawnedProjectiles.Where(projectile => !projectile.IsUnityNull()).ToList();
        
        CheckForPreviewPlanes();
        PlacePreviewPlanes ();
    }

    private void CheckForPreviewPlanes()
    {
        // If the planes list is empty or contains invalid data, recreate the list of elements
        if (previewPlanes is null || previewPlanes.Count <= 0 || previewPlanes[0] == null || previewPlanes[1] == null)
        {
            // Create preview plane objects
            previewPlanes = new List<GameObject>(2);
            previewPlanes.Add(Instantiate(previewPlanePrefab));
            previewPlanes.Add(Instantiate(previewPlanePrefab));
            foreach (GameObject previewPlane in previewPlanes)
            {
                previewPlane.SetActive(false);
            }
        }
    }
    
    private void PlacePreviewPlanes ()
    {
        if (riftManager.markerA != null && riftManager.markerB == null)
        {
            Physics.Raycast (playerViewPoint.position, playerViewPoint.forward, out RaycastHit rayHit, 255, layerMask);

            if (rayHit.collider == null)
            {
                SetPreviewPlanesEnabled (false);
                return;
            }

            if (GetIsValidTarget (rayHit) == false)
            {
                SetPreviewPlanesEnabled (false);
                return;
            }

            SetPreviewPlanesEnabled(true);

            previewPlanes[0].transform.position = riftManager.markerA.transform.position;
            previewPlanes[1].transform.position = rayHit.point;

            previewPlanes[0].transform.LookAt (previewPlanes[1].transform.position);
            previewPlanes[1].transform.LookAt (previewPlanes[0].transform.position);

        }
        else
        {
            SetPreviewPlanesEnabled (false);
        }
    }

    private void SetPreviewPlanesEnabled(bool _enabled)
    {
        foreach (GameObject previewPlane in previewPlanes)
        {
            if (previewPlane) previewPlane.SetActive (_enabled);
        }
    }

    private void FixedUpdate()
    {
        // Moving this here to hopefully reduce its performance impact
        // Get a reference to the player view point
        if (!playerViewPoint)
        {
            var targetPawn = GetComponentInParent<Pawn>();
            if (targetPawn) playerViewPoint = targetPawn.viewPoint;
            return;
        }
        
        AimBarrelTowardsCenterOfView();
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private bool FireMarker()
    {
        // Exit if the gun has already shot both markers
        if (spawnedProjectiles.Count >= maxProjectiles) return false;
        
        // Play the shoot anim for the gun and it's outline
        animator1.SetTrigger("Shoot");
        animator2.SetTrigger("Shoot");
        
        // Spawn the projectile
        var projectile = Instantiate(projectilePrefab, playerViewPoint.position, playerViewPoint.rotation, null).GetComponent<Projectile_Marker>();
        
        projectile.geogun = this;
        Physics.Raycast(playerViewPoint.position, playerViewPoint.forward, out RaycastHit hit2, 255, layerMask);
        projectile.InitializeProjectile(projectileMarkerSpeed, gunBarrel.position, Vector3.Distance(gunBarrel.position, hit2.point));
        projectile.allowMarkerPlacementAnywhere = allowMarkerPlacementAnywhere;
        
        // Keep track of fired markers
        spawnedProjectiles.Add(projectile.gameObject);
        if (spawnedProjectiles.Count >= maxProjectiles)
        {
            animator1.SetBool("Empty", true);
            animator2.SetBool("Empty", true);
        }

        return true;
    }
    
    public void DestroyMarkers()
    {
        animator1.SetBool("Empty", false);
        animator2.SetBool("Empty", false);
        animator1.SetTrigger("Clear");
        animator2.SetTrigger("Clear");
        foreach (var _projectile in spawnedProjectiles)
        {
            Destroy(_projectile);
        }
        spawnedProjectiles.Clear();
    }
    
    /// <summary>
    /// Ensures that the actual point in which projectiles are fired from is facing where the player's crosshair is aimed
    /// </summary>
    private void AimBarrelTowardsCenterOfView ()
    {
        // Perform the raycast, ignoring the trigger layer
        if (Physics.Raycast (playerViewPoint.position, playerViewPoint.forward, out RaycastHit viewPoint, Mathf.Infinity, layerMask))
        {
            // If the raycast hits something, aim the barrel towards the hit point
            gunBarrel.LookAt (viewPoint.point);
        }
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public override void UsePrimary(string _mode = "press")
    {
        switch (_mode)
        {
            case "press":
                FireMarker();
                wantsToExpand = true;
                break;
            case "release":
                wantsToExpand = false;
                break;
        }
    }

    public override void UseSecondary(string _mode = "press")
    {
        switch (_mode)
        {
            case "press":
                wantsToCollapse = true;
                break;
            case "release":
                wantsToCollapse = false;
                break;
        }
    }
    
    public override void UseTertiary(string _mode = "press")
    {
        switch (_mode)
        {
            case "press":
                DestroyMarkers();
                break;
            case "release":
                break;
        }
    }

    /// <summary>
    /// Picking up objects instantiates a copy of the object, so this function fixes the fact that the copy will already be marked as linked
    /// This is called by VolumeItemPickup's OnAttemptItemPickup UnityEvent
    /// </summary>
    public void BreakRiftManagerLink()
    {
        isLinkedToManager = false;
    }

    /// <summary>
    /// A version of the GetIsValidTarget function that's setup to use the player's view point raycast as the point to check
    /// This is used by the placement indicator on the crosshair
    /// </summary>
    /// <returns>Returns true if the player is looking at a target they are allowed to shoot</returns>
    public bool GetIsValidTargetFromView()
    {
        //Physics.Raycast(GetComponentInParent<Pawn>().viewPoint.position, GetComponentInParent<Pawn>().viewPoint.forward, out RaycastHit _hit, 255, layerMask);
        //return GetIsValidTarget(_hit);

        if (lastValidationFrame == Time.frameCount)
        {
            return lastValidationResult;
        }

        if (!cachedPawnViewPoint)
        {
            var pawn = GetComponentInParent<Pawn>();
            if (pawn) cachedPawnViewPoint = pawn.viewPoint;
            else return false;
        }
        
        Physics.Raycast(cachedPawnViewPoint.position, cachedPawnViewPoint.forward, out lastValidationHit, 255, layerMask);

        lastValidationResult = GetIsValidTarget(lastValidationHit);
        lastValidationFrame = Time.frameCount;

        return lastValidationResult;
    }
    
    /// <summary>
    /// Used by the projectile markers to see if they are pointed at an object they can pin to
    /// </summary>
    /// <returns>Returns true if the gun is pointed at a target it's allowed to shoot</returns>
    public bool GetIsValidTarget(RaycastHit _hit, bool checkIsReboundMaterial = false)
    {
        if (!_hit.collider)
        {
            DebugConsole.Log(this, "fail 0");
            //Debug.LogWarning("Somehow raycast hit an invalid object");
            return false;
        }
        // Gun is pointed at a bulb snapping point (That is valid!)
        // TODO - BulbCollisionBehaviour has not been ported!
        if (_hit.collider.gameObject.GetComponent<BulbCollisionBehaviour>() != null)
        {
            DebugConsole.Log(this, "check 1");
            return true;
        }

        // Gun is pointed at a sliceable object
        if (_hit.collider.gameObject.TryGetComponent<CorGeo_SliceableMesh>(out _) is false)
        {
            DebugConsole.Log(this, "fail 1");
            return false;
        }
        // Non-mesh colliders don't support getting the polygon information, so we exit if it's not a mesh collider
        if (_hit.collider is not MeshCollider mCollider)
        {
            DebugConsole.Log(this, "fail 2");
            return false;
        }
        // Get if the raycast hit a polygon with a valid material to place markers on
        if (_hit.collider.gameObject.TryGetComponent(out Renderer rend) is false)
        {
            DebugConsole.Log(this, "fail 3");
            return false;
        }
        // Return true if allowMarkerPlacementAnywhere
        if (allowMarkerPlacementAnywhere) return true;
        
        // Gather information about the mesh
        Mesh colMesh = mCollider.sharedMesh;
        DebugConsole.Log(this, $"Collider mesh: {colMesh?.name}, SubMeshCount: {colMesh?.subMeshCount}");
        int triIndex = _hit.triangleIndex;
        DebugConsole.Log(this, $"Triangle index: {triIndex}");
        int subMeshIndex = GetSubMeshIndex(colMesh, triIndex);
        DebugConsole.Log(this, $"SubMesh index: {subMeshIndex}");
        DebugConsole.Log(this, $"Renderer materials count: {rend.sharedMaterials.Length}");

        if (subMeshIndex >= 0 && subMeshIndex < rend.sharedMaterials.Length)
        {
            var hitMaterial = rend.sharedMaterials[subMeshIndex];
            DebugConsole.Log(this, $"Hit material: {hitMaterial?.name}");
            DebugConsole.Log(this, $"Valid materials: {string.Join(", ", validPlacementMaterials.Select(m => m?.name))}");

            bool contains = false;
            if (!checkIsReboundMaterial) contains = validPlacementMaterials.Contains(hitMaterial);
            else contains = validReboundMaterials.Contains(hitMaterial);
            DebugConsole.Log(this, $"Material is valid: {contains}");
        }

        if (rend.sharedMaterials.Length <= subMeshIndex)
        {
            DebugConsole.Log(this, "fail 4");
            return false;
        }

        var finalResult = false;
        if (!checkIsReboundMaterial) finalResult = subMeshIndex == -1 || validPlacementMaterials.Contains(rend.sharedMaterials[subMeshIndex]);
        else  finalResult = subMeshIndex == -1 || validReboundMaterials.Contains(rend.sharedMaterials[subMeshIndex]);
        DebugConsole.Log(this, $"Final result: {finalResult}");
        return finalResult;
    }
    
    /// <summary>
    /// Used by GetIsValidTarget to get the index of the tri that was hit on a mesh
    /// (So GetIsValidTarget can check for valid placement materials)
    /// </summary>
    [Todo("Can someone fact check me on this function's summary? ~Liz", TodoSeverity.Minor)]
    private int GetSubMeshIndex(Mesh mesh, int triIndex)
    {
        int triangleCounter = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            triangleCounter += mesh.GetSubMesh(i).indexCount / 3;
            if (triIndex < triangleCounter)
            {
                return i;
            }
        }
        return -1;
    }

    #endregion
}
