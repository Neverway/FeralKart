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
using UnityEngine.UI;

public class WB_NetConnect : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    private GI_NetworkManager networkManager;
    public GameObject serverEntryListParent, serverEntryWidget;
    public List<WB_NetConnect_ServerEntry> serverEntryList;
    public int currentlySelectedServerEntry = -1; // Start with no server being selected
    
    [SerializeField] private Button buttonDeleteSelected, buttonEditSelected, buttonJoinSelected, buttonRefresh, buttonAddServer, buttonCancel;
    public GameObject confirmDeleteWidget, editServerWidget, addServerWidget, connectionMessageWidget;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        buttonDeleteSelected.onClick.AddListener(delegate { OnClick("buttonDeleteSelected"); });
        buttonEditSelected.onClick.AddListener(delegate { OnClick("buttonEditSelected"); });
        buttonJoinSelected.onClick.AddListener(delegate { OnClick("buttonJoinSelected"); });
        buttonRefresh.onClick.AddListener(delegate { OnClick("buttonRefresh"); });
        buttonAddServer.onClick.AddListener(delegate { OnClick("buttonAddServer"); });
        buttonCancel.onClick.AddListener(delegate { OnClick("buttonCancel"); });
        
        Init();
    }

    private void Update()
    {
        // Disable the server entry modification buttons if there isn't a selected server entry
        buttonDeleteSelected.interactable = (currentlySelectedServerEntry != -1);
        buttonEditSelected.interactable = (currentlySelectedServerEntry != -1);
        buttonJoinSelected.interactable = (currentlySelectedServerEntry != -1);
        
        // Update the visuals of which server is actually selected (I can't relly on unity's terrible built-in interactable navigation for this >:P)
        if (currentlySelectedServerEntry != -1)
        {
            for (int i = 0; i < serverEntryList.Count; i++)
            {
                serverEntryList[i].SetVisuallySelected(false);
            }
            serverEntryList[currentlySelectedServerEntry].SetVisuallySelected(true);
        }
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    
    private void OnClick(string button)
    {
        switch (button)
        {
            case "buttonDeleteSelected":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(confirmDeleteWidget);
                break;
            case "buttonEditSelected":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(editServerWidget);
                break;
            case "buttonJoinSelected":
                networkManager ??= GameInstance.Get<GI_NetworkManager>();
                widgetManager  ??= FindObjectOfType<GI_WidgetManager>();
                networkManager.Disconnect(); // For the love of cats, make sure to disconnect from any connected servers before connecting to a new one!!!!
                string addressToJoin = networkManager.serverEntries[currentlySelectedServerEntry].serverAddress;

                buttonJoinSelected.interactable = false;

                ShowConnectionMessage("Connecting", $"Connecting to {addressToJoin}...", "Cancel",
                    onButtonPressed: () =>
                    {
                        networkManager.Disconnect();
                        buttonJoinSelected.interactable = true;
                    });

                StartCoroutine(networkManager.Connect(addressToJoin,
                    onSuccess: () =>
                    {
                        buttonJoinSelected.interactable = true;
                        widgetManager.ToggleWidget("WB_ConnectionMessage");
                        widgetManager.ToggleWidget("WB_NetConnect");
                    },
                    onFailure: (reason) =>
                    {
                        buttonJoinSelected.interactable = true;
                        ShowConnectionMessage("Connection Failed", reason, "Close");
                    }));
                break;
            case "buttonRefresh":
                PingServerEntries();
                break;
            case "buttonAddServer":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(addServerWidget);
                break;
            case "buttonCancel":
                widgetManager ??= FindObjectOfType<GI_WidgetManager>();
                widgetManager.ToggleWidget("WB_NetConnect");
                break;
        }
    }
    
    
    /// <summary>
    /// Creates each server entry in the browser by checking the contents of a server list config file
    /// </summary>
    public void Init()
    {
        // Destroy all the server entries
        for (int i = 0; i < serverEntryListParent.transform.childCount; i++)
        {
            Destroy(serverEntryListParent.transform.GetChild(i).gameObject);
        }
        serverEntryList.Clear();
        currentlySelectedServerEntry = -1;
        
        // Check for the server list config file
            // Found it, load the data
            // Not found, created a new ones
        // (This is all done in networkManager.LoadServerConfigFile)
        networkManager ??= GameInstance.Get<GI_NetworkManager>();
        networkManager.LoadServerConfigFile();
            
        // List all the servers in the config by creating a new entry
        for (int i = 0; i < networkManager.serverEntries.Count; i++)
        {
            var newServerEntry = Instantiate(serverEntryWidget, serverEntryListParent.transform).GetComponent<WB_NetConnect_ServerEntry>();
            serverEntryList.Add(newServerEntry);
            newServerEntry.netConnect = this;
            newServerEntry.serverName.text = networkManager.serverEntries[i].serverName;
            newServerEntry.serverAddress = networkManager.serverEntries[i].serverAddress;
            newServerEntry.index = i;
            newServerEntry.AssignButtonFunction();
        }
        
        // Ping the servers to see if any of them are valid Feral Kart servers
        PingServerEntries();
    }

    /// <summary>
    /// Trys to talk to each server in the list to see if they are valid Feral Kart server
    /// If they are, we'll set the server entry's info (like the icon, map, game mode, ping, and current players)
    /// </summary>
    public void PingServerEntries()
    {
        networkManager ??= GameInstance.Get<GI_NetworkManager>();

        for (int i = 0; i < serverEntryList.Count; i++)
        {
            var entry = serverEntryList[i];
            entry.SetPending();

            StartCoroutine(networkManager.QueryServer(
                entry.serverAddress,
                onSuccess: (response, ping) =>
                {
                    entry.serverPing.text    = $"{ping}ms";
                    entry.serverMap.text     = response.MapName;
                    entry.serverMode.text    = response.GameMode;
                    entry.serverPlayers.text = $"{response.PlayerCount}/{response.MaxPlayers}";
                    entry.SetIcon(response.IconBase64);
                },
                onFailure: () =>
                {
                    entry.SetOffline();
                }));
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Called by WB_NetConnect_ConfirmDelete's prompt menu, deletes a server entry from the config list and refreshes the displayed servers
    /// </summary>
    public void DeleteSelectedServer()
    {
        networkManager ??= FindObjectOfType<GI_NetworkManager>();
        networkManager.DeleteServerEntry(currentlySelectedServerEntry);
        Init();
    }
    
    /// <summary>
    /// Opens (or updates) the connection message popup with the given text
    /// If the widget is already open it reuses it, otherwise it opens a fresh one
    /// </summary>
    private void ShowConnectionMessage(string title, string message, string btnText, Action onButtonPressed = null)
    {
        widgetManager ??= FindObjectOfType<GI_WidgetManager>();
 
        // Re-use an existing instance if it is already open, otherwise spawn a new one
        var existing = FindObjectOfType<WB_ConnectionMessage>();
        if (existing != null)
        {
            existing.Setup(title, message, btnText, onButtonPressed);
            return;
        }
 
        widgetManager.AddWidget(connectionMessageWidget);
 
        var popup = FindObjectOfType<WB_ConnectionMessage>();
        popup?.Setup(title, message, btnText, onButtonPressed);
    }


    #endregion
}
