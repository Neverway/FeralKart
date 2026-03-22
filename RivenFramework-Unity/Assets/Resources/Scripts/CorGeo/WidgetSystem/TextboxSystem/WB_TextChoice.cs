using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using TMPro;
using UnityEngine;

[Todo_AddComments("Ported from AuHo, Needs tidying")]
public class WB_TextChoice : MonoBehaviour
{
    public WidgetNavigator optionNavigator;
    public Transform widgetContents;
    public WidgetSelectable_TMPText textChoiceTemplate;
    public TextMeshProUGUI dialogText;

    [Reload] private static int lastSelectedChoice;

    public void SetupChoices(string dialog, string[] choices)
    {
        dialogText.text = dialog;
        optionNavigator.selectableElements.Clear();
        for (int i = 0; i < choices.Length; i++)
        {
            var textChoice = Instantiate(textChoiceTemplate, optionNavigator.transform, false);
            textChoice.gameObject.SetActive(true);
            textChoice.SetText(choices[i]);
            textChoice.SetSelected(i == 0); //select first index
            int choiceToSelect = i;
            textChoice.OnInteracted.AddListener(() => lastSelectedChoice = choiceToSelect);
            optionNavigator.selectableElements.Add(textChoice);
        }
        widgetContents.gameObject.SetActive(true);
        optionNavigator.SetIsNavigating(true);
    }
    public static IEnumerator WaitForChoice(string dialog, params string[] choices) 
        => WaitForChoice(dialog, false, choices);
    
    public static IEnumerator WaitForChoice(string dialog, bool allowQuittingMenu, params string[] choices)
    {
        lastSelectedChoice = -1;
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        if (choices.IsEmptyOrNull())
        {
            Debug.LogWarning("Trying to display text choices with no choices? Not really able to display that, skipping");
            yield break;
        }
        if (!widgetManager.AddOrGetExistingWidget(out WB_TextChoice textChoiceManager))
        {
            Debug.LogError("Trying to display textchoice but was unable to add or get the widget");
            yield break;
        }
        //Freeze player
        // TODO player body doesn't exist on the GI in CorGeo
        //bool wasPlayerFrozen = GameInstance.Playerbody.freezeCharacterMovement;
        //GameInstance.Playerbody.freezeCharacterMovement = true;

        textChoiceManager.SetupChoices(dialog, choices); //Setup widget

        while (lastSelectedChoice < 0)
        {
            // TODO Inputs don't exist on the GI in CorGeo
            /*
            if (allowQuittingMenu)
            {
                if (GameInstance.Inputs.Action.WasPressedThisFrame() || GameInstance.Inputs.Select.WasPressedThisFrame())
                    break;
            }
            */
            yield return null; //Wait for selection
        }

        Destroy(textChoiceManager.gameObject); //Get rid of widget
        // TODO player body doesn't exist on the GI in CorGeo
        //GameInstance.Playerbody.freezeCharacterMovement = wasPlayerFrozen;
        yield return null;
    }

    public static int GetLastSelectedChoice() => lastSelectedChoice;
    
}
