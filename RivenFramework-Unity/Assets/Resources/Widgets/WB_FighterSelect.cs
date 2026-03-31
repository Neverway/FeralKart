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
    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private bool isReady = false;
    private List<GameObject> playerEntryObjects = new List<GameObject>();


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private GI_WidgetManager  widgetManager;
    private CharacterSelection characterSelection;
    public TMP_Text intermissionText, readyCountText, timerText;
    public Button readyButton, spectateButton;
    public WB_FighterSelect_FighterButton[] fighterButtons;
    public Transform playerListRoot;
    public GameObject playerEntry;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        networkManager     = GameInstance.Get<GI_NetworkManager>();
        widgetManager      ??= FindObjectOfType<GI_WidgetManager>();
        characterSelection ??= FindObjectOfType<CharacterSelection>();

        readyButton.onClick.AddListener(OnClickReady);
        spectateButton.onClick.AddListener(OnClickSpectate);

        networkManager.OnGameStateReceived += OnGameStateReceived;

        // Show spectate button only if game is already in progress
        bool midGame = networkManager.currentPhase == "InProgress";
        readyButton.gameObject.SetActive(!midGame);
        spectateButton.gameObject.SetActive(midGame);

        characterSelection.Initialize();
        SelectCharacter(fighterButtons[0].characterID);
        fighterButtons[0].SetVisuallySelected(true);
        
        
        // Destroy the pause menu or hud widgets if they are stuck open
        widgetManager.DestroyExistingWidget("WB_HUD");
        widgetManager.DestroyExistingWidget("WB_Title");
        widgetManager.DestroyExistingWidget("WB_Pause");
    }

    private void OnDestroy()
    {
        characterSelection.Close();
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
        widgetManager.ToggleWidget(gameObject.name);
    }

    private void OnGameStateReceived(GameStatePacket gs)
    {
        // Update phase label
        intermissionText.text = gs.Phase switch
        {
            "Intermission" => "Intermission",
            "Loading"      => "Loading map...",
            "InProgress"   => "Game In Progress",
            _              => gs.Phase
        };

        // Update timer
        timerText.text = gs.Phase == "Intermission" ? $"Starting in: {gs.TimeLeft}s" : "";

        // Rebuild player list
        foreach (var obj in playerEntryObjects) Destroy(obj);
        playerEntryObjects.Clear();

        foreach (var entry in gs.PlayerNames)
        {
            var entryObj  = Instantiate(playerEntry, playerListRoot);
            var entryComp = entryObj.GetComponent<WB_FighterSelect_PlayerEntry>();
            entryComp.playerNameText.text = entry.name;
            entryComp.kickButton.gameObject.SetActive(false);
            playerEntryObjects.Add(entryObj);
        }

        readyCountText.text = $"{gs.PlayerNames.Count} players in lobby";

        // Close this widget when the server moves to Loading.
        // GI_NetworkManager handles despawning any old spectator body and spawning the new one
        if (gs.Phase == "Loading")
        {
            widgetManager ??= GameInstance.Get<GI_WidgetManager>();
            widgetManager.ToggleWidget(gameObject.name);
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void SelectCharacter(string characterID)
    {
        characterSelection.ViewCharacter(characterID);
        foreach (var btn in fighterButtons)
            btn.SetVisuallySelected(false);
    }


    #endregion
}