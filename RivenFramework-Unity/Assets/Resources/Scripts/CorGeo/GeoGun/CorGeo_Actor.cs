//==========================================( Neverway 2025 )=========================================================//
// Author
//  Connorses
//
// Contributors
//  Liz M.
//
//====================================================================================================================//

using System;
using RivenFramework;
using UnityEngine;

public class CorGeo_Actor : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Check this if the actor can move around")]
    [SerializeField] public bool dynamic = false;
    [Tooltip("Set this true if the actor is a child of something, and shouldn't be moved by the rift")]
    [SerializeField] public bool isParentedIgnoreOffsets = false;
    [Tooltip("If enabled, this object will not be disabled in a fully collapsed null-space")]
    [SerializeField] public bool activeInNullSpace = false;
    [Tooltip("Uncheck this if the object has a special death animation")]
    [SerializeField] public bool destroyedInKillTrigger = true;
    
    //todo: Either reimplement crushInNullSpace, or get rid of it. I'm considering replacing it with something like "dynamicCrushable" since it only applies to dynamic actors anyway.
    //[Tooltip("If true, this actor gets distorted when inside nullspace, for example a cube that's not held by player")]
    //[SerializeField] public bool crushInNullSpace = true;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public event Action OnRiftRestore;
    [Tooltip("If enabled, this object will print logs for which 'space' it's currently in when a rift is active")]
    public bool debugLogSpaceData;

    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    [Header("Debugging")]
    [Tooltip("Used to restore static actors back to their initial position when uncollapsing rifts")]
    [SerializeField] private Vector3 homePosition;
    [Tooltip("Used to restore static actors back to their initial scale when uncollapsing rifts")]
    [SerializeField] private Vector3 homeScale;
    [Tooltip("Used to restore static actors back to their initial parent object when uncollapsing rifts")]
    [SerializeField] public Transform homeParent;
    [Tooltip("Enabled when an object is picked up by a pawn, this prevents the object from being moved during rift movements, otherwise the object would be pulled out of their hands")]
    [SerializeField] public bool isHeld = false;
    [Tooltip("Used to keep track of if this game object should be re-enabled in the hierarchy when resetting rifts")]
    public bool wasActive;
    [Tooltip("Used during space assignment for the space controller dictionary")]
    public RiftSpace riftSpace;
    [Tooltip("The velocity of the object before it was frozen by a rift movement")]
    private Vector3 previousVelocity;
    
    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    new private Rigidbody rigidbody;
    [Tooltip("Reference to the riftManager so the actor can sort themselves into the manager's correct space lists")]
    private RiftManager riftManager;

    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start ()
    {
        // Find references
        rigidbody = GetComponent<Rigidbody>();
        riftManager = FindObjectOfType<RiftManager>();
        // Store initial transform data about this object, so it can be restored later when rifts are reset
        wasActive = gameObject.activeInHierarchy;
        homePosition = transform.position;
        homeScale = transform.localScale;
        homeParent = transform.parent;
        RiftManager_ActorHandler.CorGeo_Actors.Add(this);
        // Automatically avoid hiding lights when a rift is collapsed
        if (TryGetComponent<Light> (out Light light))
        {
            activeInNullSpace = true;
        }
    }
    
    private void OnDestroy ()
    {
        // Cleanly remove this from the list of tracked actors on the GeoGun when destoryed
        RiftManager_ActorHandler.CorGeo_Actors.Remove(this);
    }

    
    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// 
    /// </summary>
    public void Freeze ()
    {
        if (!rigidbody) return;

        previousVelocity = rigidbody.velocity;
        rigidbody.isKinematic = true;
    }

    /// <summary>
    /// 
    /// </summary>
    public void UnFreeze ()
    {
        if (!rigidbody) return;

        rigidbody.isKinematic = false;
        rigidbody.velocity = previousVelocity;
    }
    
    
    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Called by the GeoGun when a rift is reset
    /// This resets the actors back to the transform state they were in prior to being affected by a rift.
    /// It also moves dynamic actors relative to rift-space
    /// </summary>
    public void GoHome ()
    {
        OnRiftRestore?.Invoke();
        if (isParentedIgnoreOffsets) return;
        transform.SetParent (homeParent);
        transform.localScale = homeScale;
        if (dynamic)
        {
            riftSpace = RiftSpace.none;
            return;
        }
        gameObject.SetActive(true); //todo: make the special cases where this SetActive doesn't apply
        transform.position = homePosition;
        riftSpace = RiftSpace.none;
    }
    
    /// <summary>
    /// Finds which space (A/B/Null) the actor is in and sets the actor's space variable accordingly.
    /// </summary>
    public void DetermineRiftSpace ()
    {
        if (!riftManager) riftManager = FindObjectOfType<RiftManager>();
        // I am very bad at math, pls don't delete my helper example ~Liz
        /*
        // The plane
        Vector3 planePosition = new Vector3(0,0,0); // Where the plane is at in the world
        Vector3 planeNormal = new Vector3(0,0,1); // The direction perpendicular to the plane

        // Test point
        Vector3 testPosition = new Vector3(0,0,5); // The position of the object we want to test

        // Calculations
        var dotProductOfNormalToPoint = Vector3.Dot(planeNormal, testPosition);
        var distanceToPoint = dotProductOfNormalToPoint + planePosition.magnitude;

        if (distanceToPoint < 0)
        {
            print("Negative");
        }
        if (distanceToPoint > 0)
        {
            print("Positive");
        }
        else
        {
            print("Equal");
        }
        */
        
        // Sanity checks
        //if (!riftManager) riftManager = GameInstance.Get<RiftManager>();
        //riftManager.spaceController.spaceActors.Remove(this);

        // We ABSOLUTELY NEED to reference the distance using the VISUAL planes since the CUT planes never move, and this function is called while the rift is in motion
        // This doesn't need to be done with the meshes since that calculation is only performed when the rift is created
        // I am embarrassed to admit how long it took me to find this oversight ~Liz
        // ( PS Don't ask me to explain this "toOther" stuff, it was just in the Unity docs and the function doesn't work correctly without it)
        Vector3 toOther = Vector3.Normalize(transform.position - riftManager.geometryHandler.visualPlaneA.transform.position); 
        var distanceToPlaneA = Vector3.Dot(-riftManager.geometryHandler.visualPlaneA.transform.forward, toOther);
        toOther = Vector3.Normalize(transform.position - riftManager.geometryHandler.visualPlaneB.transform.position);
        var distanceToPlaneB = Vector3.Dot(-riftManager.geometryHandler.visualPlaneB.transform.forward, toOther);
        
        if (distanceToPlaneA > 0)
        {
            riftSpace = RiftSpace.A;
            
            if (debugLogSpaceData) { Debug.Log ("A Space"); }
        }
        else if (distanceToPlaneB > 0)
        {
            riftSpace = RiftSpace.B;
            if (debugLogSpaceData) { Debug.Log ("B Space"); }
        }
        else
        {
            riftSpace = RiftSpace.NULLSpace;
            if (debugLogSpaceData) { Debug.Log ("Null Space"); }
        }
        
        
        // Store in space actors list
        //riftManager.spaceController.spaceActors.Add(this, riftSpace);
    }

    /// <summary>
    /// What happens when this actor is trapped in a rift.
    /// </summary>
    public virtual void CollapseActor ()
    {
        if (activeInNullSpace == false)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// What happens when this actor was trapped in a rift, and the rift is opened.
    /// </summary>
    public virtual void UnCollapseActor ()
    {
        if (activeInNullSpace == false)
        {
            gameObject.SetActive (true);
        }
    }
    
    #endregion
}
