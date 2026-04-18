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

        SettleOnFloor setPropHeight = (SettleOnFloor)target;

        if (GUILayout.Button ("Set Height"))
        {
            setPropHeight.SetHeightWithRaycast ();
        }
    }
}