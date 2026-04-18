//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//====================================================================================================================//

using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

public class FeKa_GameRules : MonoBehaviour
{
    #region========================================( Variables )======================================================//

    /*-----[ Inspector Variables ]---------------------------------------------------------------------------------*/
    [Header("Spectator")]
    [Tooltip("The prefab key registered in NetPrefabRegistry for the spectator body")]
    public string spectatorPrefabKey = "SpectatorBody";
    [Tooltip("World position the spectator body spawns at when entering a game world")]
    public Vector3 spectatorSpawnPosition = Vector3.up * 5f;

    [Header("Scene Names")]
    [Tooltip("The scene to return to when kicked or disconnected")]
    public string titleScreenWorldName = "_Title";

    [Header("Widget Prefabs")]
    public GameObject fighterSelectWidget;
    public GameObject connectionMessageWidget;
    public GameObject netChatWidget;
    public GameObject netVoteKickWidget;
    public GameObject netKillFeedWidget;


    /*-----[ External Variables ]---------------------------------------------------------------------------------*/
    public string currentPhase = "";
    public string currentMapName = "";
    public FeKa_GameStatePacket lastGameState = null;
    public string pendingCharacterChoice = "";
    public event Action<FeKa_GameStatePacket> OnGameStateReceived;


    /*-----[ Internal Variables ]---------------------------------------------------------------------------------*/
    private bool receivedFirstState = false;
    private bool isLoadingWorld = false;

    public string LocalSpectatorNetworkId { get; private set; } = null;


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    private GI_NetworkManager networkManager;
    private GI_WidgetManager widgetManager;

    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]--------------------------------------------------------------------------------------*/

    private void Start()
    {
        networkManager = GameInstance.Get<GI_NetworkManager>();
        widgetManager = FindObjectOfType<GI_WidgetManager>();

        if (networkManager == null)
        {
            Debug.LogError("[FeKa_GameRules] GI_NetworkManager not found.");
            return;
        }

        networkManager.OnRawPacketReceived += OnRawPacketReceived;
        networkManager.OnKicked += HandleKicked;
        networkManager.OnDisconnected += HandleDisconnected;
    }

    private void OnDestroy()
    {
        if (networkManager == null) return;
        networkManager.OnRawPacketReceived -= OnRawPacketReceived;
        networkManager.OnKicked -= HandleKicked;
        networkManager.OnDisconnected -= HandleDisconnected;
    }

    private void Update()
    {
        widgetManager ??= FindObjectOfType<GI_WidgetManager>();

        if (networkManager == null) return;

        if (networkManager.isConnected)
        {
            if (widgetManager.GetExistingWidget(netChatWidget.name) == null)
                widgetManager.AddWidget(netChatWidget);

            if (widgetManager.GetExistingWidget(netVoteKickWidget.name) == null)
                widgetManager.AddWidget(netVoteKickWidget);

            if (widgetManager.GetExistingWidget(netKillFeedWidget.name) == null)
                widgetManager.AddWidget(netKillFeedWidget);
        }
        else
        {
            var existingChat = widgetManager.GetExistingWidget(netChatWidget.name);
            if (existingChat != null) Destroy(existingChat);

            var existingVoteKick = widgetManager.GetExistingWidget(netVoteKickWidget.name);
            if (existingVoteKick != null) Destroy(existingVoteKick);
            
            var existingKillFeed = widgetManager.GetExistingWidget(netKillFeedWidget.name);
            if (existingKillFeed != null) Destroy(existingKillFeed);
        }
    }


    /*-----[ Internal Functions ]----------------------------------------------------------------------------------*/

    private void OnRawPacketReceived(string packet)
    {
        string resultPrefix = networkManager.protocolMagic + ":RESULTS:";
        if (packet.StartsWith(resultPrefix))
        {
            var raceManager = GameInstance.Get<GI_RaceManager>();
            if (raceManager != null)
            {
                
                
                FeKaPawn_Base localRacer = raceManager.racers.Find(racer => racer.controlMode == ControlMode.LocalPlayer);
                if (localRacer != null && !raceManager.placedRacers.Contains(localRacer) && !raceManager.eliminatedRacers.Contains(localRacer))
                {
                    localRacer.FeKaCurrentStats.finishPlacement = raceManager.GetRacerPlacement(localRacer);
                    localRacer.FeKaCurrentStats.finishTime = raceManager.timeRemaining-raceManager.raceDuration;
                    raceManager.SendFinishAndShowResults(localRacer, true);
                }
                
                
                
                string json = packet.Substring(resultPrefix.Length);
                var resultsPacket = JsonUtility.FromJson<RaceResultsPacket>(json);
                raceManager.ShowResultsFromServer(resultsPacket);
            }
            return;
        }
        
        string statePrefix = networkManager.protocolMagic + ":STATE:";
        if (packet.StartsWith(statePrefix))
        {
            string json = packet.Substring(statePrefix.Length);
            var gameState = JsonUtility.FromJson<FeKa_GameStatePacket>(json);
            if (gameState == null) return;

            lastGameState = gameState;
            currentMapName = gameState.MapName;

            string previousPhase = currentPhase;
            currentPhase = gameState.Phase;

            if (!receivedFirstState)
            {
                receivedFirstState = true;

                if (gameState.Phase != "InProgress")
                    LoadWorldAndThen(gameState.MapName, () => StartCoroutine(ShowFighterSelect()));
                else
                    LoadWorldAndThen(gameState.MapName, () => SpawnSpectatorBody());
            }
            else
            {
                // Server started loading a new map
                if (gameState.Phase == "Loading" && previousPhase != "Loading")
                {
                    DespawnSpectatorBody();
                    LoadWorldAndThen(gameState.MapName, () => SpawnSpectatorBody());
                }
                // Loading finished and the game is now in progress
			    // the server has spawned the player's kart so the temporary spectator body can be despawned
                else if (gameState.Phase == "InProgress" && previousPhase == "Loading")
                {
        
                    if (!string.IsNullOrEmpty(pendingCharacterChoice) && pendingCharacterChoice != "spectate")
                    {
                        DespawnSpectatorBody();
                        NetSpawner.Spawn(pendingCharacterChoice, Vector3.zero, Quaternion.identity, (spawnedObject, networkId) =>
                        {
                            var kart = spawnedObject.GetComponent<FeKaPawn_Base>();
                            if (kart != null)
                            {
                                kart.Init();
                                kart.controlMode = ControlMode.LocalPlayer;
                                Cursor.lockState = CursorLockMode.Locked;
                                Cursor.visible = false;
                                var nm = GameInstance.Get<GI_NetworkManager>();
                                kart.networkPlayerName = nm?.localProfile.playerName ?? kart.displayName;
                            }
                        });
                    }
                    
                    pendingCharacterChoice = "";
                    GameInstance.Get<GI_RaceManager>().StartRace();
                }
                // Game went in-progress but we missed the Loading phase (joined late)
                else if (gameState.Phase == "InProgress" && previousPhase == "Intermission")
                {
                    LoadWorldAndThen(gameState.MapName, () => SpawnSpectatorBody());
                }
                // Race ended, return to intermission
                else if (gameState.Phase == "Intermission" && previousPhase == "InProgress")
                {
                    networkManager.DespawnAllNetworkObjects();
                    DespawnSpectatorBody();
                    StartCoroutine(ShowFighterSelect());
                }
            }

            OnGameStateReceived?.Invoke(gameState);
            return;
        }
    }

    private void HandleKicked(string reason)
    {
        GI_NetworkManager.LogToFile($"[FeKa_GameRules] Kicked: {reason}");
        DespawnSpectatorBody();
        receivedFirstState = false;
        isLoadingWorld = false;
        ShowConnectionPopupAndReturnToTitle("Kicked", string.IsNullOrEmpty(reason) ? "You were kicked from the server." : reason);
        
        networkManager.DespawnAllNetworkObjects(); // This is probably not needed, but I just wanna be sure everything gets cleaned up on the client
    }

    private void HandleDisconnected()
    {
        GI_NetworkManager.LogToFile("[FeKa_GameRules] Disconnected.");
        DespawnSpectatorBody();
        receivedFirstState = false;
        isLoadingWorld = false;
        ShowConnectionPopupAndReturnToTitle("Disconnected", "You have been disconnected from the server.");
        
        networkManager.DespawnAllNetworkObjects(); // This is probably not needed, but I just wanna be sure everything gets cleaned up on the client
    }

    private void ShowConnectionPopupAndReturnToTitle(string title, string message)
    {
        widgetManager ??= FindObjectOfType<GI_WidgetManager>();
        var worldLoader = GameInstance.Get<GI_WorldLoader>();
        if (worldLoader == null) return;

        void OnLoaded()
        {
            GI_WorldLoader.OnWorldLoaded -= OnLoaded;
            if (widgetManager == null || connectionMessageWidget == null) return;

            var existing = FindObjectOfType<WB_ConnectionMessage>();
            if (existing != null)
            {
                existing.Setup(title, message, "Close");
                return;
            }

            widgetManager.AddWidget(connectionMessageWidget);
            FindObjectOfType<WB_ConnectionMessage>()?.Setup(title, message, "Close");
        }

        GI_WorldLoader.OnWorldLoaded += OnLoaded;
        worldLoader.LoadWorld(titleScreenWorldName);
    }

    private void LoadWorldAndThen(string mapName, Action onLoaded)
    {
        GI_NetworkManager.LogToFile($"[FeKa_GameRules] Loading world '{mapName}'. isLoadingWorld={isLoadingWorld}");
        if (isLoadingWorld) return;

        isLoadingWorld = true;
        networkManager.NotifyWorldUnloading();

        var worldLoader = GameInstance.Get<GI_WorldLoader>();
        if (worldLoader == null)
        {
            GI_NetworkManager.LogToFile("[FeKa_GameRules] ERROR: GI_WorldLoader not found.");
            return;
        }

        void OnLoaded()
        {
            GI_WorldLoader.OnWorldLoaded -= OnLoaded;
            isLoadingWorld = false;
            networkManager.NotifyWorldReady();
            GI_NetworkManager.LogToFile($"[FeKa_GameRules] World '{mapName}' ready.");
            onLoaded?.Invoke();
        }

        GI_WorldLoader.OnWorldLoaded += OnLoaded;
        worldLoader.LoadWorld(mapName);
    }

    private void SpawnSpectatorBody()
    {
        GI_NetworkManager.LogToFile($"[FeKa_GameRules] SpawnSpectatorBody. existing id='{LocalSpectatorNetworkId}'");
        if (!string.IsNullOrEmpty(LocalSpectatorNetworkId)) return;

        NetSpawner.Spawn(spectatorPrefabKey, spectatorSpawnPosition, Quaternion.identity, (spawnedObject, networkId) =>
        {
            LocalSpectatorNetworkId = networkId;

            var spectator = spawnedObject.GetComponent<Pawn_Spectator>();
            if (spectator != null)
            {
                spectator.controlMode = ControlMode.LocalPlayer;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                GI_NetworkManager.LogToFile("[FeKa_GameRules] WARNING: Pawn_Spectator component not found on spawned object.");
            }

            GI_NetworkManager.LogToFile($"[FeKa_GameRules] Spectator spawned. networkId={networkId}");
        });
    }

    public void DespawnSpectatorBody()
    {
        if (string.IsNullOrEmpty(LocalSpectatorNetworkId)) return;

        GI_NetworkManager.LogToFile($"[FeKa_GameRules] Despawning spectator id='{LocalSpectatorNetworkId}'");
        NetSpawner.Despawn(LocalSpectatorNetworkId);
        LocalSpectatorNetworkId = null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator ShowFighterSelect()
    {
        yield return null;
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && fighterSelectWidget != null)
            widgetManager.AddWidget(fighterSelectWidget);
        else
            GI_NetworkManager.LogToFile("[FeKa_GameRules] WARNING: widgetManager or fighterSelectWidget is null.");
    }


    /*-----[ External Functions ]----------------------------------------------------------------------------------*/

    /// <summary>
    /// Sends the player's race result to the server
    /// </summary>
    public void SendFinishPacket(FeKa_FinishPacket finishPacket)
    {
        if (networkManager == null || !networkManager.isConnected) return;
        
        networkManager.SendPacket(networkManager.protocolMagic + ":FINISH:" + JsonUtility.ToJson(finishPacket));
    }

    #endregion
}

[Serializable]
public class RaceResultEntry
{
    public string PlayerName;
    public bool Failed;
    public int Placement;
    public float FinishTime;
    public float HealthRemaining;
    public int LivesRemaining;
    public int Kills;
    public float DamageTaken;
    public float DamageDealt;
    public float DamageHealed;
}

[Serializable]
public class RaceResultsPacket
{
    public List<RaceResultEntry> Results = new();
}