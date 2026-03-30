//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WB_FighterSelect : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private bool isReady = false;
    private List<GameObject> playerEntryObjects = new List<GameObject>();

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private GI_WidgetManager  widgetManager;
    public TMP_Text intermissionText, readyCountText, timerText;
    public Button readyButton, spectateButton;
    public Transform playerListRoot;
    public GameObject playerEntry;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        networkManager ??= GameInstance.Get<GI_NetworkManager>();
        widgetManager  ??= FindObjectOfType<GI_WidgetManager>();

        readyButton.onClick.AddListener(OnClickReady);
        spectateButton.onClick.AddListener(OnClickSpectate);

        // Subscribe to server state updates
        networkManager.OnGameStateReceived += OnGameStateReceived;

        // Show spectate button only if game is already in progress
        bool midGame = networkManager.currentPhase == "InProgress";
        readyButton.gameObject.SetActive(!midGame);
        spectateButton.gameObject.SetActive(midGame);
    }

    private void OnDestroy()
    {
        if (networkManager != null) networkManager.OnGameStateReceived -= OnGameStateReceived;
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void OnClickReady()
    {
        if (isReady) return;
        isReady = true;
        readyButton.interactable = false;
        networkManager.SendReady();
    }

    private void OnClickSpectate()
    {
        networkManager.SendSpectate();
        // TODO: enable freecam and hide this widget
        widgetManager.ToggleWidget(gameObject.name);
    }

    private void OnGameStateReceived(GameStatePacket gs)
    {
        // Update phase label
        switch (gs.Phase)
        {
            case "Intermission":
                intermissionText.text = "Intermission";
                break;
            case "Loading":
                intermissionText.text = "Loading map...";
                break;
            case "InProgress":
                intermissionText.text = "Game In Progress";
                break;
            default:
                intermissionText.text = gs.Phase;
                break;
        }

        // Update timer
        timerText.text = gs.Phase == "Intermission" ? $"Starting in: {gs.TimeLeft}s" : "";

        // Rebuild player list
        foreach (var obj in playerEntryObjects) Destroy(obj);
        playerEntryObjects.Clear();

        int readyCount = 0;
        foreach (var entry in gs.PlayerNames)
        {
            var entryObj  = Instantiate(playerEntry, playerListRoot);
            var entryComp = entryObj.GetComponent<WB_FighterSelect_PlayerEntry>();
            entryComp.playerNameText.text = entry.name;
            entryComp.kickButton.gameObject.SetActive(false);
            playerEntryObjects.Add(entryObj);
        }

        readyCountText.text = $"{gs.PlayerNames.Count} players in lobby";

        // If the server just moved to Loading, close this widget
        if (gs.Phase == "Loading")
        {
            widgetManager ??= GameInstance.Get<GI_WidgetManager>();
            widgetManager.ToggleWidget(gameObject.name);
        }
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    


    #endregion
}
