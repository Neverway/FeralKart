using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class VoxWorldManager : MonoBehaviour
{
    public Material worldMaterial;
    private VoxContainer voxelContainer;
    private Dictionary<Vector3Int, VoxContainer> voxelChunks = new Dictionary<Vector3Int, VoxContainer>();
    private GameObject chunksParent;
    
    [Header("Voxelization Settings")]
    [Tooltip("The layers that are considered to be 'solid' or 'occupied' voxels, like the walls of the level geometry, but not something like a physics prop or a character")]
    public LayerMask voxelDetectionLayers = -1;
    [Tooltip("The size of a voxel compared to world units (smaller is more accurate, but slower)")]
    public float voxelScale = 1.0f;
    [Tooltip("Minimum size threshold for overlap detection (once again, smaller is more accurate, but slower)")]
    public float overlapCheckSize = 0.4f;
    [Tooltip("How many voxels to process in batches to prevent frame times from taking too long (0 = no voxel batch limits (This is vewry scawy!! ~Liz))")]
    public int maxVoxelsCalculatedPerFrame = 1000;
    
    [Header("Chunk Settings")]
    public Vector3Int chunkSize = new Vector3Int(16, 16, 16);
    public bool useProgressiveChunkGeneration = true;
    [Tooltip("If enabled, will generate voxels at runtime. If disabled, will only use pre-baked voxels from the editor.")]
    public bool generateAtRuntime = true;
    
    [Header("Debugging")]
    public Vector3Int minPosition = new Vector3Int(0,0,0);
    public Vector3Int maxPosition = new Vector3Int(32,32,32);
    [Tooltip("[BROKEN/DEPRECATED] In a standard voxel system there is no need to generate a voxel for air, enable this if you want to generate air blocks")]
    public bool doNotSkipGeneratingAirBlocks = false;
    [Tooltip("[BROKEN/DEPRECATED] In a standard voxel system you don't usually want to draw faces that can't be seen or are covering each other, enable this if you want to generate touching faces")]
    public bool doNotSkipGeneratingBackfaces = false;
    
    private static VoxWorldManager _instance;
    public static VoxWorldManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<VoxWorldManager>();
            return _instance;
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        /*if (_instance != null)
        {
            if (_instance != this) Destroy(this);
        }
        else
        {
            _instance = this;
        }*/
        _instance = this;

        // Check if we have pre-baked chunks
        Transform existingChunksParent = transform.Find("VoxelChunks");
        bool hasPrebakedChunks = existingChunksParent != null && existingChunksParent.childCount > 0;
        
        if (hasPrebakedChunks)
        {
            // Use existing pre-baked chunks
            chunksParent = existingChunksParent.gameObject;
            
            // Register existing chunks in the dictionary
            foreach (Transform child in chunksParent.transform)
            {
                VoxContainer chunk = child.GetComponent<VoxContainer>();
                if (chunk != null)
                {
                    // Deserialize the voxel data from saved lists
                    chunk.DeserializeVoxelData();
                    
                    // Ensure containerData is initialized (important for pre-baked chunks)
                    if (chunk.containerData == null)
                    {
                        chunk.containerData = new Dictionary<Vector3, Voxel>();
                    }
                    
                    // Parse chunk index from name (format: "Chunk_X_Y_Z")
                    string[] parts = child.name.Split('_');
                    if (parts.Length == 4)
                    {
                        Vector3Int chunkIndex = new Vector3Int(
                            int.Parse(parts[1]),
                            int.Parse(parts[2]),
                            int.Parse(parts[3])
                        );
                        voxelChunks[chunkIndex] = chunk;
                    }
                }
            }
            
            Debug.Log($"Using {voxelChunks.Count} pre-baked voxel chunks from editor");
            
            // Don't generate if we have pre-baked chunks and runtime generation is disabled
            if (!generateAtRuntime)
            {
                return;
            }
        }
        
        // Generate voxels at runtime
        if (chunksParent == null)
        {
            chunksParent = new GameObject("VoxelChunks");
            chunksParent.transform.SetParent(null);
        }

        if (useProgressiveChunkGeneration)
        {
            StartCoroutine(CoGenerateTerrainChunked());
        }
        else
        {
            StartCoroutine(CoGenerateTerrainBoundsBased());
        }
        
        /*GameObject containerObject = new GameObject("Voxel Container");
        containerObject.transform.SetParent(transform);
        voxelContainer = containerObject.AddComponent<VoxContainer>();

        voxelContainer.doNotSkipGeneratingAirBlocks = doNotSkipGeneratingAirBlocks;
        voxelContainer.doNotSkipGeneratingBackfaces = doNotSkipGeneratingBackfaces;
        voxelContainer.Initialize(worldMaterial, Vector3.zero, voxelScale);

        StartCoroutine(CoGenerateTerrainBoundsBased());*/
    }

    private void OnDrawGizmos()
    {
        Vector3 center = (minPosition.ConvertTo<Vector3>() + maxPosition.ConvertTo<Vector3>()) * (voxelScale / (int)2f);
        Vector3 size = (maxPosition.ConvertTo<Vector3>() - minPosition.ConvertTo<Vector3>())*voxelScale;

        Gizmos.color = new Color(1,  0, 0, 0.2f);

        Gizmos.DrawCube(center, size);
        Gizmos.DrawWireCube(center, size);
    }

    public VoxContainer GetChunk(Vector3Int chunkIndex)
    {
        if (voxelChunks.TryGetValue(chunkIndex, out VoxContainer chunk))
        {
            return chunk;
        }
        return null;
    }

    public VoxContainer GetMainContainer()
    {
        if (voxelChunks.TryGetValue(Vector3Int.zero, out VoxContainer container))
        {
            return container;
        }
        return null;
    }
    
    private IEnumerator CoGenerateTerrain()
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        voxelContainer.ClearData();
        
        Vector3 halfExtents = Vector3.one * (voxelScale * (overlapCheckSize / 2f));
        int voxelsDetected = 0;
        
        Vector3Int size = maxPosition - minPosition;
        int totalVoxels = size.x * size.y * size.z;
        int processedVoxels = 0;
        int voxelsThisFrame = 0;
        
        Debug.Log($"Starting voxelization of {totalVoxels} positions...");

        Collider[] colliderBuffer = new Collider[10];

        for (int x = minPosition.x; x < maxPosition.x; x++)
        {
            for (int z = minPosition.z; z < maxPosition.z; z++)
            {
                for (int y = minPosition.y; y < maxPosition.y; y++)
                {
                    Vector3 voxelPosition = new Vector3(x, y, z);
                    Vector3 checkPosition = (voxelPosition * voxelScale) + Vector3.one * (voxelScale * 0.5f);

                    int hitCount = Physics.OverlapBoxNonAlloc(checkPosition, halfExtents, colliderBuffer, Quaternion.identity, voxelDetectionLayers);

                    if (hitCount > 0)
                    {
                        bool foundValidCollider = false;
                        for (int i = 0; i < hitCount; i++)
                        {
                            if (colliderBuffer[i].GetComponent<VoxContainer>() == null)
                            {
                                foundValidCollider = true;
                                break;
                            }
                        }

                        if (foundValidCollider)
                        {
                            voxelContainer[voxelPosition] = new Voxel() { ID = 1 };
                            voxelsDetected++;
                        }
                    }
                    
                    processedVoxels++;
                    voxelsThisFrame++;

                    if (maxVoxelsCalculatedPerFrame > 0 && voxelsThisFrame >= maxVoxelsCalculatedPerFrame)
                    {
                        voxelsThisFrame = 0;
                        yield return null;
                    }
                }
            }
        }
        
        stopwatch.Stop();
        Debug.Log($"Voxelization completed! Created {voxelsDetected}/{totalVoxels} voxels in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:f2}seconds)");
        
        voxelContainer.GenerateMesh();
        voxelContainer.UploadMesh();
        Debug.Log("Mesh gen complete!");
    }
    
    private IEnumerator CoGenerateTerrainBoundsBased()
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        voxelContainer.ClearData();
        
        Collider[] allColliders = FindObjectsOfType<Collider>();
        List<Collider> validColliders = new List<Collider>();
        
        foreach (Collider col in allColliders)
        {
            // Check if the collider isn't part of the voxel grid
            if (((1 << col.gameObject.layer) & voxelDetectionLayers) != 0 && 
                col.GetComponent<VoxContainer>() == null)
            {
                validColliders.Add(col);
            }
        }
        
        Debug.Log($"Found {validColliders.Count} valid colliders to voxelize");
        
        if (validColliders.Count == 0)
        {
            Debug.LogWarning("No valid colliders found! Check your layer settings.");
            yield break;
        }
        
        Vector3 halfExtents = Vector3.one * (voxelScale * (overlapCheckSize / 2f));
        int voxelsDetected = 0;
        int voxelsChecked = 0;
        int voxelsThisFrame = 0;
        
        Collider[] colliderBuffer = new Collider[10];
        
        foreach (Collider col in validColliders)
        {
            Bounds bounds = col.bounds;
            
            Vector3Int boundsMin = new Vector3Int(
                Mathf.Max(minPosition.x, Mathf.FloorToInt(bounds.min.x / voxelScale)),
                Mathf.Max(minPosition.y, Mathf.FloorToInt(bounds.min.y / voxelScale)),
                Mathf.Max(minPosition.z, Mathf.FloorToInt(bounds.min.z / voxelScale))
            );
            
            Vector3Int boundsMax = new Vector3Int(
                Mathf.Min(maxPosition.x, Mathf.CeilToInt(bounds.max.x / voxelScale)),
                Mathf.Min(maxPosition.y, Mathf.CeilToInt(bounds.max.y / voxelScale)),
                Mathf.Min(maxPosition.z, Mathf.CeilToInt(bounds.max.z / voxelScale))
            );
            
            for (int x = boundsMin.x; x < boundsMax.x; x++)
            {
                for (int z = boundsMin.z; z < boundsMax.z; z++)
                {
                    for (int y = boundsMin.y; y < boundsMax.y; y++)
                    {
                        Vector3 voxelPosition = new Vector3(x, y, z);
                        
                        if (voxelContainer[voxelPosition].ID != 0) continue;
                        
                        Vector3 checkPosition = (voxelPosition * voxelScale) + Vector3.one * (voxelScale * 0.5f);
                        
                        int hitCount = Physics.OverlapBoxNonAlloc(
                            checkPosition,
                            halfExtents,
                            colliderBuffer,
                            Quaternion.identity,
                            voxelDetectionLayers
                        );
                        
                        if (hitCount > 0)
                        {
                            for (int i = 0; i < hitCount; i++)
                            {
                                if (colliderBuffer[i] == col)
                                {
                                    voxelContainer[voxelPosition] = new Voxel() { ID = 1 };
                                    voxelsDetected++;
                                    break;
                                }
                            }
                        }
                        
                        voxelsChecked++;
                        voxelsThisFrame++;
                        
                        if (maxVoxelsCalculatedPerFrame > 0 && voxelsThisFrame >= maxVoxelsCalculatedPerFrame)
                        {
                            voxelsThisFrame = 0;
                            yield return null;
                        }
                    }
                }
            }
        }
        
        stopwatch.Stop();
        Debug.Log($"Voxelization completed! Created {voxelsDetected} voxels in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:f2}seconds)");
        
        voxelContainer.GenerateMesh();
        voxelContainer.UploadMesh();
        Debug.Log("Mesh gen complete!");
    }

    private IEnumerator CoGenerateTerrainChunked()
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var chunk in voxelChunks.Values)
        {
            if (chunk != null)
            {
                Destroy(chunk.gameObject);
            }
        }
        voxelChunks.Clear();
        
        Collider[] allColliders = FindObjectsOfType<Collider>();
        List<Collider> validColliders = new List<Collider>();
        
        foreach (Collider col in allColliders)
        {
            // Check if the collider isn't part of the voxel grid
            if (((1 << col.gameObject.layer) & voxelDetectionLayers) != 0)
            {
                validColliders.Add(col);
            }
        }
        
        Debug.Log($"Found {validColliders.Count} valid colliders to voxelize");
        
        if (validColliders.Count == 0)
        {
            Debug.LogWarning("No valid colliders found! Check your layer settings.");
            yield break;
        }

        Vector3Int numberOfChunks = new Vector3Int(
            Mathf.CeilToInt((float)(maxPosition.x - minPosition.x) / chunkSize.x),
            Mathf.CeilToInt((float)(maxPosition.y - minPosition.y) / chunkSize.y),
            Mathf.CeilToInt((float)(maxPosition.z - minPosition.z) / chunkSize.z)
        );
        
        int totalChunks = numberOfChunks.x * numberOfChunks.y * numberOfChunks.z;
        int chunksCompleted = 0;
        
        Debug.Log($"Generating {totalChunks} chunks ({numberOfChunks.x}x{numberOfChunks.y}x{numberOfChunks.z})");
        
        Vector3 halfExtents = Vector3.one * (voxelScale * (overlapCheckSize / 2f));
        Collider[] colliderBuffer = new Collider[10];
        int totalVoxelsDetected = 0;

        // C stands for chunk (chunkX)
        for (int cx = 0; cx < numberOfChunks.x; cx++)
        {
            for (int cy = 0; cy < numberOfChunks.y; cy++)
            {
                for (int cz = 0; cz < numberOfChunks.z; cz++)
                {
                    Vector3Int chunkIndex = new Vector3Int(cx, cy, cz);
                    
                    Vector3Int chunkMin = minPosition + new Vector3Int(
                        cx * chunkSize.x,
                        cy * chunkSize.y,
                        cz * chunkSize.z);

                    Vector3Int chunkMax = new Vector3Int(
                        Mathf.Min(chunkMin.x + chunkSize.x, maxPosition.x),
                        Mathf.Min(chunkMin.y + chunkSize.y, maxPosition.y),
                        Mathf.Min(chunkMin.z + chunkSize.z, maxPosition.z));

                    GameObject chunkObject = new GameObject($"Chunk_{cx},{cy},{cz}");
                    chunkObject.transform.SetParent(chunksParent.transform);
                    VoxContainer chunk = chunkObject.AddComponent<VoxContainer>();

                    chunk.doNotSkipGeneratingAirBlocks = doNotSkipGeneratingAirBlocks;
                    chunk.doNotSkipGeneratingBackfaces =  doNotSkipGeneratingBackfaces;
                    chunk.Initialize(worldMaterial, voxelScale);
                    
                    voxelChunks[chunkIndex] = chunk;
                    
                    int chunkVoxelsDetected = 0;
                    int voxelsThisFrame = 0;


                    foreach (Collider col in validColliders)
                    {
                        if (col == null) continue;
                        if (col.GetComponent<VoxContainer>() != null) continue;

                        Bounds chunkBounds = new Bounds(
                            new Vector3(
                                (chunkMin.x + chunkMax.x) * 0.5f * voxelScale,
                                (chunkMin.y + chunkMax.y) * 0.5f * voxelScale,
                                (chunkMin.z + chunkMax.z) * 0.5f * voxelScale
                            ),
                            new Vector3(
                                (chunkMax.x - chunkMin.x) * voxelScale,
                                (chunkMax.y - chunkMin.y) * voxelScale,
                                (chunkMax.z - chunkMin.z) * voxelScale
                                ));
                        
                        if (!col.bounds.Intersects(chunkBounds)) continue;
                        
            
                        Vector3Int boundsMin = new Vector3Int(
                            Mathf.Max(chunkMin.x, Mathf.FloorToInt(col.bounds.min.x / voxelScale)),
                            Mathf.Max(chunkMin.y, Mathf.FloorToInt(col.bounds.min.y / voxelScale)),
                            Mathf.Max(chunkMin.z, Mathf.FloorToInt(col.bounds.min.z / voxelScale))
                        );
            
                        Vector3Int boundsMax = new Vector3Int(
                            Mathf.Min(chunkMax.x, Mathf.CeilToInt(col.bounds.max.x / voxelScale)),
                            Mathf.Min(chunkMax.y, Mathf.CeilToInt(col.bounds.max.y / voxelScale)),
                            Mathf.Min(chunkMax.z, Mathf.CeilToInt(col.bounds.max.z / voxelScale))
                        );

                        for (int x = boundsMin.x; x < boundsMax.x; x++)
                        {
                            for (int z = boundsMin.z; z < boundsMax.z; z++)
                            {
                                for (int y = boundsMin.y; y < boundsMax.y; y++)
                                {
                                    Vector3 voxelPosition = new Vector3(x, y, z);
                                    
                                    if (chunk[voxelPosition].ID != 0) continue;
                                    
                                    Vector3 checkPosition = (voxelPosition * voxelScale) + Vector3.one * (voxelScale * 0.5f);
                                    
                                    int hitCount = Physics.OverlapBoxNonAlloc(
                                        checkPosition,
                                        halfExtents,
                                        colliderBuffer,
                                        Quaternion.identity,
                                        voxelDetectionLayers
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
                                    
                                    voxelsThisFrame++;
                        
                                    if (maxVoxelsCalculatedPerFrame > 0 && voxelsThisFrame >= maxVoxelsCalculatedPerFrame)
                                    {
                                        voxelsThisFrame = 0;
                                        yield return null;
                                    }
                                }
                            }
                        }
                    }

                    if (chunkVoxelsDetected > 0)
                    {
                        chunk.GenerateMesh();
                        chunk.UploadMesh();
                        totalVoxelsDetected += chunkVoxelsDetected;
                    }
                    else
                    {
                        Destroy(chunkObject);
                        voxelChunks.Remove(chunkIndex);
                    }

                    chunksCompleted++;
                    Debug.Log($"Chunk {chunksCompleted}/{totalChunks} complete ({chunkVoxelsDetected} voxels)");
                    yield return null;
                }
            }
        }
        
        stopwatch.Stop();
        Debug.Log($"All chunks complete! Total: {totalVoxelsDetected} voxels in {chunksCompleted} chunks");
        Debug.Log($"Time taken: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F2} seconds)");
    }

    [ContextMenu("RegenerateVoxels")]
    public void RegenerateVoxels()
    {
        StopAllCoroutines();
        if (useProgressiveChunkGeneration)
        {
            StartCoroutine(CoGenerateTerrainChunked());
        }
        else
        {
            StartCoroutine(CoGenerateTerrainBoundsBased());
        }
    }
}