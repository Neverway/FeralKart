//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
// 
// Contributors: 
//  Connorses, Errynei, Soulex
//
//====================================================================================================================//

using System;
using System.Collections;
using RivenFramework;
using UnityEngine;

/// <summary>
///  This is a Level Blueprint (LB) script, it is attached to the WorldSettings
///  object in a scene.
///  This LB makes the HUD widget appear on game maps.
/// </summary>
public class LB_World : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Header("GeoGun Map Overrides")]
    [Tooltip("If enabled, the player will have the geogun when entering this level")]
    public bool shouldHaveGeogun;
    [Tooltip("Allows rifts to be placed on walls")]
    public bool allowNonLinearSlicing = true;
    [Tooltip("Allows rifts to expand past the start position")]
    public bool allowExpandingRift;
    [Tooltip("Allows rifts collapsing into the negatives, mirroring null space")]
    public Item_Utility_Geogun.CollapseBehavior collapseBehavior;
    [Tooltip("Allows the player to slam rifts closed, creating a vacuum that flings things out of rifts")]
    public bool allowSlammingRift;
    [Tooltip("Debug parameter to... well, you get it (Allows markers to be placed on any material)")]
    public bool allowMarkerPlacementAnywhere;
    
    
    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/

    
    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private bool createdHUD;

    
    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    // TODO: This may be better changed from GameObject to reference a parent WB_HUD class
    [Tooltip("A reference to the HUD widget prefab to draw to the UI")]
    [SerializeField] private GameObject HUDWidgetPrefab;

    [SerializeField] private GameObject geogunPrefab;
    
    #endregion
    
    
    #region=======================================( Functions )=======================================================//
    
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        StartCoroutine(InitializeGeogunOverrides());
    }

    private void FixedUpdate()
    {
        if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
        else if (!createdHUD)
        {
            widgetManager.AddWidget(HUDWidgetPrefab);
            createdHUD = true;
        }
    }
    
    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private IEnumerator InitializeGeogunOverrides()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        // Give the player the geogun if they don't already have it
        if (shouldHaveGeogun)
        {
            var pawnManager = FindObjectOfType<GI_PawnManager>();
            var playerInventory = pawnManager.localPlayerCharacter.GetComponentInChildren<Pawn_Inventory>();
            if (playerInventory) if (!playerInventory.items.Contains(geogunPrefab)) playerInventory.AddItem(geogunPrefab);
        }
        
        var geogun = FindObjectOfType<Item_Utility_Geogun>();

        // Override geogun upgrades
        if (geogun)
        {
            geogun.allowNonLinearSlicing = allowNonLinearSlicing;
            geogun.allowExpandingRift = allowExpandingRift;
            geogun.collapseBehavior = collapseBehavior;
            geogun.allowSlammingRift = allowSlammingRift;
            geogun.allowMarkerPlacementAnywhere = allowMarkerPlacementAnywhere;
        }
    }
    
    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    
    
    #endregion
    
}