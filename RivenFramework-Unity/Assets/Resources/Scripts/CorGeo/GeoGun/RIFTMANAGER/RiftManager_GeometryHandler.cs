//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M., Connorses, Errynei, Soulex
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ErryLib.MonoTasks;
using RivenFramework;
using UnityEngine;

/// <summary>
/// Handles the cutting and un-cutting of geometry
/// (This includes positioning the cut and visual planes)
/// </summary>
[Serializable]
public class RiftManager_GeometryHandler : ILoggable
{
    /// <summary>
    /// Class constructor
    /// </summary>
    public RiftManager_GeometryHandler(RiftManager riftManager, RiftManager_SpaceController spaceController)
    {
        this.riftManager = riftManager;
        EnableRuntimeLogging = riftManager.EnableRuntimeLogging;
        this.spaceController = spaceController;
    }
    
    
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public bool EnableRuntimeLogging { get; set; }


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Link to the space controller so this class has access to space containers")]
    private RiftManager_SpaceController spaceController;
    private RiftManager riftManager;
    
    [Tooltip("Coroutine used to keep multiple cut operations from being called at the same time")]
    private Coroutine cutRoutine;

    [Tooltip("The visuals that represent the rift cut planes")]
    public GameObject visualPlaneA, visualPlaneB;

    [Tooltip("")]
    public HashSet<GameObject> cutMeshes = new HashSet<GameObject>();


    #endregion

    
    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Instantiate the mesh planes that are used for visualizing the rift
    /// </summary>
    private void CreateVisualPlanes()
    {
        this.Log("CreateVisualPlanes called");
        visualPlaneA = MonoBehaviour.Instantiate(riftManager.visualPlanePrefab, null);
        visualPlaneB = MonoBehaviour.Instantiate(riftManager.visualPlanePrefab, null);
        visualPlaneA.name = "VisPlaneA";
        visualPlaneB.name = "VisPlaneB";
    }
    
    /// <summary>
    /// Find and cut all sliceable meshes that are intersecting with the rift cut planes
    /// </summary>
    private IEnumerator SliceCutPlanes()
    {
        this.Log("sliceCutPlanes started");
        
        // Separate and slice intersected meshes, sort the unintersected
        List<CorGeo_SliceableMesh> allMeshes = GameObject.FindObjectsOfType<CorGeo_SliceableMesh>().ToList();
        HashSet<CorGeo_SliceableMesh> intersectedMeshes = new HashSet<CorGeo_SliceableMesh>();

        foreach (var mesh in allMeshes)
        {
            if (CorGeo_PlaneIntersectionUtil.IsMeshIntersectingPlane(RiftManager.cutPlaneA, mesh) ||
                CorGeo_PlaneIntersectionUtil.IsMeshIntersectingPlane(RiftManager.cutPlaneB, mesh))
            {
                mesh.ApplyCuts();
                intersectedMeshes.Add(mesh);
            }
            else
            {
                //Debug.Log($"{mesh.name} Object wasn't intersecting");
                mesh.AssignMeshToSpaceLists();
            }
        }
        
        while (intersectedMeshes.Any((intersectedMesh) => intersectedMesh.isSliceInProgress))
        {
            yield return null;
        }
        
        
        cutRoutine = null;
        this.Log("sliceCutPlanes finished");
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Show or hide the visual effects planes that represent the rift
    /// </summary>
    public void SetRiftPlanesVisible(bool _isVisible)
    {
        this.Log("SetRiftPlanesVisible called");
        // Do an initial check
        if (!visualPlaneA && !visualPlaneB) CreateVisualPlanes();

        visualPlaneA.SetActive(_isVisible);
        visualPlaneB.SetActive(_isVisible);
    }

    /// <summary>
    /// Specify the points in 3d space in which the mathematical cut planes will be created
    /// </summary>
    public void PositionCutPlanes(Transform _markerA, Transform _markerB)
    {
        this.Log("PositionCutPlanes called");
        // Set the positions and rotations of the cut plane objects
        visualPlaneA.transform.position = _markerA.transform.position;
        visualPlaneB.transform.position = _markerB.transform.position;
        
        visualPlaneA.transform.LookAt(_markerB.transform);
        visualPlaneB.transform.LookAt(_markerA.transform);
        
        // Assign the mathematical plane values
        RiftManager.cutPlaneA = new Plane(-visualPlaneA.transform.forward, visualPlaneA.transform.position);
        RiftManager.cutPlaneB = new Plane(-visualPlaneB.transform.forward, visualPlaneB.transform.position);

        // Initialize/Position the space containers
        // (Hmm I dunno if I should be putting this here... ~Liz)
        spaceController.PositionSpaceContainers(visualPlaneA, visualPlaneB);
        
        // Initialize the rift measurements
        RiftManager.riftStartingWidth = Vector3.Distance(visualPlaneA.transform.position, visualPlaneB.transform.position);
        RiftManager.currentRiftPercent = 1;
        RiftManager.currentRiftWidth =  RiftManager.riftStartingWidth;

        // I'm preserving this position because negative scaling moves the object. ~Connorses
        RiftManager.riftNullSpaceStartingPosition = spaceController.spaceContainerNull.transform.position;

        // Saves the direction the rift is facing so we can easily reference it.
        RiftManager.riftNormal = spaceController.spaceContainerNull.transform.forward;
    }

    /// <summary>
    /// Attempt to slice all geometry across the rift planes
    /// </summary>
    public async Task PerformCutProcedure()
    {
        this.Log("PerformCutProcedure called");
        if (cutRoutine != null)
        {
            Debug.LogError("Attempted to perform cut while one is already running! This is bad!?");
            return;
        }
        await For.Coroutine(SliceCutPlanes(), out cutRoutine);
    }

    /// <summary>
    /// Undo cutting the geometry
    /// </summary>
    [Todo("Not implemented", severity:TodoSeverity.Critical, Owner = "Liz-RiftManagerRevamp")]
    public void RestoreCutGeometry()
    {
        this.Log("RestoreCutGeometry called");
        var sliceableMeshes = GameObject.FindObjectsOfType<CorGeo_SliceableMesh>();
        foreach (var sliceableMesh in sliceableMeshes)
        {
            if (sliceableMesh.isSlicedByPlane && !sliceableMesh.isClone)
            {
                sliceableMesh.UndoCuts();
            }
        }
    }


    #endregion

}
