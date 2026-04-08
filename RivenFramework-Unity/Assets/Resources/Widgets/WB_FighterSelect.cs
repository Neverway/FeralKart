//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections.Generic;
using RivenFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WB_FighterSelect : MonoBehaviour
{
    #region========================================( Variables )======================================================//

    /*-----[ Internal Variables ]---------------------------------------------------------------------------------*/
    private bool isReady = false;
    private string selectedCharacterId = "";
    private List<GameObject> playerEntryObjects = new List<GameObject>();


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private GI_WidgetManager widgetManager;
    private CharacterSelection characterSelection;
    public TMP_Text intermissionText, readyCountText, timerText;
    public Button readyButton, spectateButton;
    public WB_FighterSelect_FighterButton[] fighterButtons;
    public Transform playerListRoot;
    public GameObject playerEntry;


    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]--------------------------------------------------------------------------------------*/

    private void Start()
    {
        networkManager = GameInstance.Get<GI_NetworkManager>();
        widgetManager ??= FindObjectOfType<GI_WidgetManager>();
        characterSelection ??= FindObjectOfType<CharacterSelection>();

        readyButton.onClick.AddListener(OnClickReady);
        spectateButton.onClick.AddListener(OnClickSpectate);

        networkManager.OnRawPacketReceived += OnRawPacketReceived;

        // Select the first fighter by default
        if (fighterButtons.Length > 0)
        {
            SelectCharacter(fighterButtons[0].characterID);
            fighterButtons[0].SetVisuallySelected(true);
        }

        characterSelection.Initialize();

        // Destroy any leftover in-game widgets that should not be visible in the lobby
        widgetManager.DestroyExistingWidget("WB_HUD");
        widgetManager.DestroyExistingWidget("WB_Title");
        widgetManager.DestroyExistingWidget("WB_Pause");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        characterSelection.Close();
        if (networkManager != null)
            networkManager.OnRawPacketReceived -= OnRawPacketReceived;
    }


    /*-----[ Internal Functions ]----------------------------------------------------------------------------------*/

    private void OnClickReady()
    {
        if (isReady) return;
        isReady = true;
        readyButton.interactable = false;

        // Pass the selected character ID so the server knows what to spawn for this player
        networkManager.SendReady(selectedCharacterId);
    }

    private void OnClickSpectate()
    {
        // "spectate" is the reserved choice string the server uses to spawn a spectator body
        networkManager.SendReady("spectate");
        widgetManager.ToggleWidget(gameObject.name);
    }

    private void OnRawPacketReceived(string packet)
    {
        // Only handle Feral Kart STATE packets here
        // All other unrecognised packets are ignored by this widget
        string statePrefix = networkManager.protocolMagic + ":STATE:";
        if (!packet.StartsWith(statePrefix)) return;

        string json = packet.Substring(statePrefix.Length);
        var gameState = JsonUtility.FromJson<FeKa_GameStatePacket>(json);
        if (gameState == null) return;

        // Update the phase label
        if (gameState.Phase == "Intermission")
            intermissionText.text = "Intermission";
        else if (gameState.Phase == "Loading")
            intermissionText.text = "Loading map...";
        else if (gameState.Phase == "InProgress")
            intermissionText.text = "Game In Progress";
        else
            intermissionText.text = gameState.Phase;

        // Update the countdown timer
        timerText.text = gameState.Phase == "Intermission" ? $"Starting in: {gameState.TimeLeft}s" : "";

        // Show the ready button during intermission, spectate button if joining mid-match
        bool midGame = gameState.Phase == "InProgress";
        readyButton.gameObject.SetActive(!midGame);
        spectateButton.gameObject.SetActive(midGame);

        // Rebuild the lobby player list
        foreach (var entryObject in playerEntryObjects)
            Destroy(entryObject);
        playerEntryObjects.Clear();

        if (gameState.PlayerNames != null)
        {
            foreach (var playerEntry2 in gameState.PlayerNames)
            {
                var entryObject = Instantiate(playerEntry, playerListRoot);
                var entryComponent = entryObject.GetComponent<WB_FighterSelect_PlayerEntry>();
                entryComponent.playerNameText.text = playerEntry2.name;
                entryComponent.kickButton.gameObject.SetActive(false);
                playerEntryObjects.Add(entryObject);
            }
        }

        readyCountText.text = $"{gameState.PlayerNames?.Count ?? 0} players in lobby";

        // Close this widget once the server starts loading the map
        if (gameState.Phase == "Loading")
        {
            widgetManager ??= GameInstance.Get<GI_WidgetManager>();
            widgetManager.ToggleWidget(gameObject.name);
        }
    }


    /*-----[ External Functions ]----------------------------------------------------------------------------------*/

    public void SelectCharacter(string characterID)
    {
        selectedCharacterId = characterID;
        characterSelection.ViewCharacter(characterID);
        foreach (var button in fighterButtons)
            button.SetVisuallySelected(false);
    }


    #endregion
}