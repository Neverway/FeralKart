//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections.Generic;
using UnityEngine;

public class CorGeo_PlaneIntersectionUtil : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private static Vector3[] GetBoundingBoxCorners(Vector3 _boundingBoxMin, Vector3 _boundingBoxMax)
    {
        Vector3[] boundingBoxCorners = new Vector3[8];
        boundingBoxCorners[0] = new Vector3(_boundingBoxMin.x, _boundingBoxMin.y, _boundingBoxMin.z);
        boundingBoxCorners[1] = new Vector3(_boundingBoxMax.x, _boundingBoxMin.y, _boundingBoxMin.z);
        boundingBoxCorners[2] = new Vector3(_boundingBoxMax.x, _boundingBoxMin.y, _boundingBoxMin.z);
        boundingBoxCorners[3] = new Vector3(_boundingBoxMin.x, _boundingBoxMax.y, _boundingBoxMin.z);
        boundingBoxCorners[4] = new Vector3(_boundingBoxMin.x, _boundingBoxMax.y, _boundingBoxMax.z);
        boundingBoxCorners[5] = new Vector3(_boundingBoxMax.x, _boundingBoxMin.y, _boundingBoxMax.z);
        boundingBoxCorners[6] = new Vector3(_boundingBoxMin.x, _boundingBoxMax.y, _boundingBoxMax.z);
        boundingBoxCorners[7] = new Vector3(_boundingBoxMax.x, _boundingBoxMin.y, _boundingBoxMax.z);
        return boundingBoxCorners;
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public static bool BoundingBoxIntersectsPlane(Bounds bounds, Plane plane)
    {
        Vector3 boundingBoxMin = bounds.min;
        Vector3 boundingBoxMax = bounds.max;
        var boundingBoxCorners = GetBoundingBoxCorners(boundingBoxMin, boundingBoxMax);

        bool hasPointInPositive = false;
        bool hasPointInNegative = false;

        foreach (Vector3 corner in boundingBoxCorners)
        {
            float distance = plane.GetDistanceToPoint(corner);
            
            if (distance > 0) hasPointInPositive = true;
            else if (distance < 0) hasPointInNegative = true;

            if (hasPointInPositive && hasPointInNegative) return true;
        }

        return false;
    }

    public static int GetPlaneIntersectionSide(Bounds bounds, Plane plane)
    {
        Vector3 boundingBoxMin = bounds.min;
        Vector3 boundingBoxMax = bounds.max;
        var boundingBoxCorners = GetBoundingBoxCorners(boundingBoxMin, boundingBoxMax);

        bool hasPointInPositive = false;
        bool hasPointInNegative = false;

        foreach (Vector3 corner in boundingBoxCorners)
        {
            float distance = plane.GetDistanceToPoint(corner);
            
            if (distance > 0) hasPointInPositive = true;
            else if (distance < 0) hasPointInNegative = true;
        }
        
        if (!hasPointInPositive && hasPointInNegative) return -1;
        if (hasPointInPositive && hasPointInNegative) return 0;
        if (hasPointInPositive && !hasPointInNegative) return 1;

        // If a bounding box is returning 2, something is very, very, wrong
        return 2;
    }
    
    public static List<CorGeo_SliceableMesh> GetIntersectingMeshes(Plane plane, List<CorGeo_SliceableMesh> sliceableMeshes)
    {
        List<CorGeo_SliceableMesh> intersectedSliceableMeshes = new List<CorGeo_SliceableMesh>();


        foreach (var sliceableMesh in sliceableMeshes)
        {
            if (IsMeshIntersectingPlane(plane, sliceableMesh)) intersectedSliceableMeshes.Add(sliceableMesh);
        }
        
        return intersectedSliceableMeshes;
    }
    
    public static bool IsMeshIntersectingPlane(Plane plane, CorGeo_SliceableMesh sliceableMesh)
    {
        var renderer = sliceableMesh.meshRenderer;
        if (renderer == null) throw new Exception($"sliceableMesh '{sliceableMesh}' doesn't have renderer, this is... strange");
            
        var bounds =  renderer.bounds;

        return BoundingBoxIntersectsPlane(bounds, plane);
    }


    #endregion
}
