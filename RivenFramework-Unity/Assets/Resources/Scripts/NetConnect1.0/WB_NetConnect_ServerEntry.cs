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
using UnityEngine.UI;

public class WB_NetConnect_ServerEntry : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Header("Selection Colorization")]
    public Color normalColor, selectedColor;
    public Image colorizedImage;
    
    [Header("Text References")]
    public Image serverIcon;
    public TMP_Text serverName;
    public TMP_Text serverMap;
    public TMP_Text serverMode;
    public TMP_Text serverPing;
    public TMP_Text serverPlayers;
    
    [Header("Entry Stuff")]
    public string serverAddress;
    public int index; // Used to tell the netConnect widget what server is selected when this button is clicked


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public WB_NetConnect netConnect;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Called by WB_NetConnect when this entry is created, ensures that the netconnect had time to set the index for this entry before the button registers it
    /// </summary>
    public void AssignButtonFunction()
    {
        GetComponent<Button>().onClick.AddListener(SetNetConnectsSelectedServer);
    }

    private void SetNetConnectsSelectedServer()
    {
        netConnect.currentlySelectedServerEntry = index;
    }

    public void SetVisuallySelected(bool isSelected)
    {
        colorizedImage.color = isSelected ? selectedColor : normalColor;
    }

    public void SetPending()
    {
        serverPing.text = "...";
        serverMap.text = "...";
        serverMode.text = "...";
        serverPlayers.text = "...";
    }

    public void SetOffline()
    {
        serverPing.text = "X";
        serverMap.text = "X";
        serverMode.text = "X";
        serverPlayers.text = "X";
    }
    
    public void SetIcon(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return;

        byte[]    iconBytes = Convert.FromBase64String(base64);
        Texture2D texture   = new Texture2D(2, 2);

        if (!texture.LoadImage(iconBytes))
        {
            return;
        }

        serverIcon.sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        serverIcon.preserveAspect = true;
        serverIcon.enabled = false;
        serverIcon.enabled = true; // toggle forces a redraw
    }


    #endregion
}
