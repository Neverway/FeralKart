#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ProBuilder;
#endif

using UnityEngine;
using UnityEngine.ProBuilder;

[DisallowMultipleComponent]
public class BulbOutletStrip : MonoBehaviour, BulbCollisionBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    public float snapDistance;

    [Space, Header("EditorHelpers")]
    public ProBuilderMesh meshToModify;

    public Vector3 StripVectorNormalized => StripVector.normalized;
    public Vector3 StripVector => (endPoint.position - startPoint.position);

    public bool OnBulbCollision(Projectile_Marker bulb, RaycastHit hit)
    {
        //Get closest point between start and end in a line
        Vector3 attachPos = GetClosestPointOnLineSegment(hit.point, startPoint.position, endPoint.position);

        //Process position snapping
        attachPos = SnapPosition(attachPos);

        //Attach bulb to position
        bulb.MarkerPinAt(attachPos, startPoint.forward);
        return true;
    }

    public Vector3 SnapPosition(Vector3 position)
    {
        if (snapDistance <= 0f)
            return position;

        Vector3 attachVector = position - startPoint.position;
        float snappedMagnitude = Mathf.Round(attachVector.magnitude / snapDistance) * snapDistance;
        attachVector = (attachVector.normalized) * snappedMagnitude;
        return startPoint.position + attachVector;
    }

#if UNITY_EDITOR
    //bleh
    public void EDITOR_StretchProbuilderMesh(Vector3 handle1_position, Vector3 handle2_position)
    {
        //Convert global positions of handles into local mesh positions
        handle1_position = meshToModify.transform.InverseTransformPoint(handle1_position);
        handle2_position = meshToModify.transform.InverseTransformPoint(handle2_position);

        List<int> vertexIndexes_handle1 = new List<int>(); //List of vertices closest to handle 1
        Vector3 averageVertexPos_handle1 = Vector3.zero; //Average position of all those vertices

        List<int> vertexIndexes_handle2 = new List<int>(); //List of vertices closest to handle 1
        Vector3 averageVertexPos_handle2 = Vector3.zero; //Average position of all those vertices

        // Get mesh vertices from probuilder mesh
        Vertex[] vertices = meshToModify.GetVertices();
        //Loop through all of those verticies
        for (int i = 0; i < vertices.Length; i++)
        {
            //Get distance from this vertex to each handle
            float distanceToHandle1 = Vector3.Distance(handle1_position, vertices[i].position);
            float distanceToHandle2 = Vector3.Distance(handle2_position, vertices[i].position);

            //Check which handle is closer
            if (distanceToHandle1 <= distanceToHandle2) {
                //For handle 1 : 
                vertexIndexes_handle1.Add(i); //Add to handle 1 vertex list
                averageVertexPos_handle1 += vertices[i].position; //Add to handle 1 vertex position sum (to average later)
            } else {
                //For handle 2 : 
                vertexIndexes_handle2.Add(i); //Add to handle 1 vertex list
                averageVertexPos_handle2 += vertices[i].position; //Add to handle 1 vertex position sum (to average later)
            }
        }

        //With vertices collected and associated with each handle, move them such that their average position is AT the handle

        //For handle 1 : 
        averageVertexPos_handle1 /= vertexIndexes_handle1.Count; //Get final average of vertices position
        Vector3 moveVector_handle1 = handle1_position - averageVertexPos_handle1; //Get offset from average position to handle
        foreach (int i in vertexIndexes_handle1)
            vertices[i].position = vertices[i].position + moveVector_handle1; //Apply offset to all vertices closest to this handle

        //For handle 2 : 
        averageVertexPos_handle2 /= vertexIndexes_handle2.Count; //Get final average of vertices position
        Vector3 moveVector_handle2 = handle2_position - averageVertexPos_handle2; //Get offset from average position to handle
        foreach (int i in vertexIndexes_handle2)
            vertices[i].position = vertices[i].position + moveVector_handle2; //Apply offset to all vertices closest to this handle

        // Apply the changes to the mesh
        meshToModify.SetVertices(vertices, true);
        meshToModify.ToMesh();
        meshToModify.Refresh(RefreshMask.All);
        ProBuilderEditor.Refresh(true);
    }
#endif

    // Finds the closest point on a line segment between start and end to a given point
    public static Vector3 GetClosestPointOnLineSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        // Calculate the vector from start to end
        Vector3 lineVector = end - start;

        // Calculate the vector from start to the point
        Vector3 pointVector = point - start;

        // Calculate the projection of pointVector onto lineVector
        float lineLengthSquared = lineVector.sqrMagnitude; // Length of the line segment squared
        float dotProduct = Vector3.Dot(pointVector, lineVector); // Dot product of the vectors

        // Get the parameter t that represents how far along the line segment the projection falls
        float t = Mathf.Clamp01(dotProduct / lineLengthSquared);

        // Calculate the closest point using the parameter t
        return start + t * lineVector;
    }
}








#if UNITY_EDITOR
[CustomEditor(typeof(BulbOutletStrip))]
public class BulbOutletStripEditor : Editor
{
    private bool holdingHandle = false;
    private bool registeredUndo = false;

    private void OnEnable()
    {
        Undo.undoRedoPerformed -= RefreshMesh;
        Undo.undoRedoPerformed += RefreshMesh;
    }
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= RefreshMesh;
    }

    private void RefreshMesh()
    {
        BulbOutletStrip outletStrip = (BulbOutletStrip)target;
        outletStrip.meshToModify.ToMesh();
        outletStrip.meshToModify.Refresh(RefreshMask.All);
        ProBuilderEditor.Refresh(true);
    }


    private void OnSceneGUI()
    {
        BulbOutletStrip outletStrip = (BulbOutletStrip)target;
        Vector3 oldStartPos = outletStrip.startPoint.position;
        Vector3 oldEndPos = outletStrip.endPoint.position;

        bool changeOccurred = false;
        //bool changeWasStartHandle = false; // Was working on this
        bool holdingControl = Event.current.control;

        // Start Handle
        EditorGUI.BeginChangeCheck();
        Vector3 newStartPosition = Handles.Slider(oldStartPos, outletStrip.StripVector * -1f);

        if (EditorGUI.EndChangeCheck())
        {
            changeOccurred = true;
            newStartPosition = outletStrip.SnapPosition(newStartPosition);
            newStartPosition = BulbOutletStrip.GetClosestPointOnLineSegment(
                newStartPosition, 
                oldStartPos + outletStrip.StripVectorNormalized * -20f,
                oldEndPos + outletStrip.StripVectorNormalized * 20f);

            if (Vector3.Distance(newStartPosition, oldEndPos) > 1.5f && 
                Vector3.Angle(newStartPosition - outletStrip.transform.position, outletStrip.StripVector * -1f) == 0)
            {
                //Record a change occurring if there is a change in final position
                //changeOccurred |= outletStrip.startPoint.position != newStartPosition;
                outletStrip.startPoint.position = newStartPosition;
                UnityEditor.EditorUtility.SetDirty(outletStrip.startPoint);
            }
        }

        // End Handle
        EditorGUI.BeginChangeCheck();
        Vector3 newEndPosition = Handles.Slider(oldEndPos, outletStrip.StripVector * 1f);

        if (EditorGUI.EndChangeCheck())
        {
            changeOccurred = true;
            newEndPosition = outletStrip.SnapPosition(newEndPosition);
            newEndPosition = BulbOutletStrip.GetClosestPointOnLineSegment(
                newEndPosition,
                oldStartPos + outletStrip.StripVectorNormalized * -20f,
                oldEndPos + outletStrip.StripVectorNormalized * 20f);
            if (Vector3.Distance(newEndPosition, oldStartPos) > 1.5f &&
                Vector3.Angle(newEndPosition - outletStrip.transform.position, outletStrip.StripVector * 1f) == 0)
            {
                //Record a change occurring if there is a change in final position
                //changeOccurred |= outletStrip.endPoint.position != newEndPosition;
                outletStrip.endPoint.position = newEndPosition;
                UnityEditor.EditorUtility.SetDirty(outletStrip.endPoint);
            }
        }

        if (changeOccurred)
        {
            holdingHandle = true;
            if (!registeredUndo)
            {
                Undo.RegisterCompleteObjectUndo(outletStrip.gameObject, "OutletStrip");
                registeredUndo = true;
            }




            outletStrip.EDITOR_StretchProbuilderMesh(outletStrip.startPoint.position, outletStrip.endPoint.position);
        }
        if (holdingHandle && GUIUtility.hotControl == 0)
        {
            holdingHandle = false;
            registeredUndo = false;

            UnityEditor.EditorUtility.SetDirty(outletStrip.meshToModify);
            UnityEditor.EditorUtility.SetDirty(outletStrip.meshToModify.gameObject);
            UnityEditor.EditorUtility.SetDirty(outletStrip.startPoint.transform);
            UnityEditor.EditorUtility.SetDirty(outletStrip.endPoint.transform);

            //Undo.CollapseUndoOperations(undoGroup);
            Undo.IncrementCurrentGroup();
        }
    }
}
#endif