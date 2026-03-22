//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections;
using RivenFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles, the showing and hiding, and the functionality of the geogun crosshair
/// </summary>
public class HUD_GeogunCrosshair : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("How many frames to wait between validation checks")]
    [SerializeField] private int validationCheckInterval = 20;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    public bool hasInitializedCrosshairSine;
    private bool lastIsValidState;
    private int lastMarkerCount = -1;
    private int frameCounter;

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_PawnManager pawnManager;
    private Item_Utility_Geogun geogun;
    private GameObject crosshairObject;
    [SerializeField] private Image AMarkerIndicator, BMarkerIndicator, PlacementIndicator, ASine, BSine;
    [SerializeField] private Color activeIndicator, inactiveIndicator;

    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        crosshairObject = transform.GetChild(0).gameObject;
        crosshairObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (geogun == null)
        {
            FindReferences();
            return;
        }
        
        SetMarkerIndicators();
        
        // Only check placement every few frames
        frameCounter++;
        if (frameCounter >= validationCheckInterval)
        {
            frameCounter = 0;
            SetPlacementIndicator();
        }
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void FindReferences()
    {
        // Exit if we have all reference
        if (pawnManager != null && geogun != null) return;
        
        // Get the pawn manager
        if (pawnManager == null)
        {
            pawnManager = GameInstance.Get<GI_PawnManager>();
            return;
        }
        
        // Get the geogun
        if (geogun == null)
        {
            var player = pawnManager.localPlayerCharacter;
            if (player != null)
            {
                geogun = player.GetComponentInChildren<Item_Utility_Geogun>();
                crosshairObject.SetActive(geogun != null); // Enable/Disable the crosshair if the gun was/wasn't found
            }
        }
    }

    private void SetMarkerIndicators()
    {
        int currentCount = geogun.spawnedProjectiles.Count;

        if (currentCount == lastMarkerCount) return;
        lastMarkerCount = currentCount;
        
        switch (currentCount)
        {
            // Both Markers (Or too many markers (Which should never happen... riiiight?))
            case >= 2:
                AMarkerIndicator.color = activeIndicator;
                BMarkerIndicator.color = activeIndicator;
                InitializeCrosshairSine();
                break;
            // One Marker
            case 1:
                AMarkerIndicator.color = activeIndicator;
                BMarkerIndicator.color = inactiveIndicator;
                hasInitializedCrosshairSine = false;
                ASine.fillAmount = 0;
                BSine.fillAmount = 0;
                break;
            // No Markers
            default:
                AMarkerIndicator.color = inactiveIndicator;
                BMarkerIndicator.color = inactiveIndicator;
                hasInitializedCrosshairSine = false;
                ASine.fillAmount = 0;
                BSine.fillAmount = 0;
                break;
        }
    }

    private void SetPlacementIndicator()
    {
        bool isValidTarget = geogun.GetIsValidTargetFromView();
        
        if (isValidTarget == lastIsValidState) return;
        lastIsValidState = isValidTarget;
        
        PlacementIndicator.color = isValidTarget ? activeIndicator : inactiveIndicator;
    }
    
    private void InitializeCrosshairSine()
    {
        if (hasInitializedCrosshairSine) return;
        StartCoroutine(LerpSineFill());
    }
    
    IEnumerator LerpSineFill()
    {
        float time = 0;
        float duration = 0.5f;

        while (time < duration)
        {
            var charge = Mathf.Lerp(0, 1, time / duration);
            time += Time.deltaTime;
            ASine.fillAmount = charge;
            BSine.fillAmount = charge;
            hasInitializedCrosshairSine = true;
            yield return null;
        }

        ASine.fillAmount = 1f;
        BSine.fillAmount = 1f;
    }
    

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}

// Find the local player
// Check their inventory for the gun
// ^ REPEAT UNTIL SUCCESS

// The gun was found
// Unhide the crosshair
// Highlight the center when pointed at valid target
// For each deployed marker, highlight the bars
// When both markers, lerp the sine