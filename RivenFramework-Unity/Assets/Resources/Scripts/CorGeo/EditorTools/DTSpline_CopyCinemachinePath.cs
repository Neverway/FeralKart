//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Dreamteck.Splines;
using UnityEditor;
using UnityEngine;
public class DTSpline_CopyCinemachinePath : MonoBehaviour
{
    [MenuItem("GameObject/Spline/Copy Cinemachine Path To DT Spline", false, 0)]
    public static void CopyCinemachinePath()
    {
        var cinemachinePath = Selection.activeGameObject.GetComponent<CinemachineSmoothPath>();
        var dtspline = Selection.activeGameObject.transform.GetChild(0).GetComponent<SplineComputer>();

        if (cinemachinePath == null)
        {
            Debug.LogWarning("Couldn't find Cinemachine Path component, Ensure that the cinemachine path is selected");
        }
        if (dtspline == null)
        {
            Debug.LogWarning("Couldn't find child DTSpline, Ensure that the DTSpline component is the first child of the cinemachine path");
        }
        
        List<SplinePoint> newSpline = new List<SplinePoint>();
        
        
        print("commencing...");
        print(Selection.activeGameObject.name);
        print(cinemachinePath.m_Waypoints);
        print(cinemachinePath.m_Waypoints[0].position);
        foreach (var waypoint in cinemachinePath.m_Waypoints)
        {
            var splinePoint = new SplinePoint();
            splinePoint.position = waypoint.position;
            splinePoint.size = 1;
            splinePoint.normal = Vector3.up;
            newSpline.Add(splinePoint);
        }
        
        dtspline.SetPoints(newSpline.ToArray(), SplineComputer.Space.Local);

    }
}
#endif