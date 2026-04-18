//===================== (Neverway 2026) Written by Connorses. =====================
//
// Purpose: Put button in Inspector on the SetPropHeight component.
// Notes:
//
//=============================================================================

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SetPropHeight))]
[CanEditMultipleObjects]

public class SetPropHeightEditor : Editor
{

    public override void OnInspectorGUI ()
    {
        //Called whenever the inspector is drawn for this object.

        DrawDefaultInspector ();

        SetPropHeight setPropHeight = (SetPropHeight)target;

        if (GUILayout.Button ("Set Height"))
        {
            setPropHeight.SetHeightWithRaycast ();
        }
    }
}