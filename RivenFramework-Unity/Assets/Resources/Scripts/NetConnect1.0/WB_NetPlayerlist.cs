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
using UnityEngine;

public class WB_NetPlayerlist : MonoBehaviour
{
    #region========================================( Variables )======================================================//

    /*-----[ Internal Variables ]---------------------------------------------------------------------------------*/
    private List<GameObject> playerEntryObjects = new List<GameObject>();


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private FeKa_GameRules fekaGameRules;
    private GI_WidgetManager widgetManager;
    public Transform playerListRoot;
    public GameObject playerEntry;


    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]--------------------------------------------------------------------------------------*/

    private void OnEnable()
    {
        networkManager ??= GameInstance.Get<GI_NetworkManager>();
        fekaGameRules ??= FindObjectOfType<FeKa_GameRules>();

        if (fekaGameRules == null)
        {
            Debug.LogError("[Playerlist] FeKa_GameRules not found.");
            return;
        }

        fekaGameRules.OnGameStateReceived += OnGameStateReceived;

        if (fekaGameRules.lastGameState != null)
            RebuildList(fekaGameRules.lastGameState.PlayerNames);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
        if (fekaGameRules != null)
            fekaGameRules.OnGameStateReceived -= OnGameStateReceived;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    /*-----[ Internal Functions ]----------------------------------------------------------------------------------*/

    private void OnGameStateReceived(FeKa_GameStatePacket gameState)
    {
        RebuildList(gameState.PlayerNames);
    }

    private void RebuildList(List<PlayerNameEntry> players)
    {
        foreach (var entryObject in playerEntryObjects)
            Destroy(entryObject);
        playerEntryObjects.Clear();

        if (players == null) return;

        foreach (var player in players)
        {
            var entryObject = Instantiate(playerEntry, playerListRoot);
            var entryComponent = entryObject.GetComponent<WB_NetPlayerlist_PlayerEntry>();
            entryComponent.playerNameText.text = player.name;
            entryComponent.pingText.text = $"{player.ping}ms";

            entryComponent.kickButton.onClick.AddListener(() =>
            {
                if (networkManager == null) return;

                if (networkManager.isOp)
                    networkManager.SendChat("/kick " + player.name);
                else
                    networkManager.RequestVoteKick(player.name);
            });

            playerEntryObjects.Add(entryObject);
        }
    }


    /*-----[ External Functions ]----------------------------------------------------------------------------------*/

    #endregion
}