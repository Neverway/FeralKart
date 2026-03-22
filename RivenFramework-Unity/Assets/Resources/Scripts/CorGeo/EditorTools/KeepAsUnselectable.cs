using UnityEngine;

#if UNITY_EDITOR
using static CSG_HotFix_Utility;
#endif


[ExecuteAlways]
public class KeepAsUnselectable : MonoBehaviour
{
#if UNITY_EDITOR
    public bool isSelectable = false;
    public bool includeChildren = true;

    void Update()
    {
        PickabilityUtility.SetPickabilityWithoutUndo(gameObject, isSelectable, includeChildren);
    }
#endif
}
