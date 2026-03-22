//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using UnityEngine;

/// <summary>
/// Add this component to an actor to allow them to be culled out using the voxel grid system
/// </summary>
public class Vox_CullableActor : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("When enabled, the only modified renderers will be the ones manually set in cullableRenderers instead of getting all child renderers on this object")]
    public bool manuallyAssignCullableRenderers = false;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("These are the renderers that will be disabled or enabled when culling occurs")]
    public Renderer[] cullableRenderers;
    [Tooltip("A reference to the voxel occlusion culler so this actor can register and unregister itself")]
    private VoxOcclusionCuller occlusionCuller;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        // If manual renderer assignment is disabled, find all the renderers on this actor
        if (!manuallyAssignCullableRenderers)
        {
            cullableRenderers = GetComponentsInChildren<Renderer>();
        }
        
        // Add this actor to the occlusion culler system
        if (!occlusionCuller) occlusionCuller = FindObjectOfType<VoxOcclusionCuller>();
        if (occlusionCuller != null) occlusionCuller.RegisterActor(this);
    }

    private void OnDestroy()
    {
        // Remove this actor to the occlusion culler system
        if (!occlusionCuller) occlusionCuller = FindObjectOfType<VoxOcclusionCuller>();
        if (occlusionCuller != null) occlusionCuller.UnregisterActor(this);
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
