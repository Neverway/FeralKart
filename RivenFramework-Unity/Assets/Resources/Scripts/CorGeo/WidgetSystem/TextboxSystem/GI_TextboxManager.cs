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
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

// ReSharper disable once HollowTypeName
[Todo_AddComments("Ported from AuHo, Needs tidying")]
public class GI_TextboxManager : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public float normalTextTypeDelay, skippingTextTypeDelay;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [Box] public TextEvent currentTextEvent;
    public bool HasActiveTextEvent => textEventActive;

    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    public bool _textEventActive;

    public bool textEventActive;
    //{
//get { return _textEventActive; }
        //set { Debug.Log($"Set textEventActive to {value}"); _textEventActive=value; }
    //}
    public bool currentlyPrinting;
    public string currentTextContent;
    public float currentTextTypeDelay;
    public int currentFrame;
    public bool performingRegularMarkup, performingSpecialMarkup;
    public int markupStartIndex;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    private WB_Textbox textbox;
    [SerializeField] private AudioSource chatterAudioSource;
    [SerializeField][SerializeReference] private Char_ChatterVoice defaultVoice;
    [SerializeField] private AudioClip currentTextChatter;
    [SerializeField] private bool stopChatterClip;
    [Range(1,5)]
    [SerializeField] private int chatterFrequency;
    [Range(-3,3)]
    [SerializeField] private float chatterPitchMin;
    [Range(-3,3)]
    [SerializeField] private float chatterPitchMax;

    [SerializeField] private bool useConsistentChatterLanguage;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
    }


    public void Update()
    {
        if (textEventActive)
        {
            if (textbox == null)
            {
                Debug.LogWarning("A text event is active, but the textbox is null! This is an error I don't know how to fix, so I'm just going to force reset the textbox now. (Pls fix) ~Liz");
                textEventActive = false;
                return;
            }

            var currentEventFrame = currentTextEvent.frames[currentFrame];
            
            // Send current frame to textbox
            textbox.portrait.sprite = currentEventFrame.portrait;
            textbox.name.text = currentEventFrame.name;
            textbox.chat.text = currentTextContent;
            
            // Handel pressing the skip text button
            if (currentEventFrame.preventTextSkipping is false)
            {
                // TODO Inputs don't exist on the GI in CorGeo
                /*
                if (GameInstance.Inputs.Action.WasPressedThisFrame()) currentTextTypeDelay = skippingTextTypeDelay;
                if (GameInstance.Inputs.Action.WasReleasedThisFrame()) currentTextTypeDelay = normalTextTypeDelay;
                */
            }

            // Handel move next frame inputs
            if (currentEventFrame.preventTextContinuing is false)
            {
                // TODO Inputs don't exist on the GI in CorGeo
                /*
                if (GameInstance.Inputs.Interact.WasPressedThisFrame() && currentlyPrinting is false)
                {
                    PrintNextFrame();
                }
                */
            }
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void StartTextEvent()
    {
        // Open or get the textbox
        GetTextbox();
        
        // Display the first frame
        currentFrame = -1;
        PrintNextFrame();
        
        // Enable inputs to move next
        textEventActive = true;
    }

    /// <summary>
    /// Find or create the textbox widget
    /// </summary>
    private void GetTextbox()
    {
        if (!widgetManager) widgetManager = GameInstance.Get<GI_WidgetManager>();
        if (!textbox)
        {
            widgetManager.AddWidget("WB_Textbox");
            textbox = widgetManager.GetExistingWidget("WB_Textbox").GetComponent<WB_Textbox>();
        }
    }

    private void PrintNextFrame()
    {
        if (MoveNext())
        {
            var currentEventFrame = currentTextEvent.frames[currentFrame];
            StartCoroutine(TypeText(currentEventFrame.chatContent, currentEventFrame.OnFrameCompleted));
        }
    }

    private IEnumerator TypeText(string _fullTextContent, UnityEvent _onFrameCompleted)
    {
        // Set Displaymode
        textbox.displayMode = currentTextEvent.frames[currentFrame].displayMode;
        
        // Get voice override, if there is one
        var characterVoice = currentTextEvent.frames[currentFrame].chatterVoice;
        if (characterVoice)
        {
            chatterFrequency = characterVoice.chatterFrequency;
            currentTextChatter = characterVoice.textChatter;
            chatterPitchMin = characterVoice.chatterPitchMin;
            chatterPitchMax = characterVoice.chatterPitchMax;
        }
        else
        {
            chatterFrequency = defaultVoice.chatterFrequency;
            currentTextChatter = defaultVoice.textChatter;
            chatterPitchMin = defaultVoice.chatterPitchMin;
            chatterPitchMax = defaultVoice.chatterPitchMax;
        }
        
        currentTextContent = "";
        currentlyPrinting = true;
        for (int i = 0; i < _fullTextContent.Length; i++)
        {
            // Check for Special { } markups and skip if inside of one
            if (CheckForSpecialMarkups(_fullTextContent, i))
                continue;

            //Check for Regular < > markups and skip if inside of one
            if (CheckForRegularMarkups(_fullTextContent, i))
                continue;

            //If there are no markups, add current character to text content and wait for text delay
            PlayChatterSound(currentTextContent.Length, _fullTextContent[i]);
            currentTextContent += _fullTextContent[i];
            yield return new WaitForSeconds(currentTextTypeDelay);
        }
        currentlyPrinting = false;

        _onFrameCompleted.Invoke();
        
        if (currentTextEvent.frames[currentFrame].autoProgressOnComplete) PrintNextFrame();
    }

    /// <returns>True if currently inside a special markup</returns>
    private bool CheckForSpecialMarkups(string _fullTextContent, int _index)
    {
        //Don't check for special markups if you're checking for regular markups
        if (performingRegularMarkup) return false;

        //If not in regular markup, check if this is the start of one, and exit function
        if (!performingSpecialMarkup)
        {
            if (_fullTextContent[_index] == '{')
            {
                markupStartIndex = _index;
                performingSpecialMarkup = true;
                return true;
            }
            return false;
        }

        //If this is the end of the markup, finish the markup and process it
        if (_fullTextContent[_index] == '}')
        {
            string fullMarkup = _fullTextContent.Substring(markupStartIndex, _index - markupStartIndex + 1);

            //Remove certain characters from the markup to make processing it easier and flexible to use
            string[] charsToRemove = { "{", "}", " " };
            foreach (var charToRemove in charsToRemove) 
                fullMarkup = fullMarkup.Replace(charToRemove, "");

            //Get all special markup commands sorted by commas, and process each one
            string[] allCommands = fullMarkup.Split(',');
            foreach (string command in allCommands)
            {
                //Split command by an "=" where the left side is the command name, and the right is the command value
                string[] commandParts = command.Split('=');
                string commandName = commandParts[0];
                string commandValue = commandParts[1];

                switch (commandName)
                {
                    case "col":
                        switch (commandValue)
                        {
                            case "":
                                currentTextContent += "<color=#ffffff>";
                                break;
                            case "key":
                                currentTextContent += "<color=#ffe04d>";
                                break;
                            case "stat":
                                currentTextContent += "<color=#ffad2f>";
                                break;
                            case "err":
                                currentTextContent += "<color=#ff1111>";
                                break;
                        }
                        break;
                    case "spd":
                        switch (commandValue)
                        {
                            case "":
                                currentTextTypeDelay = normalTextTypeDelay;
                                break;
                            case "stat":
                                currentTextTypeDelay = 0.01f;
                                break;
                            default:
                                float.TryParse(commandParts[1], out currentTextTypeDelay);
                                break;
                        }
                        break;
                    case "por":

                        break;
                }
            }

            //Finish this markup
            performingSpecialMarkup = false;
        }

        return true;
    }
    
    /// <returns>True if currently inside a regular markup</returns>
    private bool CheckForRegularMarkups(string _fullTextContent, int _index)
    {
        //Don't check for regular markups if you're checking for special markups
        if (performingSpecialMarkup) return false;

        //If not in regular markup, check if this is the start of one, and exit function
        if (!performingRegularMarkup)
        {
            if (_fullTextContent[_index] == '<')
            {
                markupStartIndex = _index;
                performingRegularMarkup = true;
                return true;
            }
            return false;
        }

        //If this is the end of the markup, finish the markup and process it
        if (_fullTextContent[_index] == '>')
        {
            var fullMarkup = _fullTextContent.Substring(markupStartIndex, _index - markupStartIndex + 1);
            //Add full markup to current text content (essentially skips waiting for each character)
            currentTextContent += fullMarkup;

            //Finish this markup
            performingRegularMarkup = false;
        }

        return true;
    }

    public void PlayChatterSound(int _currentDisplayCharactersCount, char _currentTextIndex)
    {
        // Check if the character count is cleanly divisible by two
        // Apparently this is called a modulo expression? ~Liz
        if (_currentDisplayCharactersCount % chatterFrequency == 0)
        {
            if (stopChatterClip) chatterAudioSource.Stop();
            if (useConsistentChatterLanguage)
            {
                int hashCode = _currentTextIndex.GetHashCode();
                // Pitch
                int minPitchInt = (int)(chatterPitchMin * 100);
                int maxPitchInt = (int)(chatterPitchMax * 100);
                int pitchRangeInt = maxPitchInt - minPitchInt;
                if (pitchRangeInt != 0)
                {
                    int consistentPitchInt = (hashCode % pitchRangeInt) + minPitchInt;
                    float consistentPitch = consistentPitchInt / 100f;
                    chatterAudioSource.pitch = consistentPitch;
                }
                // If no range, skip selection
                else
                {
                    chatterAudioSource.pitch = minPitchInt;
                }
                
                chatterAudioSource.pitch = Random.Range(chatterPitchMin, chatterPitchMax);
            }
            else
            {
                // Pitch
                chatterAudioSource.pitch = Random.Range(chatterPitchMin, chatterPitchMax);
            }
            // Play
            chatterAudioSource.PlayOneShot(currentTextChatter);
        }
    }
    
    
    private bool MoveNext()
    {
        if (currentFrame < currentTextEvent.frames.Count-1)
        {
            currentFrame++;
            return true;
        }
        
        textEventActive = false;
        Destroy(textbox.gameObject);
        currentTextEvent.OnFinish?.Invoke();
        Clear();
        return false;
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public bool TryStartTextEvent(TextEvent _textEvent, bool _overrideExistingEvents = false)
    {
        if (textEventActive is false || _overrideExistingEvents)
        {   
            Clear();
            currentTextEvent = _textEvent;
            StartTextEvent();
            return true;
        }
        
        // Failed to start, an event was already running
        return false;
    }

    
    /// <summary>
    /// I'm trashily copying some of the functions from here for the cycle info widget
    /// This function is used by the cycle info widget so we can force the type text noise
    /// </summary>
    public void OverideSetChatterVoice(Char_ChatterVoice _voice)
    {
        chatterFrequency = _voice.chatterFrequency;
        currentTextChatter = _voice.textChatter;
        chatterPitchMin = _voice.chatterPitchMin;
        chatterPitchMax = _voice.chatterPitchMax;
    }

    public void Clear()
    {
        StopAllCoroutines();
        textEventActive = false;
        performingRegularMarkup = false;
        performingSpecialMarkup = false;
        currentTextContent = "";
        currentTextEvent = null;
        currentTextTypeDelay = normalTextTypeDelay;
    }

    #endregion
}
[Serializable]
public class TextEventItem
{

}

[Serializable]
public class TextFrames
{
    public TextFrames(string chatContent)
    {
        this.chatContent = chatContent;
    }

    public string name;
    [TextArea] public string chatContent;
    public Sprite portrait;
    public Char_ChatterVoice chatterVoice;
    public UnityEvent OnFrameCompleted = new UnityEvent();
    [Header("Frame Settings")] 
    public TextboxDisplayMode displayMode;
    public bool preventTextSkipping;
    public bool preventTextContinuing;
    public bool autoProgressOnComplete;
}




[Serializable]
public class TextEvent
{
    public List<TextFrames> frames = new();
    public UnityEvent OnFinish = new UnityEvent();

    public void ClearFrames() => frames.Clear();
    public void AddFrame(string text) => frames.Add(new TextFrames(text));
    public virtual bool TryDisplay(bool overrideExistingEvents = false) => GameInstance.Get<GI_TextboxManager>().TryStartTextEvent(this, overrideExistingEvents);

}


[Serializable]
public enum TextboxDisplayMode
{
    monologue,
    dialogueNoPortrait,
    dialogue,
    shopMono,
    shopDia,
    centered,
}
