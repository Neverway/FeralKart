//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
// Created following this guide: https://youtu.be/EubjobNVJdM
//====================================================================================================================//

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Used on 'chunks' to build the 3D grid of voxels
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class VoxContainer : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/

    
    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("How big in world units each voxel is (default is 1 (1 meter))")]
    public float voxelScale = 1.0f;
    [Tooltip("In a standard voxel system there is no need to generate a voxel for air, enable this if you want to generate air blocks")]
    public bool doNotSkipGeneratingAirBlocks = false;
    [Tooltip("In a standard voxel system you don't usually want to draw faces that can't be seen or are covering each other, enable this if you want to generate touching faces")]
    public bool doNotSkipGeneratingBackfaces = false;
    [Tooltip("...")]
    public Dictionary<Vector3, Voxel> containerData;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    // Serializable storage for voxel data (Unity can't serialize Dictionary)
    [SerializeField, HideInInspector]
    private List<Vector3> serializedVoxelPositions = new List<Vector3>();
    [SerializeField, HideInInspector]
    private List<byte> serializedVoxelIDs = new List<byte>();
    
    private VoxMeshData voxMeshData;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion
    
    
    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/

    
    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Get the mesh components required for rendering the voxels
    /// </summary>
    private void ConfigureComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }
    

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Create a container object to store the voxel chunks
    /// </summary>
    /// <param name="material">The material to assign to the voxels by default</param>
    /// <param name="scale">The scale of the voxels in the 3D grid</param>
    public void Initialize(Material material, float scale = 1.0f)
    {
        ConfigureComponents();
        
        // Assign the voxel grid to the voxel layer so it doesn't get in the way of players, phys props, etc.
        int voxelLayer = LayerMask.NameToLayer("Voxel Grid");
        gameObject.layer = voxelLayer;
        
        containerData = new Dictionary<Vector3, Voxel>();
        meshRenderer.sharedMaterial = material;
        voxelScale = scale;
    }
    
    /// <summary>
    /// Clear the voxel container data
    /// </summary>
    public void ClearData()
    {
        containerData.Clear();
    }
    
    /// <summary>
    /// Create the actual data for combined mesh that represents the voxels
    /// </summary>
    public void GenerateMesh()
    {
        voxMeshData.ClearData();

        if (containerData.Count == 0)
        {
            Debug.Log("No voxel data to generate mesh from since the container data was empty");
            return;
        }

        Vector3 blockPos;
        Voxel block;

        int counter = 0;
        Vector3[] faceVertices = new Vector3[4];
        Vector2[] faceUVs = new Vector2[4];
        
        int estimatedFaces = containerData.Count * 3;
        int estimatedVertices = estimatedFaces * 6;
        voxMeshData.vertices.Capacity = estimatedVertices;
        voxMeshData.triangles.Capacity = estimatedVertices;
        voxMeshData.UVs.Capacity = estimatedVertices;
        
        // Iterate over each face direction
        foreach (KeyValuePair<Vector3, Voxel> kvp in containerData)
        {
            // Don't bother creating a voxel for air
            if (kvp.Value.ID == 0 && !doNotSkipGeneratingAirBlocks) continue;
            
            blockPos = kvp.Key;
            block = kvp.Value;
            
            int voxelFacesCount = 6;
            for (int i = 0; i < voxelFacesCount; i++)
            {
                // Backface culling
                if (this[blockPos + voxelFaceChecks[i]].isSolid && !doNotSkipGeneratingBackfaces) continue;
                
                // Draw this face
                // Collect the appropriate vertices from the default vertices and add the block position
                int faceVerticesCount = 4;
                for (int j = 0; j < faceVerticesCount; j++)
                {
                    faceVertices[j] = (voxelVertices[voxelVertexIndex[i, j]] * voxelScale) + (blockPos * voxelScale);
                    faceUVs[j] = voxelUVs[j];
                }

                for (int j = 0; j < 6; j++)
                {
                    voxMeshData.vertices.Add(faceVertices[voxelTris[i,j]]);
                    voxMeshData.UVs.Add(faceUVs[voxelTris[i,j]]);
                    //voxMeshData.colors.Add(voxelColorAlpha);
                    //voxMeshData.UVs2.Add(voxelSmoothness);
                    voxMeshData.triangles.Add(counter++);
                }
            }
        }
    }
    
    /// <summary>
    /// Render out the voxels in this chunk
    /// </summary>
    public void UploadMesh()
    {
        voxMeshData.UploadMesh();

        if (meshRenderer == null) ConfigureComponents();

        meshFilter.mesh = voxMeshData.mesh;
        if (voxMeshData.vertices.Count > 3) meshCollider.sharedMesh = voxMeshData.mesh;
    }

    #endregion
    
    
    public Voxel this[Vector3 index]
    {
        get
        {
            if (containerData != null && containerData.ContainsKey(index))
            {
                return containerData[index];
            }
            else
            {
                return emptyVoxel;
            }
        }

        set
        {
            if (containerData == null)
            {
                containerData = new Dictionary<Vector3, Voxel>();
            }
            
            if (containerData.ContainsKey(index))
            {
                containerData[index] = value;
            }
            else
            {
                containerData.Add(index, value);
            }
        }
    }
    
    #region  Voxel Mesh Data
    public struct VoxMeshData
    {
        public Mesh mesh;
        public List<Vector3> vertices;
        public List<int> triangles;
        public List<Vector2> UVs;
        //public List<Vector2> UVs2;
        //public List<Color> colors;

        public bool initialized;

        public void ClearData()
        {
            if (!initialized)
            {
                vertices = new List<Vector3>();
                triangles = new List<int>();
                UVs = new List<Vector2>();
                //UVs2 = new List<Vector2>();
                //colors = new List<Color>();
                
                initialized = true;
                mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            else
            {
                vertices.Clear();
                triangles.Clear();
                UVs.Clear();
                //UVs2.Clear();
                //colors.Clear();
                mesh.Clear();
            }
        }
        /// <summary>
        /// Assign the vertices, triangles, and uvs HEHEHEHA
        /// </summary>
        /// <param name="sharedVerticies"></param>
        public void UploadMesh(bool sharedVerticies = false)
        {
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, false);
            //mesh.SetColors(colors);
            mesh.SetUVs(0, UVs);
            //mesh.SetUVs(2, UVs2);
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            mesh.Optimize();
            
            mesh.UploadMeshData(false);
        }
    }
    #endregion
    
    #region Voxel Statics

    /// <summary>
    /// Defines the basic shape of a cubic voxel's vertex points
    /// </summary>
    private static readonly Vector3[] voxelVertices = new Vector3[8]
    {
        new Vector3(0, 0, 0), // vertex 0 (bottom left)
        new Vector3(1, 0, 0), // vertex 1 (bottom right)
        new Vector3(0, 1, 0), // vertex 2 (top right)
        new Vector3(1, 1, 0), // vertex 3 (top left)
        
        new Vector3(0, 0, 1), // vertex 4 (bottom left)
        new Vector3(1, 0, 1), // vertex 5 (bottom right)
        new Vector3(0, 1, 1), // vertex 6 (top right)
        new Vector3(1, 1, 1), // vertex 7 (top left)
    };

    private static Vector3[] voxelFaceChecks = new Vector3[6]
    {
        new Vector3(0, 0, -1), // Back
        new Vector3(0, 0, 1), // Front
        new Vector3(-1, 0, 0), // Left
        new Vector3(1, 0, 0), // Right
        new Vector3(0, -1, 0), // Bottom
        new Vector3(0, 1, 0), // Top
    };

    /// <summary>
    /// I believe this defines the basic shape of a cubic voxel's vertex connections
    /// </summary>
    private static readonly int[,] voxelVertexIndex = new int[6, 4]
    {
        { 0, 1, 2, 3 },
        { 4, 5, 6, 7 },
        { 4, 0, 6, 2 },
        { 5, 1, 7, 3 },
        { 0, 1, 4, 5 },
        { 2, 3, 6, 7 },
    };

    private static readonly Vector2[] voxelUVs = new Vector2[4]
    {
        new Vector2(0, 0),
        new Vector2(0, 1),
        new Vector2(1, 0),
        new Vector2(1, 1)
    };

    private static readonly int[,] voxelTris = new int[6, 6]
    {
        { 0, 2, 3, 0, 3, 1 },
        { 0, 1, 2, 1, 3, 2 },
        { 0, 2, 3, 0, 3, 1 },
        { 0, 1, 2, 1, 3, 2 },
        { 0, 1, 2, 1, 3, 2 },
        { 0, 2, 3, 0, 3, 1 }
    };
    
    public static Voxel emptyVoxel = new Voxel() { ID = 0 };

    #endregion
    
    #region Serialization
    
    /// <summary>
    /// Manually serialize voxel data to lists (called by editor before saving)
    /// </summary>
    public void SerializeVoxelData()
    {
        if (containerData == null || containerData.Count == 0)
            return;
        
        serializedVoxelPositions.Clear();
        serializedVoxelIDs.Clear();
        
        foreach (var kvp in containerData)
        {
            serializedVoxelPositions.Add(kvp.Key);
            serializedVoxelIDs.Add(kvp.Value.ID);
        }
    }
    
    /// <summary>
    /// Deserialize voxel data from lists (called when loading pre-baked chunks)
    /// </summary>
    public void DeserializeVoxelData()
    {
        if (serializedVoxelPositions == null || serializedVoxelPositions.Count == 0)
            return;
        
        if (containerData == null)
            containerData = new Dictionary<Vector3, Voxel>();
        
        containerData.Clear();
        
        for (int i = 0; i < serializedVoxelPositions.Count && i < serializedVoxelIDs.Count; i++)
        {
            containerData[serializedVoxelPositions[i]] = new Voxel { ID = serializedVoxelIDs[i] };
        }
        
        //Debug.Log($"Deserialized {containerData.Count} voxels from saved data");
    }
    
    #endregion
}