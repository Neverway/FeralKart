//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections.Generic;
using RivenFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class WB_FighterSelect : MonoBehaviour
{
    #region========================================( Variables )======================================================//

    /*-----[ Internal Variables ]---------------------------------------------------------------------------------*/
    private bool isReady = false;
    private string selectedCharacterId = "";
    private List<GameObject> playerEntryObjects = new List<GameObject>();


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private FeKa_GameRules fekaGameRules;
    private GI_WidgetManager widgetManager;
    private CharacterSelection characterSelection;
    public TMP_Text intermissionText, readyCountText, timerText;
    public Button readyButton, spectateButton;
    
    public WB_FighterSelect_FighterButton[] fighterButtons;
    public TMP_Text fighterNameText, fighterSubtitleText, finalStrikeNameText, finalStrikeDescriptionText, abilityNameText, abilityDescriptionText, passiveNameText, passiveDescriptionText;
    public Image fighterPortraitImage, finalStrikeImage, abilityImage;
    
    public Transform playerListRoot;
    public GameObject playerEntry;
    public List<string> widgetsToNotClear;


    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]--------------------------------------------------------------------------------------*/

    private void Start()
    {
        networkManager = GameInstance.Get<GI_NetworkManager>();
        fekaGameRules = FindObjectOfType<FeKa_GameRules>();
        widgetManager ??= FindObjectOfType<GI_WidgetManager>();
        characterSelection ??= FindObjectOfType<CharacterSelection>();
        fekaGameRules.pendingCharacterChoice="";

        readyButton.onClick.AddListener(OnClickReady);
        spectateButton.onClick.AddListener(OnClickSpectate);

        if (fekaGameRules != null) fekaGameRules.OnGameStateReceived += OnGameStateReceived;

        // Select the first fighter by default
        if (fighterButtons.Length > 0)
        {
            SelectCharacter(fighterButtons[0].characterID);
            fighterButtons[0].SetVisuallySelected(true);
        }

        characterSelection.Initialize();

        // Destroy any leftover in-game widgets that should not be visible in the lobby
        widgetManager.ClearAllWidgets(widgetsToNotClear);

        bool midGame = fekaGameRules != null && fekaGameRules.currentPhase == "InProgress";
        readyButton.gameObject.SetActive(!midGame);
        spectateButton.gameObject.SetActive(true);

        if (fekaGameRules != null && fekaGameRules.lastGameState != null)
            OnGameStateReceived(fekaGameRules.lastGameState);
    }

    private void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        characterSelection.Close();
        if (fekaGameRules != null)
            fekaGameRules.OnGameStateReceived -= OnGameStateReceived;
    }


    /*-----[ Internal Functions ]----------------------------------------------------------------------------------*/

    private void OnClickReady()
    {
        if (isReady) return;
        isReady = true;
        readyButton.interactable = false;
        fekaGameRules.pendingCharacterChoice = selectedCharacterId;
        networkManager.SendReady(selectedCharacterId);
    }

    private void OnClickSpectate()
    {
        networkManager.SendReady("spectate");
        widgetManager.ToggleWidget(gameObject.name);
    }

    private void OnGameStateReceived(FeKa_GameStatePacket gameState)
    {
        if (gameState.Phase == "Intermission")
            intermissionText.text = "Intermission";
        else if (gameState.Phase == "Loading")
            intermissionText.text = "Loading map...";
        else if (gameState.Phase == "InProgress")
            intermissionText.text = "Game In Progress";
        else
            intermissionText.text = gameState.Phase;

        timerText.text = gameState.Phase == "Intermission" ? $"Starting in: {gameState.TimeLeft}s" : "";

        bool midGame = gameState.Phase == "InProgress";
        readyButton.gameObject.SetActive(!midGame);
        spectateButton.gameObject.SetActive(true);

        foreach (var entryObject in playerEntryObjects)
            Destroy(entryObject);
        playerEntryObjects.Clear();

        if (gameState.PlayerNames != null)
        {
            foreach (var player in gameState.PlayerNames)
            {
                var entryObject = Instantiate(playerEntry, playerListRoot);
                var entryComponent = entryObject.GetComponent<WB_FighterSelect_PlayerEntry>();
                entryComponent.playerNameText.text = player.name;
                entryComponent.kickButton.gameObject.SetActive(false);
                playerEntryObjects.Add(entryObject);
            }
        }

        readyCountText.text = $"{gameState.PlayerNames?.Count ?? 0} players in lobby";

        if (gameState.Phase == "Loading")
        {
            widgetManager ??= GameInstance.Get<GI_WidgetManager>();
            widgetManager.ToggleWidget(gameObject.name);
        }
    }


    /*-----[ External Functions ]----------------------------------------------------------------------------------*/

    public void SelectCharacter(string characterID)
    {
        if (string.IsNullOrEmpty(characterID) || characterID == "Random")
        {
            var randomSelection = Random.Range(0, 2);
            fighterButtons[randomSelection].OnButtonClicked();
            return;
        }
        
        selectedCharacterId = characterID;
        
        // Enable the camera that is looking at the selected character
        characterSelection.ViewCharacter(characterID);
        
        // Set the fighter info in the sidebar according to what is set on the character selection
        var fighterData = characterSelection.GetFighterData(characterID);
        fighterPortraitImage.sprite = fighterData.fighterIcon;
        fighterNameText.text = fighterData.fighterName;
        fighterSubtitleText.text = fighterData.fighterSubtitle;
        finalStrikeImage.sprite = fighterData.finalStrike.details.icon;
        finalStrikeNameText.text = fighterData.finalStrike.details.itemName;
        finalStrikeDescriptionText.text = fighterData.finalStrike.details.itemDescription;
        abilityImage.sprite = fighterData.ability.details.icon;
        abilityNameText.text = fighterData.ability.details.itemName;
        abilityDescriptionText.text = fighterData.ability.details.itemDescription;
        passiveNameText.text = fighterData.passive.details.itemName;
        passiveDescriptionText.text = fighterData.passive.details.itemDescription;
        
        foreach (var button in fighterButtons) button.SetVisuallySelected(false);
    }


    #endregion
}