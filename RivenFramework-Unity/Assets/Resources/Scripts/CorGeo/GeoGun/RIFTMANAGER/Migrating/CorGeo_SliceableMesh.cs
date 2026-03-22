//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M. & Connorses
//
// Contributors
//  Errynei
//
// Notes: This code was "Super Expertly Adapted" from the source code created by
//          @DitzelGames on YouTube. (See source)
//          Also thanks to Connorses for helping fix the bridgeMeshGaps function
//          and for putting up with my crazed rambling about polygons. ~Liz
//
// Notes (Rework 1): The code was rewritten by Connorses to use the BzMeshslicer instead of the custom system ~Liz
//
// Notes (Rework 2): I have rewritten the code to work with the ground up rebuild of the project. It currently does not 
//          Handel any of the logic for supporting sliceable trigger volumes as I don't fully understand that system
//          yet. ~Liz
//      
// Source: https://www.youtube.com/watch?v=VwGiwDLQ40A
//
//====================================================================================================================//

using System.Collections.Generic;
using UnityEngine;
using BzKovSoft.ObjectSlicer;
using System;
using System.Collections;
using System.Threading.Tasks;
using RivenFramework;
using Sabresaurus.SabreCSG;
using ErryLib.MonoTasks;
using Neverway.Framework;

/// <summary>
/// Added to meshes to allow them to be sliced by the geogun
/// </summary>
[RequireComponent (typeof (BzSliceableObject))]
public class CorGeo_SliceableMesh : MonoBehaviour, ILoggable
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [field: SerializeField] public bool EnableRuntimeLogging { get; set; }
    

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("If enabled, lasers are reflected off this mesh")]
    public bool isReflective;
    
    [Tooltip("Used to identify when a slice operation is being performed")]
    public bool isSliceInProgress;
    [Tooltip("Used by the rift manager's geometry handler to identify which meshes are ones that were cut by a rift plane")]
    public bool isSlicedByPlane;
    [Tooltip("Used to identify cut chunks that will be removed when undoing a cut")]
    public bool isClone;
    [Tooltip("Used during space assignment for the space controller dictionary")]
    public RiftSpace riftSpace;
    

    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("The BZSlicer script that actually cuts the mesh")]
    private BzSliceableObject slicer;
    [Tooltip("The data that the BZSlicer returns when cutting the mesh")]
    private IBzMeshSlicer sliceData;
    [Tooltip("Reference to the riftManager so the cut meshes can sort themselves into the manager's correct space lists")]
    private RiftManager riftManager;
    [Tooltip("Reference to the mesh renderer so the intersection util can quickly check its overlaps with rift planes")]
    [HideInInspector] public MeshRenderer meshRenderer;
    [Tooltip("The history of how this object has been cut, used for undoing cuts when a rift is destroyed")]
    private Stack<UndoSliceState> sliceHistory = new();
    [Tooltip("Reference to the original object that contains the slice history")]
    private CorGeo_SliceableMesh originalSliceableObject;
    
    public struct SliceResultChunks
    {
        public bool isSliced;
        public CorGeo_SliceableMesh positiveChunk;
        public CorGeo_SliceableMesh negativeChunk;

        public void FinalizeResult()
        {
            if (isSliced)
            {
                positiveChunk.FinishSlice();
                negativeChunk.FinishSlice();
            }
            else
            {
                negativeChunk.FinishSlice();
            }
        }
    }
    
    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        // Get internal references
        slicer = GetComponent<BzSliceableObject>();
        riftManager = GameInstance.Get<RiftManager>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        // If no slicing material was defined, assign the null space material from the rift manager
        if (!slicer.defaultSliceMaterial)
        {
            slicer.defaultSliceMaterial = riftManager.nullSpaceMaterial;
        }
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Save the state of the mesh prior to being cut
    /// </summary>
    private void SaveUndoSnapshot()
    {
        // Add a new slice state to the undo stack
        UndoSliceState state = new UndoSliceState();

        // Save the current name
        state.meshName = gameObject.name;
        
        // Save the mesh data
        var meshFilter = GetComponent<MeshFilter>();
        state.originalMesh = Instantiate(meshFilter.sharedMesh);
        var meshRenderer = GetComponent<MeshRenderer>();
        
        // Save the materials
        var originalMaterials = meshRenderer.sharedMaterials;
        state.materials = new Material[originalMaterials.Length];
        Array.Copy(originalMaterials, state.materials, originalMaterials.Length);

        // Save the colliders
        var colliders = GetComponents<Collider>(); 
        state.colliders = new UndoSliceState.ColliderData[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            // TRY SAVING AS MESH COLLIDER
            if (colliders[i].GetType() == typeof(MeshCollider))
            {
                var meshCollider = (MeshCollider)colliders[i];
                state.colliders[i] = new UndoSliceState.ColliderData
                {
                    colliderType = ColliderCollisionType.mesh,
                    mesh = meshCollider.sharedMesh is not null ? Instantiate(meshCollider.sharedMesh) : null,
                    convex = meshCollider.convex,
                    isTrigger = meshCollider.isTrigger,
                };
            }
            // TRY SAVING AS BOX COLLIDER
            else if (colliders[i].GetType() == typeof(BoxCollider))
            {
                var boxCollider = (BoxCollider)colliders[i];
                state.colliders[i] = new UndoSliceState.ColliderData
                {
                    colliderType = ColliderCollisionType.box,
                    center = boxCollider.center,
                    size = boxCollider.size,
                    convex = true,
                    isTrigger = boxCollider.isTrigger,
                };
            }
            else
            {
                Debug.LogError("Unknown or unsupported collider type on object: " + colliders[i].name + "! This is Future Liz's fault! Go tell them to fix the stupid rift collider implementations already! ~Past Liz");
            }
        }
        
        // Save the transform
        state.transformData.position = transform.position;
        state.transformData.rotation = transform.rotation;
        state.transformData.scale = transform.lossyScale;
        
        // Okay, all done, actually add this data onto the stack now
        sliceHistory.Push(state);
    }

    /// <summary>
    /// Attempts to cut this mesh, and any of its subsequent slice chunks, across the rift planes
    /// </summary>
    private async void AttemptSliceRiftPlanes()
    {
        // Mark the start of a slice operation (this will get set false when we are done
        // (unless there is a critical failure (which happens a lot (sry)))) ~Liz
        isSliceInProgress = true;
        // Mark everything as a clone (this will be set to false for the original that we keep later)
        //isClone = true;
        // Store a reference to the original object that holds the slice history
        var originalObject = this;
        
        // DO THE SLICEY THING!!!
        DebugConsole.Log(this, $"[START] Slicing {gameObject.name}");
        var sliceResultOfAPlane = await AttemptSlice(RiftManager.cutPlaneA);
        DebugConsole.Log(this, $"[PLANE A] isSliced: {sliceResultOfAPlane.isSliced}");
        if (sliceResultOfAPlane.isSliced)
        {
            DebugConsole.Log(this, $"  Positive: {sliceResultOfAPlane.positiveChunk?.gameObject.name}");
            DebugConsole.Log(this, $"  Negative: {sliceResultOfAPlane.negativeChunk?.gameObject.name}");
        }
        await For.NextFrame; // THIS IS VERY IMPORTANT TO AVOID ASYNC SLICE COLLISIONS (THANK YOU ERRYNEIIIIIII)
        var sliceResultOfBPlane = await sliceResultOfAPlane.negativeChunk.AttemptSlice(RiftManager.cutPlaneB);
        DebugConsole.Log(this, $"[PLANE B] isSliced: {sliceResultOfBPlane.isSliced}");
        if (sliceResultOfBPlane.isSliced)
        {
            DebugConsole.Log(this, $"  Positive: {sliceResultOfBPlane.positiveChunk?.gameObject.name}");
            DebugConsole.Log(this, $"  Negative: {sliceResultOfBPlane.negativeChunk?.gameObject.name}");
        }

        
        // Assign spaces based on slice results, not geometry testing
        if (sliceResultOfAPlane.isSliced)
        {
            // Positive side of plane A = Space A
            sliceResultOfAPlane.positiveChunk.riftSpace = RiftSpace.A;
        
            if (sliceResultOfBPlane.isSliced)
            {
                // Positive side of plane B (but negative of A) = Space B
                sliceResultOfBPlane.positiveChunk.riftSpace = RiftSpace.B;
                // Negative side of both = NULL
                sliceResultOfBPlane.negativeChunk.riftSpace = RiftSpace.NULLSpace;
            }
            else
            {
                // Didn't slice on B, entire negative chunk of A is in NULL space
                sliceResultOfAPlane.negativeChunk.riftSpace = RiftSpace.NULLSpace;
            }
        }
        else if (sliceResultOfBPlane.isSliced)
        {
            // Didn't slice on A, so everything is on negative side of A
            sliceResultOfBPlane.positiveChunk.riftSpace = RiftSpace.B;
            sliceResultOfBPlane.negativeChunk.riftSpace = RiftSpace.NULLSpace;
        }
        else
        {
            Debug.LogWarning($"[CRIT] {gameObject.name} was determined to be intersecting with a rift plane, but all slices failed so rift space could not be determined!!!! <=(Oh crap that's bad!)");
            // Neither plane sliced - determine space for the unsliced mesh
            //originalObject.AssignMeshToSpaceLists();
        }
        
        
        
        // Find the actual original object
        CorGeo_SliceableMesh nonClone = null;
        
        
        // Check A slice results
        if (sliceResultOfAPlane.isSliced)
        {
            // Check if the positive chunk is the original
            if (sliceResultOfAPlane.positiveChunk == originalObject)
            {
                nonClone = sliceResultOfAPlane.positiveChunk;
            }
            // Otherwise check the negative side results
            else if (sliceResultOfBPlane.isSliced)
            {
                // One of these must be the original
                if (sliceResultOfBPlane.positiveChunk == originalObject)
                    nonClone = sliceResultOfBPlane.positiveChunk;
                else if (sliceResultOfBPlane.negativeChunk == originalObject)
                    nonClone = sliceResultOfBPlane.negativeChunk;
            }
            else
            {
                // B plane didn't slice, so negative chunk from A is the original
                if (sliceResultOfAPlane.negativeChunk == originalObject)
                    nonClone = sliceResultOfAPlane.negativeChunk;
            }
        }
        // A plane missed, Check B slice results
        else if (sliceResultOfBPlane.isSliced)
        {
            if (sliceResultOfBPlane.positiveChunk == originalObject)
                nonClone = sliceResultOfBPlane.positiveChunk;
            else if (sliceResultOfBPlane.negativeChunk == originalObject)
                nonClone = sliceResultOfBPlane.negativeChunk;
        }
        // Nothing was sliced, so the original was not changed
        else
        {
            nonClone = originalObject;
        }
        
        
        
        
        // Mark everything else as clones
        if (sliceResultOfAPlane.isSliced)
        {
            sliceResultOfAPlane.positiveChunk.isClone = (sliceResultOfAPlane.positiveChunk != nonClone);
            sliceResultOfAPlane.negativeChunk.isClone = (sliceResultOfAPlane.negativeChunk != nonClone);
        }
        if (sliceResultOfBPlane.isSliced)
        {
            sliceResultOfBPlane.positiveChunk.isClone = (sliceResultOfBPlane.positiveChunk != nonClone);
            sliceResultOfBPlane.negativeChunk.isClone = (sliceResultOfBPlane.negativeChunk != nonClone);
        }
        
        // Remove the non-clone from cut meshes list
        if (nonClone != null && riftManager.geometryHandler.cutMeshes.Contains(nonClone.gameObject))
        {
            riftManager.geometryHandler.cutMeshes.Remove(nonClone.gameObject);
        }
        
        // Ensure convex and mark the slice operation as completed for all chunks
        DebugConsole.Log(this, $"[FINALIZE] Calling FinalizeResult on plane A");
        sliceResultOfAPlane.FinalizeResult();
        DebugConsole.Log(this, $"[FINALIZE] Calling FinalizeResult on plane B");
        sliceResultOfBPlane.FinalizeResult();
    }
     
    /// <summary>
    /// Attempts to cut this mesh across a single plane
    /// </summary>
    /// <param name="_cutPlane">The plane to attempt to cut across</param>
    /// <returns>Returns result of the mesh slice</returns>
    public async Task<SliceResultChunks> AttemptSlice(Plane _cutPlane)
    {
        // Clones get AttemptSlice called before start or awake, so we have to force get components here
        //slicer = GetComponent<BzSliceableObject>();
        //sliceData = GetComponent<IBzMeshSlicer>();
        if (!riftManager) riftManager = GameInstance.Get<RiftManager>();

        slicer.asynchronously = true;
        
        // Attempt to slice Plane
        var sliceResult = await slicer.SliceAsync(_cutPlane, slicer);

        return ProcessSliceResult(sliceResult);
    }
    
    /// <summary>
    /// ???
    /// </summary>
    private SliceResultChunks ProcessSliceResult(BzSliceTryResult sliceResult)
    {
        SliceResultChunks result = new ();
        
        result.isSliced = sliceResult.sliced;

        // Slice was a miss, just do some bull shit
        if (!result.isSliced)
        {
            result.negativeChunk = this;
            return result;
        }
        
        // Find the chunk that hasn't been cut yet
        foreach (var cutChunk in sliceResult.resultObjects)
        {
            riftManager.geometryHandler.cutMeshes.Add(cutChunk.gameObject);
            
            var chunkSliceable = cutChunk.gameObject.GetComponent<CorGeo_SliceableMesh>();
            
            chunkSliceable.originalSliceableObject = this.originalSliceableObject ?? this;
            
            // .side returns true if the chunk is on the positive side of the plane (which in this case means that we haven't cut it yet)
            if (cutChunk.side)
            {
                result.positiveChunk = chunkSliceable;
            }
            else
            {
                result.negativeChunk = chunkSliceable;
            }
        }

        return result;
    }

    /// <summary>
    /// Mark the mesh as being done with its slice, this is fired once on each mesh chunk
    /// </summary>
    private void FinishSlice()
    {
        // Ensures this function is only fired once per mesh chunk
        if (!isSliceInProgress) return;
        
        isSliceInProgress = false;

        if (!riftManager)
        {
            riftManager = GameInstance.Get<RiftManager>();
        }
        
        // !Temporary! Mesh collider state fix
        var meshColliders = GetComponents<MeshCollider>();
        if (meshColliders.Length > 0)
        {
            // Find the original object's collider settings
            var originalScript = originalSliceableObject ?? this;
            if (originalScript.sliceHistory.Count > 0)
            {
                var lastState = originalScript.sliceHistory.Peek();
                foreach (var savedCollider in lastState.colliders)
                {
                    // If original had a trigger collider, make new mesh colliders triggers too
                    if (savedCollider.isTrigger)
                    {
                        foreach (var meshCollider in meshColliders)
                        {
                            meshCollider.convex = true;
                            meshCollider.isTrigger = true;
                        }
                    }
                }
            }
        }
        
        // Store in space meshes list
        riftManager.spaceController.spaceMeshes.Add(this, riftSpace);
        //AssignMeshToSpaceLists();
    }
    
    private CorGeo_SliceableMesh FindOriginalObject()
    {
        // Look through parent/siblings to find the non-clone
        var allSliceables = FindObjectsOfType<CorGeo_SliceableMesh>();
        foreach (var sliceable in allSliceables)
        {
            if (!sliceable.isClone && sliceable.sliceHistory.Count > 0)
            {
                return sliceable;
            }
        }
        return null;
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Saves the starting mesh state and attempts to cut the mesh across the rift planes
    /// </summary>
    public void ApplyCuts()
    {
        if (isSliceInProgress)
        {
            throw new Exception("ApplyCuts was called while a slice operation was still in progress!");
        }
        
        isSlicedByPlane = true;
        SaveUndoSnapshot();
        AttemptSliceRiftPlanes();
    }

    /// <summary>
    /// Restores the saved mesh state and destroys cut mesh chunks
    /// </summary>
    public void UndoCuts()
    {
        // Check for unintended exceptions that could cause errors
        if (isSliceInProgress)
        {
            throw new Exception("UndoCuts was called before the slice operation finished!");
        }
        if (sliceHistory.Count == 0)
        {
            throw new Exception($"UndoCuts was called on {gameObject.name}, but the object wasn't marked as cut or the slice history was empty!");
        }
        
        // Mark the object as no longer being cut
        isSlicedByPlane = false;

        // Load the saved information of the mesh prior to the cut
        UndoSliceState state = sliceHistory.Pop();

        // Reconstruct the current mesh data from the loaded data
        // (Colliders get handled after this)
        gameObject.name = state.meshName;
        var meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = state.originalMesh;
        GetComponent<MeshRenderer>().sharedMaterials = state.materials;
        transform.position = state.transformData.position;
        transform.rotation = state.transformData.rotation;
        transform.localScale = state.transformData.scale;
      
        // Restore collider data (Curse you mesh-collider the platypus!)
        // Start with destroying all existing colliders (I can't trust them! >:{ )
        var existingColliders = GetComponents<Collider>();
        foreach (var existingCollider in existingColliders)
        {
            DestroyImmediate(existingCollider); // This needs to be immediate to avoid a race condition
        }
        // Now actually reconstruct the original collider from the save data
        for (int i = 0; i < state.colliders.Length; i++)
        {
            var colliderData = state.colliders[i];
            if (colliderData.colliderType == ColliderCollisionType.mesh)
            {
                var newCollider = gameObject.AddComponent<MeshCollider>();
                newCollider.sharedMesh = colliderData.mesh;
                newCollider.convex = colliderData.convex;
                newCollider.isTrigger = colliderData.isTrigger;
            }
            else if (colliderData.colliderType == ColliderCollisionType.box)
            {
                var newCollider = gameObject.AddComponent<BoxCollider>();
                newCollider.center = colliderData.center;
                newCollider.size = colliderData.size;
                newCollider.isTrigger = colliderData.isTrigger;
            }
        }

        // Clean up the cloned cut chunks for this mesh
        // Wait... this shouldn't be called here...
        if (riftManager.geometryHandler.cutMeshes != null)
        {
            foreach (var clone in riftManager.geometryHandler.cutMeshes)
            {
                Destroy(clone);
            }
            riftManager.geometryHandler.cutMeshes.Clear();
        }
        else
        {
            print("Whoops! riftManager.geometryHandler.cutMeshes is null!");
        }
        
        // Todo: Is this set active actually being used in the new system? ~Liz
        // gameObject.SetActive(true);
    }

    /// <summary>
    /// Test the zeroth vertex of the mesh to determine which side of the rift planes it falls in, then sort it into the correct space
    /// </summary>
    public void AssignMeshToSpaceLists()
    {
        // Sanity checks
        if (!riftManager) riftManager = GameInstance.Get<RiftManager>();
        riftManager.spaceController.spaceMeshes.Remove(this);
        
        // Check the mesh bounds center to see which side of the rift planes it falls on
        MeshFilter meshFilter = GetComponent<MeshFilter> ();
        if (!meshFilter || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning($"{name} has no mesh filter or is an empty mesh");
            return;
        }
        Vector3 worldPoint = transform.TransformPoint(meshFilter.mesh.bounds.center);
        
        // Object is in A Space
        if (RiftManager.cutPlaneA.GetDistanceToPoint(worldPoint) > 0) { riftSpace = RiftSpace.A; }
        // Object is in B Space
        else if (RiftManager.cutPlaneB.GetDistanceToPoint(worldPoint) > 0) { riftSpace = RiftSpace.B; }
        // Object is in NULL Space
        else { riftSpace = RiftSpace.NULLSpace; }
        
        // Store in space meshes list
        riftManager.spaceController.spaceMeshes.Add(this, riftSpace);
    }

    #endregion

}

[Todo("Need to add support for other collider types, currently only works with mesh colliders", TodoSeverity.Major, Owner = "liz")]
struct UndoSliceState
{
    public string meshName;
    public Mesh originalMesh;
    public Material[] materials;
    public ColliderData[] colliders;
    public MeshTransformData transformData;

    public struct MeshTransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    public struct ColliderData
    {
        public ColliderCollisionType colliderType;
        
        // Mesh collider data
        public Mesh mesh;
        public bool convex;
        
        // Box collider data
        public Vector3 center;
        public Vector3 size;
        
        // Generic collider data
        public bool isTrigger;
    }
}

public enum ColliderCollisionType
{
    mesh,
    box,
    unknown
}