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
using TMPro;
using UnityEngine;

public class WB_NetChat : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Maximum number of messages kept in the chat history before old ones are removed")]
    public int maxMessages = 50;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private readonly List<string> messageHistory = new List<string>();
    public bool isTyping = false;

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public TMP_Text chatHistory;
    public TMP_InputField chatInput;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (!isTyping)
            {
                chatInput.gameObject.SetActive(true);
                chatInput.ActivateInputField();
                isTyping = true;
            }
            else if (isTyping)
            {
                chatInput.DeactivateInputField();
                chatInput.gameObject.SetActive(false);
                isTyping = false;
            }
        }
    }

    private void OnEnable()
    {
        NetChat.Subscribe(OnChatMessageReceived);
    }
 
    private void OnDisable()
    {
        NetChat.Unsubscribe(OnChatMessageReceived);
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void OnChatMessageReceived(string sender, string text)
    {
        AddMessage($"<b>{sender}:</b> {text}");
    }
 
    private void AddMessage(string formattedLine)
    {
        messageHistory.Add(formattedLine);
 
        // Drop the oldest message if we've exceeded the limit
        if (messageHistory.Count > maxMessages)
            messageHistory.RemoveAt(0);
 
        chatHistory.text = string.Join("\n", messageHistory);
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Called from the chat input field's OnSubmit event
    /// </summary>
    public void SubmitChat()
    {
        string text = chatInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
 
        NetChat.Send(text);
 
        chatInput.text = "";
 
        // Return focus to the input field so the player can keep typing
        //chatInput.ActivateInputField();
    }

    #endregion
}
