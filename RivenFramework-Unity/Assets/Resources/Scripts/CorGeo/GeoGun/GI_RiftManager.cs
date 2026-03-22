//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M., Connorses
//
// Contributors
//  Errynei, Soulex
//
// Notes
//  Rift Creation Sequence:
//      Update (Found two deployed markers)
//          Unhide rift
//          Position cut planes
//              Slice cut planes
//                  Cleanup extra mesh colliders
//                  Assign space container for meshes
//              Assign space for actors
//          Update rift state
//
// For anyone who has to fix or change something in this script, feel free to add a tick mark and move a chess piece
// Programmers Suffered: ||||
// ♜♝♞■♚♞♝♜
// ♟♟♟♟■♟♟♟
// □■□■♟■□■
// ■□■□■□♛□
// □■□■♙■□■
// ■□■□■□■□
// ♙♙♙♙□♙♙♙
// ♖♗♘♕♔♘♗♖

//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RivenFramework;
using UnityEngine;

/// <summary>
/// Finds pinned markers, does rift stuff, referenced by gun script to control rift movements
/// </summary>
public class GI_RiftManager : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    public static bool riftActive; // This is set to true when all of the rift initialization is complete and false when a rift is cleared

    //Amount the B-Space is currently offset from its starting position.
    //public static Vector3 currentRiftOffset;
    //Direction the rift space is facing (this is the line the rift moves along when expanding and contracting).
    public static Vector3 riftNormal;

    // Alright jeez here's your comments, are these descriptive enough? - Connorses

    //Tells things what state the rift is changing from.
    public static RiftState previousState = RiftState.None;
    //The current rift state  :O
    public static RiftState currentState = RiftState.None;

    // This event allows scripts to respond to any changes in the RiftState, such as the animated plane visuals or the rift audio effects.
    public delegate void StateChanged ();
    public static event StateChanged OnStateChanged;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private float maxRiftWidth = 30;  // Max size a rift can *expand* to in worldspace units.
    private float minRiftWidth = -30; // Max size an *inverted* rift can expand to in the negative direction.
    private float minAbsoluteRiftWidth = 0.15f; // This is to prevent physics bugs if nullspace scales too close to 0 without being 0.

    [HideInInspector] public static float currentRiftPercent; //current percent scaling of the rift (the local scale)
    [HideInInspector] public static float currentRiftWidth; //current width after applying percent scale
    [HideInInspector] public static float riftStartingWidth; //width of the rift when it was first placed
    private bool collapseHeld = false;
    private bool expandHeld = false;

    //Waits for you to release collapse so that the player has to press it again to collapse rift.
    //Prevents player from clicking to place the rift and holding the mouse causing it to collapse right away.
    private bool waitForCollapseReleased = false;
    
    private Vector3 riftNullSpacePosition; //The starting position of the null space container.
    private bool expandingRiftDueToCrush; // Overrides rift inputs to auto expand when the player is crushed


    //  rift movement speed stuff:  //
    [SerializeField] private float minRiftSpeed = 0.5f;
    [SerializeField] private float maxRiftSpeed = 6f;
    [SerializeField] private float riftAcceleration = 2f;
    [SerializeField] private float currentRiftMoveSpeed;

    // state stuff //
    private bool riftIsMoving;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [SerializeField] public Item_Utility_Geogun linkedGeogun;
    [SerializeField] private GameObject cutPlanePrefab, spaceContainerA, spaceContainerB, spaceContainerNull;
    [HideInInspector] public GameObject cutPlaneA, cutPlaneB;
    [HideInInspector] public static Plane planeA, planeB;
    [HideInInspector] public Projectile_Marker markerA, markerB;

    public List<GameObject> spaceAMeshes; 
    public List<GameObject> spaceBMeshes, spaceNullMeshes, hiddenOriginalMeshes, meshesToActivate;
    public Graphics_RiftPreviewEffects riftPreviewEffects;
    public Material nullSpaceMaterial;
    /// <summary>
    /// All actors currently in the scene
    /// </summary>
    private CrushDetector linkedCrushDetector;

    public static List<CorGeo_Actor> CorGeo_Actors = new List<CorGeo_Actor> { };

    public List<CorGeo_SliceableMesh> nonintersectedMeshes;

    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        StartCoroutine(LinkCrushDetector());
    }
    
    private void Update()
    {
        // Initialize rift objects if they are missing
        if (!IsRiftObjectsInitialized())
        {
            InitializeRiftObjects();
        }
        
        // Create or Remove rift when the markers are found or lost
        if (IsMarkersPinned() && !riftActive)
        {
            CreateRift();
        }
        else if (!IsMarkersPinned() && riftActive)
        {
            RestoreRift();
        }
        
        
        // Input stuff?
        if (waitForCollapseReleased && !collapseHeld)
        {
            waitForCollapseReleased = false;
        }

        // Activated rift moving stuff
        riftIsMoving = false;
        if (riftActive)
        {
            if (expandingRiftDueToCrush)
            {
                MoveRiftByDistance (maxRiftSpeed * Time.deltaTime);
                UpdateState (RiftState.Expanding);
            }
            else if (collapseHeld && waitForCollapseReleased == false)
            {
                MoveRiftByDistance (-currentRiftMoveSpeed * Time.deltaTime);
                AccelerateRift ();
                UpdateState (RiftState.Collapsing);
            }
            else if (expandHeld)
            {
                MoveRiftByDistance (currentRiftMoveSpeed * Time.deltaTime);
                AccelerateRift ();
                UpdateState (RiftState.Expanding);
            }
            else
            {
                currentRiftMoveSpeed = 0;
                riftIsMoving = false;
                if (currentState != RiftState.Preview)
                {
                    UpdateState (RiftState.Idle);
                }
            }
        }
    }

    private void AccelerateRift ()
    {
        currentRiftMoveSpeed += riftAcceleration * Time.deltaTime;
        if (currentRiftMoveSpeed > maxRiftSpeed)
        {
            currentRiftMoveSpeed = maxRiftSpeed;
        }
    }

    private void OnDestroy()
    {
        //print($"{gameObject.name} THIS INSTANCE OF RIFT MANAGER WAS DESTROYED!");
        //RestoreRift();
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    // --== CALLED FROM START ==-- //
    /// <summary>
    /// Set up the player's crush detector so it can expand the rift when crushed
    /// </summary>
    private IEnumerator LinkCrushDetector()
    {
        while (!linkedCrushDetector)
        {
            var player = GameInstance.Get<GI_PawnManager>().localPlayerCharacter;
            if (player) linkedCrushDetector = player.GetComponent<CrushDetector>();
            yield return new WaitForEndOfFrame();
        }
        
        // Assign the listener so if the player gets crushed the rift will backoff slightly
        linkedCrushDetector.onCrushed.AddListener (() => StartCoroutine(InterruptRiftCollapse (0.15f)));
    }
    /// <summary>
    /// Used for stopping rift collapse when getting crushed
    /// </summary>
    private IEnumerator InterruptRiftCollapse (float delay)
    {
        if (!collapseHeld) yield break;

        collapseHeld = false; // release close rift input
        //ignoreRiftInputAfterCrush = true;

        expandingRiftDueToCrush = true;
        yield return new WaitForSeconds (delay);
        expandingRiftDueToCrush = false;
    }
    
    
    // --== CALLED FROM UPDATE ==-- //
    // Rift Initialization
    /// <summary>
    /// Detects if any of the rift objects are missing
    /// </summary>
    /// <returns>Returns false if any of the rift object references are null</returns>
    private bool IsRiftObjectsInitialized()
    {
        return cutPlaneA && cutPlaneB && spaceContainerA && spaceContainerB && spaceContainerNull;
    }
    
    /// <summary>
    /// Creates the cut planes and containers, then hides them until they are ready for use
    /// </summary>
    private void InitializeRiftObjects()
    {
        // The objects used for the rift are created when the level loads
        // They are never destroyed, only hidden or unhidden
        // This is done this way to avoid lag spikes caused by having to constantly spawn and despawn the rift objects
        CreateCutPlanes();
        CreateSpaceContainers();
        SetRiftPlanesHidden(true);
    }
    
    /// <summary>
    /// Spawn the planes used for the cutting of the world as well as the visuals for previewing the cut
    /// </summary>
    private void CreateCutPlanes()
    {
        cutPlaneA = Instantiate(cutPlanePrefab, null);
        cutPlaneB = Instantiate(cutPlanePrefab, null);
        cutPlaneA.name = "CutPlaneA";
        cutPlaneB.name = "CutPlaneB";
    }    
    
    /// <summary>
    /// Spawn the empty game objects that represent the space that matter exists in while a rift is active
    /// These objects are used to scale and reposition all objects at once, according to the space they occupy
    /// </summary>
    private void CreateSpaceContainers()
    {
        var spaceContainer = new GameObject();
        spaceContainer.name = "ASpace";
        spaceContainerA = spaceContainer;
        spaceContainer = new GameObject();
        spaceContainer.name = "BSpace";
        spaceContainerB = spaceContainer;
        spaceContainer = new GameObject();
        spaceContainer.name = "NullSpace";
        spaceContainerNull = spaceContainer;
    }
    
    /// <summary>
    /// Toggles the visibility of the cut planes, used for hiding/showing the rift objects when the rift is deactivated/activated
    /// </summary>
    private void SetRiftPlanesHidden(bool _hidden)
    {
        //riftActive = !_hidden;
        cutPlaneA.SetActive(!_hidden);
        cutPlaneB.SetActive(!_hidden);
    }
    
    // Rift Creation & Removal
    /// <summary>
    /// Assigns the 'markerA/B' references to the first pinned marks found
    /// </summary>
    /// <returns>Returns true if two pinned markers are found and assigned</returns>
    private bool IsMarkersPinned()
    {
        return (markerA != null && markerB != null);
    }

    /// <summary>
    /// Helper function to avoid possible flag issues caused by coroutine delegations
    /// (It basically just sets the riftActive flag (that stops the CreateRift func from multi-fire))
    /// </summary>
    private void CreateRift()
    {
        riftActive = true;
        StartCoroutine(CoCreateRift());
    }

    /// <summary>
    /// Call the functions needed to create the rift
    /// </summary>
    private IEnumerator CoCreateRift()
    {
        SetRiftPlanesHidden(false);
        //StartCoroutine(riftPreviewEffects.OnRiftCreated(this));
        UpdateState (RiftState.Preview);
        
        // Operations for rift creation
        PositionCutPlanes();
        yield return SliceCutPlanes();
        
        // TODO Replace the find objects every time with a single list that contains ALL sliceables, and any new sliceable should add itself to that list, any destroyed should remove themselves
        var allSliceableMeshes = FindObjectsOfType<CorGeo_SliceableMesh>(true).ToList();
        foreach (var sliceableMesh in allSliceableMeshes)
        {
            sliceableMesh.AssignMeshToSpaceLists();
        }
        
        CleanupExtraMeshColliders();
        ReparentMeshesToSpaceContainer();
        yield return SwitchToSlicedMeshesOnDelay();
        ReparentActorsToSpaceContainer();
        

        /*
        // Sort the remaining, unsliced meshes into the correct space containers
        nonintersectedMeshes = FindObjectsOfType<CorGeo_SliceableMesh>().ToList();
        foreach (var sliceableMesh in nonintersectedMeshes)
        {
            sliceableMesh.AssignMeshToSpaceContainer();
        }

        // Clean up glitched duplicate mesh colliders that sometimes appear on sub-cuts
        StartCoroutine(CleanupExtraMeshCollidersCleanupExtraMeshColliders());

        StartCoroutine(AssignSpaceContainerForMeshes());

        waitForCollapseReleased = true;

        StartCoroutine (SwitchToSlicedObjectsOnDelay ());

        // DO NOT set the rift to being active until we are sure that all of the other rift init stuff is done
        // This is one of the last functions that's called in that chain, so hopefully just waiting until the
        // end of the frame should give it time to complete. ~Liz
        yield return new WaitForEndOfFrame();
        StartCoroutine(AssignSpaceForActors());
        */
    }

    /// <summary>
    /// Moves the cut planes to the position of the markers
    /// </summary>
    private void PositionCutPlanes()
    {
        // Set the positions and rotations of the cut plane objects
        cutPlaneA.transform.position = markerA.transform.position;
        cutPlaneB.transform.position = markerB.transform.position;
        
        cutPlaneA.transform.LookAt(markerB.transform);
        cutPlaneB.transform.LookAt(markerA.transform);
        
        // Assign the mathematical plane values
        planeA = new Plane(cutPlaneA.transform.forward, cutPlaneA.transform.position);
        planeB = new Plane(cutPlaneB.transform.forward, cutPlaneB.transform.position);

        //Place the Space Containers at the edges of the rift.
        spaceContainerNull.transform.position = cutPlaneA.transform.position;
        spaceContainerB.transform.position = cutPlaneB.transform.position;
        //Aim spaceContainerNull so that when we scale it, it will squish parallel to the rift planes.
        spaceContainerNull.transform.LookAt (cutPlaneB.transform.position);
        //Initialize the rift measurements
        riftStartingWidth = Vector3.Distance(cutPlaneA.transform.position, cutPlaneB.transform.position);
        
        currentRiftPercent = 1;
        currentRiftWidth = riftStartingWidth;

        // I'm preserving this position because negative scaling moves the object. ~Connorses
        riftNullSpacePosition = spaceContainerNull.transform.position;

        // Saves the direction the rift is facing so we can easily reference it.
        riftNormal = spaceContainerNull.transform.forward;
    }

    /// <summary>
    /// Makes the initial cuts at the positions of the two cut planes
    /// </summary>
    private IEnumerator SliceCutPlanes()
    {
        meshesToActivate = new List<GameObject> ();

        var intersectedMeshes = new HashSet<CorGeo_SliceableMesh>();
        
        // Separate and slice intersected meshes
        //intersectedMeshes.UnionWith(CorGeo_PlaneIntersectionUtil.GetIntersectingMeshes(planeA));
        //intersectedMeshes.UnionWith(CorGeo_PlaneIntersectionUtil.GetIntersectingMeshes(planeB));
        
        foreach (var intersectedMesh in intersectedMeshes)
        {
            intersectedMesh.ApplyCuts();
        }
        

        while (intersectedMeshes.Any((intersectedMesh) => intersectedMesh.isSliceInProgress))
        {
            yield return null;
        }
    }

    private IEnumerator SwitchToSlicedMeshesOnDelay()
    {
        yield return new WaitForEndOfFrame();
        foreach (var hiddenOriginalMesh in hiddenOriginalMeshes)
        {
            hiddenOriginalMesh.SetActive (false);
        }
        foreach (var mesh in meshesToActivate) 
        {
            if (mesh == null)
            {
                Debug.LogError ("null mesh was left in the list??");
                continue;
            }
            mesh.SetActive (true);
        }
    }

    /// <summary>
    /// Sometimes multi-cut meshes have an extra, broken, mesh collider as the first one in the index, this fixes those
    /// </summary>
    private void CleanupExtraMeshColliders()
    {
        foreach (var newMesh in spaceBMeshes)
        {
            var meshColliders = newMesh.GetComponents<MeshCollider>();
            if (meshColliders.Length > 1)
            {
                Destroy (meshColliders[0]);
            }
        }

        foreach (var newMesh in spaceNullMeshes)
        {
            var meshColliders = newMesh.GetComponents<MeshCollider>();
            if (meshColliders.Length > 1)
            {
                Destroy (meshColliders[0]);
            }
        }
    }

    /// <summary>
    /// Sets the parent for all the meshes in the lists to the correct space container
    /// </summary>
    private void ReparentMeshesToSpaceContainer()
    {
        foreach (var mesh in spaceAMeshes)
        {
            mesh.transform.parent = spaceContainerA.transform;
        }
        foreach (var mesh in spaceBMeshes)
        {
            mesh.transform.parent = spaceContainerB.transform;
        }
        foreach (var mesh in spaceNullMeshes)
        {
            mesh.transform.parent = spaceContainerNull.transform;
        }
    }
    
    /// <summary>
    /// Sorts all dynamic (moving/movable) actors into 'A', 'B', and 'Null' spaces
    /// </summary>
    private void ReparentActorsToSpaceContainer()
    {
        foreach (CorGeo_Actor actor in CorGeo_Actors)
        {
            actor.DetermineRiftSpace();
            if (actor.dynamic)
            {
                continue; //don't parent dynamic actors to the space-containers
            }
            if (actor.riftSpace == RiftSpace.B)
            {
                actor.transform.SetParent(spaceContainerB.transform);
                continue;
            }
            if (actor.riftSpace == RiftSpace.NULLSpace)
            {
                actor.transform.SetParent (spaceContainerNull.transform);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void EmptyMatterInSpaceContainers()
    {
     
        // There is a possibility that when calling this from OnDestroy, the spaceContainers don't exist, which is fine
        // This just exits if that's the case, so it doesn't throw a null error
        if (!spaceContainerA || !spaceContainerB || !spaceContainerNull) return;
        
        // Un-parent matter

        foreach (var mesh in spaceAMeshes)
        {
            mesh.transform.parent = null;
        }
        
        foreach (var mesh in spaceBMeshes)
        {
            mesh.transform.parent = null;
        }

        foreach (var mesh in spaceNullMeshes)
        {
            mesh.transform.parent = null;
        }
        
        /*
        for (int i = 0; i < spaceContainerA.transform.childCount; i++)
        {
            spaceContainerA.transform.GetChild(i).parent = null;
        }
        for (int i = 0; i < spaceBMeshes.Count; i++)
        {
            if (spaceContainerB.transform.GetChild(i).GetComponent<TestRef>())
            {
                print("I found the target!");
            }
            spaceContainerB.transform.GetChild(i).parent = null;
        }
        for (int i = 0; i < spaceContainerNull.transform.childCount; i++)
        {
            spaceContainerNull.transform.GetChild(i).parent = null;
        }*/
        
        // Clear lists
        spaceAMeshes.Clear();
        spaceBMeshes.Clear();
        spaceNullMeshes.Clear();
    }

    /// <summary>
    /// Cleans up cloned cut meshes and restores original meshes
    /// </summary>
    private void RestoreCutGeometry()
    {
        print("restoring cut geometry");
        var sliceableMeshes = FindObjectsOfType<CorGeo_SliceableMesh>();
        foreach (var sliceableMesh in sliceableMeshes)
        {
            if (sliceableMesh.isSlicedByPlane && !hiddenOriginalMeshes.Contains(sliceableMesh.gameObject))
            {
                sliceableMesh.UndoCuts();
            }
        }
        
        /*
        // Destroy cloned cut geometry
        var sliceableMeshes = FindObjectsOfType<CorGeo_SliceableMesh>();
        foreach (var sliceableMesh in sliceableMeshes)
        {
            if (sliceableMesh.isSlicedByPlane && !hiddenOriginalMeshes.Contains(sliceableMesh.gameObject))
            {
                Destroy(sliceableMesh.gameObject);
            }
        }

        // Un-hide the original meshes
        for (int i = 0; i < hiddenOriginalMeshes.Count; i++)
        {
            hiddenOriginalMeshes[i].SetActive(true);
        }
        
        hiddenOriginalMeshes.Clear();*/
    }

    /// <summary>
    /// Sets the rift back to it's zero point and restores cut geometry
    /// </summary>
    public void RestoreRift()
    {
        SetRiftPlanesHidden(true);
        UpdateState (RiftState.None);
        SetRiftPosition(1);
        RestoreCutGeometry();
        EmptyMatterInSpaceContainers();
        currentRiftMoveSpeed = 0;
        RestoreActors ();
    }

    private void RestoreActors ()
    {
        foreach (CorGeo_Actor actor in CorGeo_Actors)
        {
            actor.GoHome ();
        }

        riftActive = false;
    }
    

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Controls the collapsing and expanding of a deployed rift
    /// 1 is the start position (no compression/expansion), 0 is fully collapsed, and 2 is expanded to twice the distance of the rift planes
    /// </summary>
    /// <param name="_percent">The size of the rift relative to it's starting size.</param>
    public void SetRiftPosition(float _percent)
    {
        if (!cutPlaneB) return;

        if (linkedGeogun.collapseBehavior == Item_Utility_Geogun.CollapseBehavior.Default)
        {
            if (_percent <= 0)
            {
                CollapseRift ();
            }
            if (currentRiftPercent == 0 && _percent > 0){
                UnCollapseRift ();
            }
        }

        if (!cutPlaneB || !spaceContainerNull.activeInHierarchy) return;

        planeB = new Plane (cutPlaneB.transform.forward, cutPlaneB.transform.position);
        MoveActorsWithRift (_percent);
        currentRiftPercent = _percent;
        currentRiftWidth = riftStartingWidth * currentRiftPercent;
        MoveGeometryWithRift ();
    }

    //What happens when the rift is set to zero size
    private void CollapseRift ()
    {
        UpdateState (RiftState.Closed);
        DisableCollapsedObjects ();
        riftIsMoving = false;
    }

    //What happens when the rift was at zero size and was opened.
    private void UnCollapseRift ()
    {
        UpdateState (RiftState.Expanding);
        EnableCollapsedObjects ();
        riftIsMoving = true;
    }

    /// <summary>
    /// Changes the size of the rift by the specified number of units.
    /// </summary>
    /// <param name="distance"></param>
    [Todo("Max width section causes bug when rift is created with a big distance.", Owner = "connorses")]
    public void MoveRiftByDistance(float distance)
    {
        // Keep from expanding if allowExpandingRift is false
        if (!linkedGeogun.allowExpandingRift && currentRiftWidth + distance > riftStartingWidth)
        {
            distance = 0;
        }

        if (linkedGeogun.collapseBehavior == Item_Utility_Geogun.CollapseBehavior.Default)
        {
            if (distance < 0 && currentRiftWidth + distance < minAbsoluteRiftWidth)
            {
                SetRiftPosition (0);
                return;
            }
            if (distance > 0 && currentRiftWidth == 0)
            {
                SetRiftPosition(1/riftStartingWidth * minAbsoluteRiftWidth);
            }
        }

        if (currentRiftWidth + distance < minRiftWidth)
        {
            currentRiftWidth = minRiftWidth;
        }
        
        
        float percentChange = 1 / riftStartingWidth * distance;

        // Does anything use this value? ~Liz
        //currentRiftOffset = (currentRiftWidth - riftStartingWidth) * riftNormal;

        SetRiftPosition (currentRiftPercent + percentChange);
        riftIsMoving = true;
    }

    private void MoveGeometryWithRift()
    {
        if (!spaceContainerNull) return;

        // If the rift collapsed, ignore minimum size rule so that we don't have a gap.
        if (linkedGeogun.collapseBehavior == Item_Utility_Geogun.CollapseBehavior.Default && currentRiftPercent == 0)
        {
            spaceContainerB.transform.position = riftNullSpacePosition;
            cutPlaneB.transform.position = spaceContainerB.transform.position;
            return;
        }

        //  We use minAbsoluteRiftWidth to prevent the rift scale from getting too close to zero
        //  because collision mesh generation will bug out if the mesh is too skinny.

        float moddedRiftPercent = currentRiftPercent;

        if (currentRiftPercent < 0)
        {
            //Special case for negative rift scaling, where the rift can be mirrored.

            if (currentRiftWidth > -minAbsoluteRiftWidth)
            {
                moddedRiftPercent = 1 / riftStartingWidth * -minAbsoluteRiftWidth;
                currentRiftWidth = -minAbsoluteRiftWidth;
            }
            spaceContainerNull.transform.localScale = new Vector3 (1, 1, moddedRiftPercent);
            spaceContainerNull.transform.position = riftNullSpacePosition + spaceContainerNull.transform.forward * -currentRiftWidth;
            spaceContainerB.transform.position = spaceContainerNull.transform.position;
        }
        if (currentRiftPercent >= 0)
        {
            if (currentRiftWidth < minAbsoluteRiftWidth)
            {
                moddedRiftPercent = 1 / riftStartingWidth * minAbsoluteRiftWidth;
                currentRiftWidth = minAbsoluteRiftWidth;
            }
            spaceContainerNull.transform.localScale = new Vector3 (1, 1, moddedRiftPercent);
            spaceContainerB.transform.position = spaceContainerNull.transform.position + spaceContainerNull.transform.forward * currentRiftWidth;
            spaceContainerNull.transform.position = riftNullSpacePosition;
        }
        cutPlaneB.transform.position = spaceContainerB.transform.position;
    }

    private void MoveActorsWithRift (float _newPercent)
    {
        foreach (CorGeo_Actor actor in CorGeo_Actors)
        {
            if (actor.dynamic && actor.isHeld == false)
            {
                actor.DetermineRiftSpace ();
                if (actor.riftSpace == RiftSpace.NULLSpace)
                {
                    //print(actor.transform.position);
                    actor.transform.position = MovePositionWithNullSpace (actor.transform.position, _newPercent);
                }
                if (actor.riftSpace == RiftSpace.B)
                {
                    actor.transform.position = MovePositionWithBSpace (actor.transform.position, _newPercent);
                }
            }
        }
    }

    /// <summary>
    /// Update the state of the Rift and, if the new state is different, trigger OnStateChanged so things can respond to what he Rift is doing.
    /// </summary>
    private void UpdateState (RiftState _newState)
    {
        if (currentState == _newState)
        {
            return;
        }
        previousState = currentState;

        currentState = _newState;

        OnStateChanged?.Invoke ();
    }

    //todo: rework these a bit to use the rift size WITH the minimum size applied. Actors currently still move when the rift is in that weird min-size state.

    /// <summary>
    /// Calculate where an object in Null-Space should move to if the rift scales to the given percent.
    /// </summary>
    /// <param name="_position"></param>
    /// <param name="_newPercent"></param>
    /// <returns></returns>
    private Vector3 MovePositionWithNullSpace (Vector3 _position, float _newPercent)
    {
        //Calculate how far across null-space the transform is.
        float riftDistance = planeA.GetDistanceToPoint (_position);

        if (riftDistance == 0)
        {
            return _position;
        }

        float riftPercent = riftDistance / currentRiftWidth;
        //Calculate where the transform would be if null-space were not scaled.
        float newDistance = Mathf.Abs( riftPercent * (riftStartingWidth * _newPercent) );
        Vector3 answer = _position + ( riftNormal * (newDistance - riftDistance) );
        return answer;
    }

    /// <summary>
    /// Calculate where an object in B-Space should move to if the rift scales to the given percent.
    /// </summary>
    /// <param name="_position"></param>
    /// <param name="_newPercent"></param>
    /// <returns></returns>
    private Vector3 MovePositionWithBSpace (Vector3 _position, float _newPercent)
    {
        float offset = Mathf.Abs(riftStartingWidth*currentRiftPercent)-Mathf.Abs(riftStartingWidth * _newPercent);

        return _position - (riftNormal * offset);
    }
    
    /// <summary>
    /// Gets a reference to an unlinked Geogun in the scene so the rift manager can subscribe to the guns action events
    /// (Like clearing, collapsing, or expanding the rift)
    /// </summary>
    public void RegisterGeogun(Item_Utility_Geogun _linkedGeogun)
    {
        //if (linkedGeogun) return; // Sanity check to avoid multiple function calls // Sanity is overrated ~Present Liz
        if (_linkedGeogun.isLinkedToManager is false)
        {
            linkedGeogun = _linkedGeogun;
            linkedGeogun.isLinkedToManager = true;
            //linkedGeogun.OnGunDestroyMarkers += () => RestoreRift();
            linkedGeogun.OnCollapseHeld += () => collapseHeld = true;
            linkedGeogun.OnCollapseReleased += () => collapseHeld = false;
            linkedGeogun.OnExpandHeld += () => expandHeld = true;
            linkedGeogun.OnExpandReleased += () => expandHeld = false;
        }
    }

    private void DisableCollapsedObjects ()
    {
        foreach (GameObject g in spaceNullMeshes)
        {
            g.SetActive (false);
        }
        foreach (var actor in CorGeo_Actors)
        {
            if (actor.riftSpace == RiftSpace.NULLSpace)
            {
                actor.CollapseActor();
            }
        }
    }

    private void EnableCollapsedObjects ()
    {
        foreach (GameObject g in spaceNullMeshes)
        {
            g.SetActive (true);
        }
        foreach (var actor in CorGeo_Actors)
        {
            if (actor.riftSpace == RiftSpace.NULLSpace)
            {
                actor.UnCollapseActor ();
            }
        }
    }

    #endregion
}