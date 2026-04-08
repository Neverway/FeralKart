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
    private List<PlayerNameEntry> lastKnownPlayers = null;


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private GI_WidgetManager widgetManager;
    public Transform playerListRoot;
    public GameObject playerEntry;


    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]--------------------------------------------------------------------------------------*/

    private void OnEnable()
    {
        networkManager ??= GameInstance.Get<GI_NetworkManager>();

        if (networkManager == null)
        {
            Debug.LogError("[Playerlist] networkManager is NULL");
            return;
        }

        networkManager.OnPlayerListReceived += OnPlayerListReceived;

        // If we already have a player list from before this widget was opened, populate immediately
        if (lastKnownPlayers != null) RebuildList(lastKnownPlayers);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.OnPlayerListReceived -= OnPlayerListReceived;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    /*-----[ Internal Functions ]----------------------------------------------------------------------------------*/

    private void OnPlayerListReceived(PlayerListPacket playerListPacket)
    {
        lastKnownPlayers = playerListPacket.PlayerNames;
        RebuildList(playerListPacket.PlayerNames);
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
                var networkManager = GameInstance.Get<GI_NetworkManager>();
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