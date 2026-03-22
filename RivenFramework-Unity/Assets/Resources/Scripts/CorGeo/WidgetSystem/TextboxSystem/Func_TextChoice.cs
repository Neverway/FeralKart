using System;
using System.Collections;
using System.Linq;
using RivenFramework;
using UnityEngine;
using UnityEngine.Events;

public class Func_TextChoice : MonoBehaviour
{
    [TextArea] public string dialogText;
    public bool allowQuittingMenu = false;
    public UnityEvent onQuitMenu = new UnityEvent();
    public TextChoiceOption[] choices;
    private Coroutine currentChoices;
    public void StartChoices()
    {
        StopChoices();
        currentChoices = GameInstance.SendCoroutine(CoWaitForChoice());
    }
    public void StopChoices()
    {
        if (currentChoices != null)
            StopCoroutine(currentChoices);
    }

    public IEnumerator CoWaitForChoice()
    {
        yield return WB_TextChoice.WaitForChoice(dialogText, allowQuittingMenu, choices.Select(choice => choice.choiceName).ToArray());
        int choiceIndex = WB_TextChoice.GetLastSelectedChoice();
        if (choiceIndex == -1)
        {
            if (allowQuittingMenu) onQuitMenu?.Invoke();
            yield break;
        }
        if (choices.IsIndexOutOfRange(choiceIndex))
        {
            Debug.LogError("Text Choice was out of range somehow? There must be a bug in the text choice code." +
                " Maybe multiple text choices running?");
            yield break;
        }
        choices[choiceIndex].OnChoiceSelected?.Invoke();
    }

    [Serializable]
    public struct TextChoiceOption
    {
        public string choiceName;
        public UnityEvent OnChoiceSelected;
    }
}
