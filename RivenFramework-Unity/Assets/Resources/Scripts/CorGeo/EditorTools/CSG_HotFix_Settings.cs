//SCRIPT BY ERRYNEI

//While editing SabreCSG scripts, I have referred to flags in this static class so that if my hotfixes cause any
//   problems, you should be able to change the flags here and it will skip my hotfix. You can also use this to
//   refer to anywhere I have added code in the CSG scripts


using Sabresaurus.SabreCSG;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CSG_HotFix_Settings
{
    //-----------------------------------------------------------------------------------------------------------------
    //                                             Hotfix flags
    //-----------------------------------------------------------------------------------------------------------------

    /* Detailed description of hotfix
        *--------------------------------------------------------------------------------------------------------------
        * This will suppress most controls in CSG if you are ever holding the "alt" key. The purpose of this is to stop
        *   SabreCSG from preferring to select or edit things over rotating the camera view. There isnt a lot of cases 
        *   where the alt key added functionality anyways, and oftentimes for me I would me in face select mode, and
        *   want to rotate the camera view to select another face, but clicking would select or drag the UVs of a face
        *   instead, EVEN when holding down "alt" which doesnt even change selecting or translating face UVs anyways.
        *   So I added this hotfix since rotating the camera view this way was an important feature for me to navigate 
        *   Unity in general. There might possibly be some features I am overriding with this fix, since it removes
        *   the ability to click-and-drag anything in CSG if alt is held at all, but I don't know of any such features
        *--------------------------------------------------------------------------------------------------------------
     */
    // Ensures that holding "alt" when clicking and dragging will rotate the scene camera instead of CSG 
    public static readonly bool FixAltClickCameraRotate = true;

    /* Detailed description of hotfix
        *--------------------------------------------------------------------------------------------------------------
        * The [Space] key in SabreCSG normally rotates through the different available tools. But, I personally find
        *   this hardly useful, since there are 5 different tools and you have to press it between 1 to 4 times to
        *   get to the tool you want. So, instead, this flag will make it so that the [Space] key will toggle between
        *   editing a CSGModel and using other EditorTools for moving other non-CSG GameObjects around. 
        * SabreCSG overrides all EditorTools when in edit mode for a CSGModel, and this is annoying when you want
        *   to move back and fourth between editing anything else, such as simply moving scene objects around. You 
        *   cant even select other objects when you're in CSG edit mode, so I figured this would be a much more useful
        *   feature.  (The CSGModel that you enter edit mode for is the one that was last being edited)
        *--------------------------------------------------------------------------------------------------------------
     */
    // Changes the [Space] key to toggle between editmode for the last selected CSGMdoel instead of rotating CSGTools
    public static readonly bool RebindSpaceKeyToToggleCSGEdit = true;

    /* Detailed description of hotfix
        *--------------------------------------------------------------------------------------------------------------
        * Before this, SabreCSG just didnt rebuild the mesh on Undo or Redo, which is strange because there is already
        *   code that repaints and fixes things on Undos or Redos. So I just slipped a "Force Rebuild" in there if you
        *   have "Auto Rebuild" enabled. There may have been a reason they did not include that, but so far it seems
        *   to make everything much easier and more intuitive to edit
        *   
        *   I could add an optimization in the future to only force rebuild on CSGModels that are a part of the latest
        *   undo or redo by making use of the Undo.postprocessModifications event. Currently my hotfix forces rebuild
        *   on all CSGModels, which may be overkill if there is a lot of them
        *--------------------------------------------------------------------------------------------------------------
     */
    // Makes it so if "Auto Rebuild" is enabled, and you preform an undo or redo, a rebuild will be forced
    public static readonly bool AutoRebuildOnUndoOrRedo = true;

    public static readonly bool ResizeModeKeybindsAlsoWorkOnOtherTools = true;
    public static readonly string ExtraToolButton1 = "r";

    public static readonly bool FixUVEditingUndoRegistering = true;

    public static readonly bool SwapShiftAndNonShiftControlsForFaceEdit = true;


    // HOTFIXES TO IMPLEMENT LATER : 
#pragma warning disable CS0414
    private static readonly bool AddCustomMaterialPaintTool = false;
    private static readonly bool UseCachedGeometryDuringRotateBrush = false;
    private static readonly bool AddGridDoublingAndHalfingControls = false;
    private static readonly bool FixSetMaterialUndoRegistering = false;
#pragma warning restore CS0414

    //-----------------------------------------------------------------------------------------------------------------
    //                                 Important Links to Classes or Methods
    //-----------------------------------------------------------------------------------------------------------------

    //----- Settings Classes and Singletons ---------------------------------------------------------------------------

    ///<see cref= Sabresaurus.SabreCSG.KeyMappings
    ///<see cref= Sabresaurus.SabreCSG.EditorKeyMappings


    //----- Important Functionality of CSG ----------------------------------------------------------------------------
    ///<see cref= Sabresaurus.SabreCSG.CSGModel
    ///<see cref= Sabresaurus.SabreCSG.CSGModel.CleanupForBuild
    ///<see cref= Sabresaurus.SabreCSG.CSGModel.OnGenericKeyAction



    //----- Tools --------------------------------------------------------------------------------------------------------------------
    ///<see cref= Sabresaurus.SabreCSG.ResizeEditor    Resize
    ///<see cref= Sabresaurus.SabreCSG.VertexEditor    Vertex
    ///<see cref= Sabresaurus.SabreCSG.SurfaceEditor   Face
    ///<see cref= Sabresaurus.SabreCSG.ClipEditor      Clip
    ///<see cref= Sabresaurus.SabreCSG.DrawEditor      Draw
    ///<see cref= Sabresaurus.SabreCSG.CSGTool_MaterialPaint  Custom tool by Errynei


    //----- Customly programmed CSG features --------------------------------------------------------------------------
    ///<see cref= Sabresaurus.SabreCSG.CSGTool_MaterialPaint
    ///<see cref= CSGForceRebuildOnUndo



}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class CSG_HotFix_Utility
{
    public static CSGModel LastEditedCSGModel;

    static CSG_HotFix_Utility()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (CSG_HotFix_Settings.RebindSpaceKeyToToggleCSGEdit)
            OnHotfix_RebindSpaceKeyToToggleCSGEdit();
    }

    private static void OnHotfix_RebindSpaceKeyToToggleCSGEdit()
    {
        if (LastEditedCSGModel == null)
            return;


        //Toggle EditMode on the last edited CSGModel when pressing the toggle CSG edit mode button
        if (IsPressingKey(KeyMappings.Instance.ToggleCSGEditMode))
        {
            LastEditedCSGModel.EditMode ^= true; //Toggles
        }
            

    }

    private static bool IsPressingKey(string keyString) =>
        KeyMappings.EventsMatch(Event.current, Event.KeyboardEvent(keyString)) && Event.current.type == EventType.KeyDown;
    private static bool IsHoldingKey(string keyString) =>
        KeyMappings.EventsMatch(Event.current, Event.KeyboardEvent(keyString));

    public static class PickabilityUtility
    {
        private static MethodInfo DirectlySetPickabilityMethod;

        public static void SetPickabilityWithoutUndo(GameObject obj, bool pickable, bool includeChildren)
        {
            if (DirectlySetPickabilityMethod == null)
                SetupPickabilityMethod();

            DirectlySetPickabilityMethod.Invoke(null, new object[] { 
                /* Target: */ obj,  
                /* Disable Pickability?: */ !pickable, 
                /* Include Children?: */ includeChildren
            });

            SceneView.RepaintAll();
        }
        private static void SetupPickabilityMethod()
        {
            if (DirectlySetPickabilityMethod != null)
                return;

            // Get the UnityEditor assembly
            var editorAssembly = typeof(SceneVisibilityManager).Assembly;

            // Fully qualified name (namespace + type name)
            string typeName = "UnityEditor.SceneVisibilityState";
            string methodName = "SetGameObjectPickingDisabled";

            // Try to get the internal type, use fallback method if could not find it
            Type typeToGetMethodFrom = editorAssembly.GetType(typeName);
            if (typeToGetMethodFrom == null)
            {
                Debug.LogWarning("SceneVisibilityState type not found, cannot set pickability of objects");
                typeToGetMethodFrom = typeof(PickabilityUtility);
            }

            DirectlySetPickabilityMethod = typeToGetMethodFrom.GetMethod(methodName, 
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            if (DirectlySetPickabilityMethod == null)
                throw new Exception("Could not get a method for setting pickability. Fallback didnt even work somehow");
        }

        private static void FallbackMethod(GameObject gameObject, bool pickingDisabled, bool includeChildren)
        {
            //Do nothing! For now at least, this will prob never get called
        }
    }
}
#endif

//Use Ctrl + F and search "CSG HOTFIX" in current project to find all hotfixes I have added
//All hotfixes I add will be surrounded by this set of comments:

//---------- CSG HOTFIX START : By Errynei
//   <Code here>
//---------- END OF HOTFIX
