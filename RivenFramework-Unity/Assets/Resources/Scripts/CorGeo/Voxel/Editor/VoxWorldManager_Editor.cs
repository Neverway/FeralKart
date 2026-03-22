// Written by Liz M.
// Editor script for VoxWorldManager to allow pre-baking voxels in the editor

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

[CustomEditor(typeof(VoxWorldManager))]
public class VoxWorldManagerEditor : Editor
{
    private VoxWorldManager manager;
    private bool isBaking = false;
    private float bakingProgress = 0f;
    private string bakingStatus = "";
    
    void OnEnable()
    {
        manager = (VoxWorldManager)target;
    }
    
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Voxel Baking", EditorStyles.boldLabel);
        
        // Baking buttons
        using (new EditorGUI.DisabledScope(isBaking))
        {
            if (GUILayout.Button("Bake Voxels in Editor", GUILayout.Height(30)))
            {
                BakeVoxels();
            }
        }
        
        if (GUILayout.Button("Clear All Voxels", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear Voxels", 
                "Are you sure you want to clear all baked voxels?", 
                "Yes", "Cancel"))
            {
                ClearVoxels();
            }
        }
        
        // Show baking progress
        if (isBaking)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Baking Status:", bakingStatus);
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), bakingProgress, 
                $"{Mathf.RoundToInt(bakingProgress * 100)}%");
        }
        
        // Show chunk info
        EditorGUILayout.Space(10);
        ShowChunkInfo();
    }
    
    void ShowChunkInfo()
    {
        EditorGUILayout.LabelField("Current Voxel Info", EditorStyles.boldLabel);
        
        // Count existing chunks
        Transform chunksParent = manager.transform.Find("VoxelChunks");
        int chunkCount = 0;
        int totalVoxels = 0;
        
        if (chunksParent != null)
        {
            chunkCount = chunksParent.childCount;
            
            for (int i = 0; i < chunksParent.childCount; i++)
            {
                VoxContainer container = chunksParent.GetChild(i).GetComponent<VoxContainer>();
                if (container != null && container.containerData != null)
                {
                    totalVoxels += container.containerData.Count;
                }
            }
        }
        
        EditorGUILayout.LabelField($"Chunks: {chunkCount}");
        EditorGUILayout.LabelField($"Total Voxels: {totalVoxels}");
        
        if (chunkCount > 0)
        {
            EditorGUILayout.HelpBox("Voxels have been baked to the scene. They will persist when you save.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("No voxels baked yet. Click 'Bake Voxels in Editor' to generate.", MessageType.Warning);
        }
    }
    
    void BakeVoxels()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorCoroutineUtility.StartCoroutine(BakeVoxelsCoroutine(), this);
        }
        else
        {
            EditorUtility.DisplayDialog("Cannot Bake", 
                "Cannot bake voxels while in Play Mode. Exit Play Mode first.", "OK");
        }
    }
    
    IEnumerator BakeVoxelsCoroutine()
    {
        isBaking = true;
        bakingProgress = 0f;
        
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Clear existing voxels first
        ClearVoxels();
        
        // Create chunks parent if it doesn't exist
        Transform chunksParentTransform = manager.transform.Find("VoxelChunks");
        GameObject chunksParent;
        
        if (chunksParentTransform == null)
        {
            chunksParent = new GameObject("VoxelChunks");
            chunksParent.transform.SetParent(manager.transform);
            chunksParent.transform.localPosition = Vector3.zero;
        }
        else
        {
            chunksParent = chunksParentTransform.gameObject;
        }
        
        bakingStatus = "Finding colliders...";
        yield return null;
        
        // Find all colliders
        Collider[] allColliders = Object.FindObjectsOfType<Collider>();
        List<Collider> validColliders = new List<Collider>();
        
        foreach (Collider col in allColliders)
        {
            if (((1 << col.gameObject.layer) & manager.voxelDetectionLayers) != 0)
            {
                validColliders.Add(col);
            }
        }
        
        Debug.Log($"Found {validColliders.Count} valid colliders to voxelize");
        
        if (validColliders.Count == 0)
        {
            EditorUtility.DisplayDialog("No Colliders Found", 
                "No valid colliders found! Check your layer settings.", "OK");
            isBaking = false;
            yield break;
        }
        
        // Calculate chunk count
        Vector3Int numChunks = new Vector3Int(
            Mathf.CeilToInt((float)(manager.maxPosition.x - manager.minPosition.x) / manager.chunkSize.x),
            Mathf.CeilToInt((float)(manager.maxPosition.y - manager.minPosition.y) / manager.chunkSize.y),
            Mathf.CeilToInt((float)(manager.maxPosition.z - manager.minPosition.z) / manager.chunkSize.z)
        );
        
        int totalChunks = numChunks.x * numChunks.y * numChunks.z;
        int chunksCompleted = 0;
        int totalVoxelsDetected = 0;
        
        Debug.Log($"Baking {totalChunks} chunks ({numChunks.x}x{numChunks.y}x{numChunks.z})");
        
        Vector3 halfExtents = Vector3.one * manager.voxelScale * (manager.overlapCheckSize / 2f);
        Collider[] colliderBuffer = new Collider[10];
        
        // Generate each chunk
        for (int cx = 0; cx < numChunks.x; cx++)
        {
            for (int cy = 0; cy < numChunks.y; cy++)
            {
                for (int cz = 0; cz < numChunks.z; cz++)
                {
                    bakingStatus = $"Baking chunk {chunksCompleted + 1}/{totalChunks}";
                    bakingProgress = (float)chunksCompleted / totalChunks;
                    
                    Vector3Int chunkMin = manager.minPosition + new Vector3Int(
                        cx * manager.chunkSize.x,
                        cy * manager.chunkSize.y,
                        cz * manager.chunkSize.z
                    );
                    
                    Vector3Int chunkMax = new Vector3Int(
                        Mathf.Min(chunkMin.x + manager.chunkSize.x, manager.maxPosition.x),
                        Mathf.Min(chunkMin.y + manager.chunkSize.y, manager.maxPosition.y),
                        Mathf.Min(chunkMin.z + manager.chunkSize.z, manager.maxPosition.z)
                    );
                    
                    // Create chunk container
                    GameObject chunkObject = new GameObject($"Chunk_{cx}_{cy}_{cz}");
                    chunkObject.transform.SetParent(chunksParent.transform);
                    chunkObject.transform.localPosition = Vector3.zero;
                    VoxContainer chunk = chunkObject.AddComponent<VoxContainer>();
                    
                    chunk.doNotSkipGeneratingAirBlocks = manager.doNotSkipGeneratingAirBlocks;
                    chunk.doNotSkipGeneratingBackfaces = manager.doNotSkipGeneratingBackfaces;
                    chunk.Initialize(manager.worldMaterial, manager.voxelScale);
                    
                    int chunkVoxelsDetected = 0;
                    
                    // Voxelize this chunk
                    foreach (Collider col in validColliders)
                    {
                        if (col == null || col.GetComponent<VoxContainer>() != null) continue;
                        
                        Bounds chunkBounds = new Bounds(
                            new Vector3(
                                (chunkMin.x + chunkMax.x) * 0.5f * manager.voxelScale,
                                (chunkMin.y + chunkMax.y) * 0.5f * manager.voxelScale,
                                (chunkMin.z + chunkMax.z) * 0.5f * manager.voxelScale
                            ),
                            new Vector3(
                                (chunkMax.x - chunkMin.x) * manager.voxelScale,
                                (chunkMax.y - chunkMin.y) * manager.voxelScale,
                                (chunkMax.z - chunkMin.z) * manager.voxelScale
                            )
                        );
                        
                        if (!col.bounds.Intersects(chunkBounds)) continue;
                        
                        Vector3Int boundsMin = new Vector3Int(
                            Mathf.Max(chunkMin.x, Mathf.FloorToInt(col.bounds.min.x / manager.voxelScale)),
                            Mathf.Max(chunkMin.y, Mathf.FloorToInt(col.bounds.min.y / manager.voxelScale)),
                            Mathf.Max(chunkMin.z, Mathf.FloorToInt(col.bounds.min.z / manager.voxelScale))
                        );
                        
                        Vector3Int boundsMax = new Vector3Int(
                            Mathf.Min(chunkMax.x, Mathf.CeilToInt(col.bounds.max.x / manager.voxelScale)),
                            Mathf.Min(chunkMax.y, Mathf.CeilToInt(col.bounds.max.y / manager.voxelScale)),
                            Mathf.Min(chunkMax.z, Mathf.CeilToInt(col.bounds.max.z / manager.voxelScale))
                        );
                        
                        for (int x = boundsMin.x; x < boundsMax.x; x++)
                        {
                            for (int z = boundsMin.z; z < boundsMax.z; z++)
                            {
                                for (int y = boundsMin.y; y < boundsMax.y; y++)
                                {
                                    Vector3 voxelPosition = new Vector3(x, y, z);
                                    if (chunk[voxelPosition].ID != 0) continue;
                                    
                                    Vector3 checkPosition = (voxelPosition * manager.voxelScale) + 
                                        Vector3.one * (manager.voxelScale * 0.5f);
                                    
                                    int hitCount = Physics.OverlapBoxNonAlloc(
                                        checkPosition, halfExtents, colliderBuffer,
                                        Quaternion.identity, manager.voxelDetectionLayers
                                    );
                                    
                                    if (hitCount > 0)
                                    {
                                        for (int i = 0; i < hitCount; i++)
                                        {
                                            if (colliderBuffer[i] == col)
                                            {
                                                chunk[voxelPosition] = new Voxel() { ID = 1 };
                                                chunkVoxelsDetected++;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Generate and upload mesh
                    if (chunkVoxelsDetected > 0)
                    {
                        chunk.GenerateMesh();
                        chunk.UploadMesh();
                        totalVoxelsDetected += chunkVoxelsDetected;
                        
                        // Serialize voxel data so it persists when saved
                        chunk.SerializeVoxelData();
                        
                        // Mark as dirty for saving
                        EditorUtility.SetDirty(chunkObject);
                    }
                    else
                    {
                        // Empty chunk - destroy it
                        Object.DestroyImmediate(chunkObject);
                    }
                    
                    chunksCompleted++;
                    
                    // Yield periodically to keep editor responsive
                    if (chunksCompleted % 5 == 0)
                    {
                        yield return null;
                    }
                }
            }
        }
        
        stopwatch.Stop();
        
        bakingProgress = 1f;
        bakingStatus = "Complete!";
        
        // Mark manager as dirty
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(chunksParent);
        
        Debug.Log($"Voxel baking complete! Total: {totalVoxelsDetected} voxels in {chunksCompleted} chunks");
        Debug.Log($"Time taken: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F2} seconds)");
        
        EditorUtility.DisplayDialog("Baking Complete!", 
            $"Successfully baked {totalVoxelsDetected} voxels across {chunksCompleted} chunks in {stopwatch.Elapsed.TotalSeconds:F2} seconds.\n\nDon't forget to save your scene!", 
            "OK");
        
        isBaking = false;
        
        // Repaint to update inspector
        Repaint();
    }
    
    void ClearVoxels()
    {
        Transform chunksParent = manager.transform.Find("VoxelChunks");
        if (chunksParent != null)
        {
            // Destroy all chunk children
            for (int i = chunksParent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(chunksParent.GetChild(i).gameObject);
            }
            
            Debug.Log("Cleared all voxel chunks");
        }
        
        EditorUtility.SetDirty(manager);
        Repaint();
    }
}

/// <summary>
/// Utility class to run coroutines in the editor
/// </summary>
public static class EditorCoroutineUtility
{
    private class EditorCoroutine
    {
        private IEnumerator routine;
        
        public EditorCoroutine(IEnumerator routine)
        {
            this.routine = routine;
        }
        
        public void Start()
        {
            EditorApplication.update += Update;
        }
        
        public void Stop()
        {
            EditorApplication.update -= Update;
        }
        
        private void Update()
        {
            if (!routine.MoveNext())
            {
                Stop();
            }
        }
    }
    
    public static void StartCoroutine(IEnumerator routine, Object owner)
    {
        EditorCoroutine coroutine = new EditorCoroutine(routine);
        coroutine.Start();
    }
}
#endif