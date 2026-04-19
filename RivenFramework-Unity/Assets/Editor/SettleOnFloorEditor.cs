//===================== (Neverway 2026) Written by Connorses. =====================
//
// Purpose: Put button in Inspector on the SetPropHeight component.
// Notes:
//
//=============================================================================

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SettleOnFloor))]
[CanEditMultipleObjects]

public class SettleOnFloorEditor : Editor
{

    public override void OnInspectorGUI ()
    {
        //Called whenever the inspector is drawn for this object.

        DrawDefaultInspector ();


        if (GUILayout.Button ("Set Height"))
        {
            foreach (Object selectedObject in targets)
            {
                SettleOnFloor settleOnFloor = (SettleOnFloor)selectedObject;
                Undo.RecordObject(settleOnFloor.transform, "Set Height");
                settleOnFloor.SetHeightWithRaycast();
                EditorUtility.SetDirty(settleOnFloor);
            }
        }
    }
}