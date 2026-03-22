//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M., Connorses, Errynei, Soulex
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using DG.Tweening;
using RivenFramework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles everything with creating, moving, and destroying a rift
/// </summary>
public class RiftManager : MonoBehaviour, ILoggable
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [field: SerializeField] public bool EnableRuntimeLogging { get; set; }
    
    [Header("RIFT SETTINGS")] 
    [Tooltip("Creates a rift when the two marker transform variables are set")]
    [SerializeField] private bool createRiftOnMarkersPinned;
    [Header("Size")]
    [Tooltip("Max size a rift can *expand* to in worldspace units")]
    [SerializeField] private float maxRiftWidth = 30;
    [Tooltip("Max size an *inverted* rift can expand to in the negative direction")]
    [SerializeField] private float minRiftWidth = -30;
    [Tooltip("This is to prevent physics bugs if nullspace scales too close to 0 without being 0")]
    [SerializeField] public static float minAbsoluteRiftWidth = 0.15f;
    [Header("Speed")]
    [Tooltip("The speed of the rift when it starts moving")]
    [SerializeField] public float minRiftSpeed = 0.5f;
    [Tooltip("The maximum speed of the rift when it moves")]
    [SerializeField] public float maxRiftSpeed = 6f;
    [Tooltip("How quickly the rift picks up in speed while moving")]
    [SerializeField] private float riftAcceleration = 2f;
    
    [Header("RIFT VISUALS")] 
    [Tooltip("The game object that is used to represent the visual planes of the rift")]
    public GameObject visualPlanePrefab;
    [Tooltip("The material used to represent geometry exposed by a total null collapse")]
    public Material nullSpaceMaterial;
    [Tooltip("The script that controls the rift preview effects")]
    public Graphics_RiftPreviewEffects riftPreviewEffects;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Header("CURRENT RIFT DATA")]
    [Tooltip("Width of the rift when it was first placed")]
    public static float riftStartingWidth;
    [Tooltip("Direction the rift space is facing (this is the line the rift moves along when expanding and contracting)")]
    public static Vector3 riftNormal;
    [Tooltip("The starting position of the null space container, used to restore its position after scaling the rift")]
    public static Vector3 riftNullSpaceStartingPosition;
    [Tooltip("Current percent scaling of the rift (the local scale)")]
    public static float currentRiftPercent;
    [Tooltip("Current width after applying percent scale")]
    public static float currentRiftWidth;
    [Tooltip("How fast the rift planes are currently moving")]
    public float currentRiftMoveSpeed;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private bool riftActive;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Header("HELPER CLASSES")] 
    [Tooltip("Handles the rift states")]
    [Box] public RiftManager_StateHandler stateHandler;
    [Tooltip("Controls space containers and rift movement")]
    [Box] public RiftManager_SpaceController spaceController;
    [Tooltip("Handles rift positioning and mesh slicing")]
    [Box] public RiftManager_GeometryHandler geometryHandler;
    [Tooltip("Handles actor restoring")]
    [Box] public RiftManager_ActorHandler actorHandler;
    
    [Header("REFERENCES")]
    [Tooltip("The positions where the rift planes will be created")]
    [HideInInspector] public Transform markerA, markerB;
    [Tooltip("The mathematical plane where the rift is cut")]
    [HideInInspector] public static Plane cutPlaneA, cutPlaneB;

    [Header("REFERENCES")] 
    [Tooltip("The script that is currently controlling this rift manager")]
    public RiftController linkedRiftController;

    [Tooltip("If either collapseHeld or expandHeld is enabled, the rift will attempt to move")]
    private bool collapseHeld, expandHeld;
    [Tooltip("When the crush detector is triggered, it sets this to true to expand the rift slightly (This is janky I know >:P ~Liz)")]
    public static bool expandDueToCrush = false;
    
    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        // Due to a circular dependency between geometryHandler and spaceController I have to wait to pass the reference here.
        // This is terrible and I hate it, but I suck at coding, so for now I guess it can stay ~Liz
        stateHandler = new RiftManager_StateHandler(this);
        spaceController = new RiftManager_SpaceController(this, null);
        geometryHandler = new RiftManager_GeometryHandler(this, spaceController);
        spaceController.geometryHandler = geometryHandler;
        actorHandler = new RiftManager_ActorHandler(this);
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void FixedUpdate()
    {
        // Create rift when markers pinned
        if (createRiftOnMarkersPinned && IsMarkersPinned() && stateHandler.IsState<RiftState_None>())
        {
            CreateRift(markerA, markerB);
        }
        
        // Erase rift when marker transforms are destroyed
        else if (createRiftOnMarkersPinned && !IsMarkersPinned() && !stateHandler.IsState<RiftState_None>())
        {
            DestroyRift();
        }

        // Handle rift inputs
        // Don't try to switch states if the rift is being destroyed
        // This is very likely gonna cause a code fire, but that's a problem for future me >:P ~Liz 
        stateHandler.Update();
        if (stateHandler.currentState.GetType() == typeof(RiftState_DestroyRestoring) ||
            stateHandler.currentState.GetType() == typeof(RiftState_Destroy))
        {
            return;
        }
        UpdateState();
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Checks to see if two valid anchor points are present for the rift to generate
    /// </summary>
    /// <returns></returns>
    private bool IsMarkersPinned()
    {
        return (markerA != null && markerB != null);
    }

    /// <summary>
    /// Sets the state machine state based on the rift controller inputs
    /// </summary>
    private void UpdateState()
    {
        if (!riftActive) return;

        // Expand due to crush
        if (expandDueToCrush) { stateHandler.SetState<RiftState_ExpandingFromCrush>(); } 
        // Collapsing
        else if (collapseHeld) { stateHandler.SetState<RiftState_Collapsing>(); }
        // Expanding
        else if (expandHeld) { stateHandler.SetState<RiftState_Expanding>(); }
        // Idling
        else { stateHandler.SetState<RiftState_Idle>(); }
    }
    
    /// <summary>
    /// Increase the rift's movement speed while it's being moved
    /// </summary>
    private void AccelerateRift ()
    {
        currentRiftMoveSpeed += riftAcceleration * Time.deltaTime;
        if (currentRiftMoveSpeed > maxRiftSpeed)
        {
            currentRiftMoveSpeed = maxRiftSpeed;
        }
    }

    /// <summary>
    /// Unslice the world and remove objects from space containers
    /// </summary>
    private void DestroyRift()
    {
        //GameInstance.Get<GI_ReplayEventTimeline>().RecordThisEvent(this);
        
        riftActive = false;
        this.Log("DestroyRift called");
        if (stateHandler.currentState.GetType() != typeof(RiftState_DestroyRestoring) && stateHandler.currentState.GetType() != typeof(RiftState_Destroy))
        {
            stateHandler.SetState<RiftState_DestroyRestoring>();
        }
    }

    /// <summary>
    /// This ensures a rift does not persist across scene changes, since currently the space containers and other
    /// references are not persistent and get destroyed when changing levels
    /// Liz: This doesn't need to handle destroying markers since the geogun will get recreated on level restart
    /// </summary>
    private void OnSceneChanged(Scene current, Scene next)
    {
        stateHandler.SetState<RiftState_Destroy>();
    }
    

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Slice the world and assign all objects to space containers
    /// </summary>
    public async void CreateRift(Transform _markerA, Transform _markerB)
    {
        //GameInstance.Get<GI_ReplayEventTimeline>().RecordThisEvent(this, new object[]{ _markerA, _markerB });
        
        this.Log($"CreateRift called (_markerA: '{_markerA}', _markerB: '{_markerB}')");
        stateHandler.SetState<RiftState_Preview>();
        geometryHandler.SetRiftPlanesVisible(true);
        geometryHandler.PositionCutPlanes(_markerA, _markerB);
        await geometryHandler.PerformCutProcedure();
        spaceController.ReparentGeometryToSpaceContainers();
        spaceController.ReparentActorsToSpaceContainers();
        riftActive = true;
    }

    /// <summary>
    /// Unslice the world and remove objects from space containers
    /// This version is used by the rift gun controller so that it properly cleans up its markers
    /// </summary>
    public void DestroyRiftExternal()
    {
        if (markerA)
        {
            Destroy(markerA.gameObject);
        }
        if (markerB)
        {
            Destroy(markerB.gameObject);
        }
        stateHandler.SetState<RiftState_Destroy>();
    }

    /// <summary>
    /// Lerp the movement of the rift and space containers by a specified amount in meters
    /// </summary>
    public void MoveRiftByDistance(float _distance)
    {
        //GameInstance.Get<GI_ReplayEventTimeline>().RecordThisEvent(this, new object[]{ _distance });
        
        this.Log($"MoveRiftByDistance called (_distance: '{_distance}')");
        
        AccelerateRift();
        
        // Keep from expanding if allowExpandingRift is false
        if (!linkedRiftController.allowExpandingRift && currentRiftWidth + _distance > riftStartingWidth)
        {
            _distance = riftStartingWidth - currentRiftWidth;
            if (_distance == 0)
            {
                return;
            }
        }

        if (linkedRiftController.collapseBehavior == Item_Utility_Geogun.CollapseBehavior.Default)
        {
            if (_distance < 0 && currentRiftWidth + _distance < minAbsoluteRiftWidth)
            {
                SetRiftPercentage(0);
                return;
            }
            if (_distance > 0 && currentRiftWidth == 0)
            {
                SetRiftPercentage(1/riftStartingWidth * minAbsoluteRiftWidth);
                return;
            }
        }

        if (currentRiftWidth + _distance < minRiftWidth)
        {
            currentRiftWidth = minRiftWidth;
        }
        
        
        float percentChange = 1 / riftStartingWidth * _distance;

        // Does anything use this value? ~Liz
        //currentRiftOffset = (currentRiftWidth - riftStartingWidth) * riftNormal;

        SetRiftPercentage (currentRiftPercent + percentChange);
        // This value is probably not needed anymore for the new system
        //riftIsMoving = true;
    }

    /// <summary>
    /// Force set the exact percentage of rift collapse
    /// </summary>
    /// <param name="_distance">0 = closed rift, 1 = starting percentage, 2 = double expansion, -1 = mirrored rift</param>
    public void SetRiftPercentage(float _distance)
    {
        this.Log($"SetRiftPercentage called (_distance: '{_distance}')");
        
        // If the collapse behaviour of the rift controller is set to default, handle full-collapsing and uncollapsing of null-space
        if (linkedRiftController)
        {
            if (linkedRiftController.collapseBehavior == Item_Utility_Geogun.CollapseBehavior.Default)
            {
                if (_distance <= 0)
                {
                    stateHandler.SetState<RiftState_Closed>();
                }
                if (currentRiftPercent == 0 && _distance > 0)
                {
                    stateHandler.SetState<RiftState_Opened>();
                }
            }
        }
        
        // Some sort of fallback to avoid a bug... I know I added this here for some important reason, I'm sure ~Liz
        if (!geometryHandler.visualPlaneB || !spaceController.spaceContainerNull)
        {
            Debug.LogWarning($"Attempted to set rift percentage, but the space containers were missing! spn = {spaceController.spaceContainerNull}");
            return;
        }
        
        // Apply movement offsets to actors and geometry
        // The order of operations here is very important!
        currentRiftPercent = _distance;
        spaceController.MoveActorsWithRift(_distance);
        
        // For the love of dog, please leave this line as the last thing this function does
        // Otherwise the spaceController doesn't know the distance change to move the actors ~Liz
        currentRiftWidth = riftStartingWidth * currentRiftPercent;
        
        // Actually, this line needs to be last to avoid the geometry being one step behind the actual rift percentage
        // That one-step delay is fine for the actors though, since it's nearly unnoticeable ~Liz
        spaceController.MoveGeometryWithRift();
    }

    /// <summary>
    /// Assign a controller, like the Geogun, to control the rift manager
    /// </summary>
    public void RegisterRiftController(RiftController _linkedRiftController)
    {
        this.Log($"RegisterRiftController called (_linkedRiftController: '{_linkedRiftController}')");
        linkedRiftController = _linkedRiftController;
        //linkedRiftController.isLinkedToManager = true; 
        linkedRiftController.OnCollapseHeld += () => collapseHeld = true;
        linkedRiftController.OnCollapseReleased += () => collapseHeld = false;
        linkedRiftController.OnExpandHeld += () => expandHeld = true;
        linkedRiftController.OnExpandReleased += () => expandHeld = false;
    }

    
    
    
    
    
    
    
    
    
    
    
    
    // TEMP TEMP TEMP TEMP TEMP TEMP TEMP TEMPE?
    // Labirhin reference?
    /*
                             / |  | \        
                            /  |  |  \       
    _______________________/   |__|   \______
    |        _____     ___     _____         |
    |     .__\   /__   \ /   __\   /__.      |
    |   ...\/.\ /.\/...   ...\/.\ /.\/...    |
    |  ...................................   |
    |  ...................................   |
    |   .../\./ \./\...   .../\./ \./\...    |
    |     .--/   \--         --/   \--.      |
    |        -----             -----         |
    ------------------------------------------
    */
    #endregion

}
