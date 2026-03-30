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
using UnityEngine;

public class WB_NetPlayerlist : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private List<GameObject> playerEntryObjects = new List<GameObject>();


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private GI_WidgetManager  widgetManager;
    public Transform playerListRoot;
    public GameObject playerEntry;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        networkManager ??= GameInstance.Get<GI_NetworkManager>();
        widgetManager  ??= GameInstance.Get<GI_WidgetManager>();

        networkManager.OnGameStateReceived += OnGameStateReceived;
        
        // Unlock the cursor while the playerlist is open
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void OnDestroy()
    {
        if (networkManager != null) networkManager.OnGameStateReceived -= OnGameStateReceived;

        // Re-lock the cursor when the playerlist closes
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void OnGameStateReceived(GameStatePacket gs)
    {
        print("Rebuilding player list");
        RebuildList(gs.PlayerNames);
    }

    private void RebuildList(List<PlayerNameEntry> players)
    {
        foreach (var obj in playerEntryObjects) Destroy(obj);
        playerEntryObjects.Clear();

        foreach (var p in players)
        {
            var entryObj  = Instantiate(playerEntry, playerListRoot);
            var entryComp = entryObj.GetComponent<WB_NetPlayerlist_PlayerEntry>();
            entryComp.playerNameText.text = p.name;
            entryComp.pingText.text       = p.ping > 0 ? $"{p.ping}ms" : "--";
            entryComp.kickButton.onClick.AddListener(() =>
            {
                // Kick is server-side only — for now just log; wire up a kick RPC later if needed
                Debug.Log($"Kick requested for {p.name}");
            });
            playerEntryObjects.Add(entryObj);
        }
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Call this to do a one-time populate without waiting for the next state packet
    /// </summary>
    public void PopulateFromCurrentState()
    {
        networkManager ??= GameInstance.Get<GI_NetworkManager>();
        // Pull the last known player names from the network manager if available
        // For now this is a no-op until we cache the last GameStatePacket
    }


    #endregion
}
