
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

/// <summary>
/// Performs occlusion culling on props using the voxel grid.
/// Checks if props are hidden behind solid voxels from the camera's perspective.
/// </summary>
public class VoxOcclusionCuller : MonoBehaviour
{
    [Header("Culling Settings")]
    [Tooltip("The camera to use for occlusion checks (usually the main camera)")]
    public Camera cullingCamera;
    
    [Tooltip("How many props to check per frame (lower = better performance, higher = more responsive culling)")]
    public int propsCheckedPerFrame = 50;
    
    [Tooltip("How often to update occlusion (in seconds). Lower = more responsive but more expensive")]
    public float updateInterval = 0.1f;
    
    [Tooltip("Maximum distance to check for occlusion. Props beyond this are always rendered")]
    public float maxCullingDistance = 100f;
    
    [Header("Debug")]
    [Tooltip("Show debug rays for occlusion checks")]
    public bool showDebugRays = false;
    
    [Tooltip("Show statistics in console")]
    public bool showStatistics = false;
    
    private List<Vox_CullableActor> registeredActors = new List<Vox_CullableActor>();
    private int currentCheckIndex = 0;
    private float timeSinceLastUpdate = 0f;
    
    // Statistics
    private int totalActors = 0;
    private int culledActors = 0;
    private int visibleActors = 0;
    
    void Start()
    {
        // Default to main camera if not set
        if (cullingCamera == null)
        {
            var localPlayer = GameInstance.Get<GI_PawnManager>().localPlayerCharacter;
            if (localPlayer) cullingCamera = localPlayer.GetComponentInChildren<Camera>();
        }
        
        // Find all actors in the scene
        RegisterAllActors();
        
        Debug.Log($"VoxelOcclusionCuller initialized with {registeredActors.Count} actors");
    }
    
    /// <summary>
    /// Register all Actor components in the scene
    /// </summary>
    public void RegisterAllActors()
    {
        registeredActors.Clear();
        Vox_CullableActor[] allActors = FindObjectsOfType<Vox_CullableActor>();
        
        foreach (Vox_CullableActor actor in allActors)
        {
            RegisterActor(actor);
        }
        
        totalActors = registeredActors.Count;
    }
    
    /// <summary>
    /// Register a single actor for occlusion culling
    /// </summary>
    public void RegisterActor(Vox_CullableActor actor)
    {
        if (!registeredActors.Contains(actor))
        {
            registeredActors.Add(actor);
            totalActors = registeredActors.Count;
        }
    }
    
    /// <summary>
    /// Unregister an actor (e.g., when destroyed)
    /// </summary>
    public void UnregisterActor(Vox_CullableActor actor)
    {
        registeredActors.Remove(actor);
        totalActors = registeredActors.Count;
    }
    
    void Update()
    {
        if (!cullingCamera)
        {
            var localPlayer = GameInstance.Get<GI_PawnManager>().localPlayerCharacter;
            if (localPlayer) cullingCamera = localPlayer.GetComponentInChildren<Camera>();
        }
        if (!cullingCamera || !VoxWorldManager.Instance)
            return;
        
        // Don't run culling until voxels are ready
        //if (!IsVoxelSystemReady()) return;
        
        timeSinceLastUpdate += Time.deltaTime;
        
        // Only update at specified intervals
        if (timeSinceLastUpdate >= updateInterval)
        {
            timeSinceLastUpdate = 0f;
            UpdateOcclusion();
        }
    }
    
    /// <summary>
    /// Check if the voxel system has chunks ready for occlusion queries
    /// </summary>
    bool IsVoxelSystemReady()
    {
        // Check if we have at least one chunk available
        VoxContainer testChunk = VoxWorldManager.Instance.GetChunk(Vector3Int.zero);
        if (testChunk == null)
        {
            testChunk = VoxWorldManager.Instance.GetMainContainer();
        }
        
        // If we still don't have a chunk, system isn't ready yet
        return testChunk != null && testChunk.containerData != null;
    }
    
    /// <summary>
    /// Update occlusion for a batch of props
    /// </summary>
    void UpdateOcclusion()
    {
        if (registeredActors.Count == 0) return;
        
        int propsCheckedThisFrame = 0;
        culledActors = 0;
        visibleActors = 0;
        
        // Check props in a round-robin fashion
        while (propsCheckedThisFrame < propsCheckedPerFrame && registeredActors.Count > 0)
        {
            // Wrap around to the beginning if we've checked all props
            if (currentCheckIndex >= registeredActors.Count)
            {
                currentCheckIndex = 0;
            }
            
            Vox_CullableActor actor = registeredActors[currentCheckIndex];
            
            // Remove null actors (destroyed objects)
            if (actor is null)
            {
                registeredActors.RemoveAt(currentCheckIndex);
                continue;
            }
            
            // Check if this actor is occluded
            bool isOccluded = IsOccluded(actor);
            
            // Enable/disable the renderer based on occlusion
            foreach (var renderer in actor.cullableRenderers)
            {
                if (renderer)
                {
                    renderer.enabled = !isOccluded;
                }
            }
            
            if (isOccluded)
                culledActors++;
            else
                visibleActors++;
            
            currentCheckIndex++;
            propsCheckedThisFrame++;
        }
        
        // Show statistics periodically
        if (showStatistics && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Occlusion Stats - Total: {totalActors}, Visible: {visibleActors}, Culled: {culledActors}");
        }
    }
    
    /// <summary>
    /// Check if an actor is occluded by the voxel grid from the camera's perspective
    /// </summary>
    bool IsOccluded(Vox_CullableActor actor)
    {Vector3 cameraPosition = cullingCamera.transform.position;
        
        // Get all renderers for this actor
        //Renderer[] renderers = actor.GetComponentsInChildren<Renderer>();
        //if (renderers.Length == 0) return false;
        
        // Check if ANY renderer is visible
        foreach (Renderer renderer in actor.cullableRenderers)
        {
            if (!renderer) continue;
            
            Bounds bounds = renderer.bounds;
            
            // Distance check - don't cull things that are too far
            float distance = Vector3.Distance(cameraPosition, bounds.center);
            if (distance > maxCullingDistance)
            {
                return false;
            }
            
            // Get sample points on the bounds that face the camera
            Vector3[] samplePoints = GetBoundsSamplePoints(bounds, cameraPosition);
            
            // If ANY sample point is visible, the whole prop is visible
            foreach (Vector3 samplePoint in samplePoints)
            {
                Vector3 direction = (samplePoint - cameraPosition).normalized;
                float sampleDistance = Vector3.Distance(cameraPosition, samplePoint);
                
                bool hitVoxel = VoxelRaycast(cameraPosition, direction, sampleDistance, out float hitDistance);
                
                // If we didn't hit a voxel, or hit one beyond the sample point, it's visible
                if (!hitVoxel || hitDistance >= sampleDistance - 0.1f)
                {
                    // This sample point is visible!
                    if (showDebugRays)
                    {
                        Debug.DrawLine(cameraPosition, samplePoint, Color.green, updateInterval);
                    }
                    return false; // At least one point visible = don't cull
                }
                
                if (showDebugRays)
                {
                    Debug.DrawRay(cameraPosition, direction * hitDistance, Color.red, updateInterval);
                    Debug.DrawRay(cameraPosition + direction * hitDistance, direction * (sampleDistance - hitDistance), Color.yellow, updateInterval);
                }
            }
        }
        
        // All sample points on all renderers are occluded
        return true;
    }
    
    /// <summary>
    /// Get strategic sample points on a bounds that are most likely visible from the camera.
    /// Uses the bounds center + the 4 corners facing the camera.
    /// </summary>
    Vector3[] GetBoundsSamplePoints(Bounds bounds, Vector3 cameraPosition)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        
        // Direction from bounds to camera
        Vector3 toCamera = (cameraPosition - center).normalized;
        
        // Always check the center
        List<Vector3> points = new List<Vector3>(5);
        points.Add(center);
        
        // Determine which face of the bounds is facing the camera
        // and add the 4 corners of that face
        
        // Check which axis is most aligned with camera direction
        float absX = Mathf.Abs(toCamera.x);
        float absY = Mathf.Abs(toCamera.y);
        float absZ = Mathf.Abs(toCamera.z);
        
        if (absX > absY && absX > absZ)
        {
            // Camera is mostly on the X axis
            float x = toCamera.x > 0 ? extents.x : -extents.x;
            points.Add(center + new Vector3(x, extents.y, extents.z));
            points.Add(center + new Vector3(x, extents.y, -extents.z));
            points.Add(center + new Vector3(x, -extents.y, extents.z));
            points.Add(center + new Vector3(x, -extents.y, -extents.z));
        }
        else if (absY > absZ)
        {
            // Camera is mostly on the Y axis
            float y = toCamera.y > 0 ? extents.y : -extents.y;
            points.Add(center + new Vector3(extents.x, y, extents.z));
            points.Add(center + new Vector3(extents.x, y, -extents.z));
            points.Add(center + new Vector3(-extents.x, y, extents.z));
            points.Add(center + new Vector3(-extents.x, y, -extents.z));
        }
        else
        {
            // Camera is mostly on the Z axis
            float z = toCamera.z > 0 ? extents.z : -extents.z;
            points.Add(center + new Vector3(extents.x, extents.y, z));
            points.Add(center + new Vector3(extents.x, -extents.y, z));
            points.Add(center + new Vector3(-extents.x, extents.y, z));
            points.Add(center + new Vector3(-extents.x, -extents.y, z));
        }
        
        return points.ToArray();
    }
    
    /// <summary>
    /// Raycast through the voxel grid using DDA (Digital Differential Analyzer) algorithm
    /// This is much faster than checking every voxel along the path
    /// </summary>
    bool VoxelRaycast(Vector3 origin, Vector3 direction, float maxDistance, out float hitDistance)
    {
        hitDistance = maxDistance;
        
        // Convert world position to voxel coordinates
        Vector3 currentPos = origin / VoxWorldManager.Instance.voxelScale;
        Vector3Int currentVoxel = new Vector3Int(
            Mathf.FloorToInt(currentPos.x),
            Mathf.FloorToInt(currentPos.y),
            Mathf.FloorToInt(currentPos.z)
        );
        
        // DDA setup
        Vector3Int step = new Vector3Int(
            direction.x > 0 ? 1 : -1,
            direction.y > 0 ? 1 : -1,
            direction.z > 0 ? 1 : -1
        );
        
        // Prevent division by zero
        Vector3 deltaDist = new Vector3(
            Mathf.Abs(1f / (direction.x == 0 ? 0.0001f : direction.x)),
            Mathf.Abs(1f / (direction.y == 0 ? 0.0001f : direction.y)),
            Mathf.Abs(1f / (direction.z == 0 ? 0.0001f : direction.z))
        );
        
        // Distance to next voxel boundary
        Vector3 sideDist = new Vector3(
            step.x > 0 ? (currentVoxel.x + 1 - currentPos.x) * deltaDist.x : (currentPos.x - currentVoxel.x) * deltaDist.x,
            step.y > 0 ? (currentVoxel.y + 1 - currentPos.y) * deltaDist.y : (currentPos.y - currentVoxel.y) * deltaDist.y,
            step.z > 0 ? (currentVoxel.z + 1 - currentPos.z) * deltaDist.z : (currentPos.z - currentVoxel.z) * deltaDist.z
        );
        
        float traveledDistance = 0f;
        float maxDistanceInVoxels = maxDistance / VoxWorldManager.Instance.voxelScale;
        
        // Traverse voxels using DDA
        int maxIterations = Mathf.CeilToInt(maxDistanceInVoxels * 2); // Safety limit
        int iterations = 0;
        
        while (traveledDistance < maxDistanceInVoxels && iterations < maxIterations)
        {
            iterations++;
            
            // Check if current voxel is solid
            if (IsVoxelSolid(currentVoxel))
            {
                hitDistance = traveledDistance * VoxWorldManager.Instance.voxelScale;
                return true;
            }
            
            // Step to next voxel boundary
            if (sideDist.x < sideDist.y)
            {
                if (sideDist.x < sideDist.z)
                {
                    currentVoxel.x += step.x;
                    traveledDistance = sideDist.x;
                    sideDist.x += deltaDist.x;
                }
                else
                {
                    currentVoxel.z += step.z;
                    traveledDistance = sideDist.z;
                    sideDist.z += deltaDist.z;
                }
            }
            else
            {
                if (sideDist.y < sideDist.z)
                {
                    currentVoxel.y += step.y;
                    traveledDistance = sideDist.y;
                    sideDist.y += deltaDist.y;
                }
                else
                {
                    currentVoxel.z += step.z;
                    traveledDistance = sideDist.z;
                    sideDist.z += deltaDist.z;
                }
            }
        }
        
        return false; // No voxel hit within max distance
    }
    
    /// <summary>
    /// Check if a voxel at the given position is solid
    /// </summary>
    bool IsVoxelSolid(Vector3Int voxelPos)
    {
        // If using chunked system, need to find the right chunk
        if (VoxWorldManager.Instance.useProgressiveChunkGeneration)
        {
            // Calculate which chunk this voxel belongs to
            Vector3Int chunkIndex = new Vector3Int(
                Mathf.FloorToInt((float)(voxelPos.x - VoxWorldManager.Instance.minPosition.x) / VoxWorldManager.Instance.chunkSize.x),
                Mathf.FloorToInt((float)(voxelPos.y - VoxWorldManager.Instance.minPosition.y) / VoxWorldManager.Instance.chunkSize.y),
                Mathf.FloorToInt((float)(voxelPos.z - VoxWorldManager.Instance.minPosition.z) / VoxWorldManager.Instance.chunkSize.z)
            );
            
            // Try to get the chunk
            VoxContainer chunk = VoxWorldManager.Instance.GetChunk(chunkIndex);
            if (chunk)
            {
                Vector3 voxelPosFloat = new Vector3(voxelPos.x, voxelPos.y, voxelPos.z);
                return chunk[voxelPosFloat].isSolid;
            }
            
            return false;
        }
        else
        {
            // Single container mode - access directly
            VoxContainer container = VoxWorldManager.Instance.GetMainContainer();
            if (container)
            {
                Vector3 voxelPosFloat = new Vector3(voxelPos.x, voxelPos.y, voxelPos.z);
                return container[voxelPosFloat].isSolid;
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Force an immediate occlusion check for all actors
    /// </summary>
    public void ForceUpdate()
    {
        currentCheckIndex = 0;
        
        foreach (Vox_CullableActor actor in registeredActors)
        {
            if (actor == null) continue;
            
            bool isOccluded = IsOccluded(actor);
            Renderer renderer = actor.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = !isOccluded;
            }
        }
    }
}