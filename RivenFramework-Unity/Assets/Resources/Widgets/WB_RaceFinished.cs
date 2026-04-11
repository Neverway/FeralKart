using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using TMPro;
using UnityEngine;

public class WB_RaceFinished : MonoBehaviour
{
    public FeKaPawn targetFeKaPawn;
    public bool findPossessedPawn;

    public float textDelay;
    
    public TMP_Text finishedText, placementText, timeText;
    
    void Start()
    {
        // Pawn reference check
        if (findPossessedPawn)
        {
            targetFeKaPawn ??= GetLocalPlayer();
        }
        finishedText.enabled = false;
        placementText.enabled = false;
        timeText.enabled = false;
        StartCoroutine(EnableTextBlock(0));
    }

    private IEnumerator EnableTextBlock(int textblockID)
    {
        yield return new WaitForSeconds(textDelay);
        switch (textblockID)
        {
            case 0:
                finishedText.enabled = true;
                StartCoroutine(EnableTextBlock(1));
                break;
            case 1:
                GetPlacement();
                placementText.enabled = true;
                StartCoroutine(EnableTextBlock(2));
                break;
            case 2:
                timeText.text = ($"{GameInstance.Get<GI_RaceManager>().timeRemaining:f2}");
                timeText.enabled = true;
                break;
        }
    }

    private void GetPlacement()
    {
        int placementNum = targetFeKaPawn.FeKaCurrentStats.finishPlacement;
        print(placementNum);
        if (placementNum == 1)
        {
            placementText.text = ($"1st");
            placementText.color = new Color(1f, 0.8f, 0.3f, 1);
        }
        else if (placementNum == 2)
        {
            placementText.text = ($"2nd");
            placementText.color = new Color(0.6f, 0.8f, 0.9f, 1);
        }

        else if (placementNum == 3)
        {
            placementText.text = ($"3rd");
            placementText.color = new Color(0.6f, 0.3f, 0.2f, 1);
        }
        else
        {
            placementText.text = ($"{placementNum}th");
            placementText.color = new Color(0.8f, 0.8f, 0.8f, 1);
        }
    }
    
    
    public FeKaPawn_Base GetLocalPlayer()
    {
        foreach (var fekaPawn in FindObjectsOfType<FeKaPawn_Base>())
        {
            if (fekaPawn.controlMode == ControlMode.LocalPlayer) return fekaPawn;
        }

        return null;
    }
}
