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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using RivenFramework;
using UnityEngine;

public class GI_NetworkManager : MonoBehaviour
{
    #region========================================( Variables )======================================================//

    /*-----[ Inspector Variables ]---------------------------------------------------------------------------------*/
    [Header("Network Object Syncing")]
    [Tooltip("Registry mapping prefab keys to prefabs for spawning over the network")]
    public NetPrefabRegistry prefabRegistry;
    [Tooltip("How many times per second variable changes are sent, 10 is default")]
    public float varSyncRate = 10f;

    [Header("Spectator")]
    [Tooltip("The prefab key registered in NetPrefabRegistry for the spectator body")]
    public string spectatorPrefabKey = "SpectatorBody";
    [Tooltip("World position the spectator body spawns at when entering a game world")]
    public Vector3 spectatorSpawnPosition = Vector3.up * 5f;


    /*-----[ External Variables ]---------------------------------------------------------------------------------*/
    public NetProfile localProfile = new NetProfile();
    public bool isConnected = false;
    public string connectedAddress = "";
    public string currentPhase = null;
    public string lastKnownMapName = "";
    public GameStatePacket lastGameState = null;
    public bool isOp = false;


    /*-----[ Internal Variables ]---------------------------------------------------------------------------------*/
    private string configFilePath => Path.Combine(Application.persistentDataPath, "serverlist.config");
    private string profileFilePath => Path.Combine(Application.persistentDataPath, "netprofile.profile");

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> inboundPackets
        = new System.Collections.Concurrent.ConcurrentQueue<string>();
    private Thread receiveThread;

    // UDP socket and server connection
    private UdpClient gameUdp;
    private IPEndPoint serverEndpoint;

    // Active coroutines, stored so they can be stopped on disconnect
    private Coroutine heartbeatCoroutine;
    private Coroutine listenCoroutine;
    private Coroutine varSyncCoroutine;

    // Connection state flags
    private bool receivedFirstState = false;
    private bool worldIsReady = false;
    private bool isLoadingWorld = false;

    // Network object tracking
    private readonly Dictionary<string, NetTransform> netTransforms
        = new Dictionary<string, NetTransform>();
    private readonly Dictionary<string, NetVarOwner> netVarOwners
        = new Dictionary<string, NetVarOwner>();
    private readonly Dictionary<string, Action<GameObject, string>> pendingSpawnCallbacks
        = new Dictionary<string, Action<GameObject, string>>();
    private readonly Dictionary<string, GameObject> netObjects
        = new Dictionary<string, GameObject>();
    private readonly List<SpawnBroadcastPacket> pendingSpawnQueue
        = new List<SpawnBroadcastPacket>();


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;

    [SerializeField] public List<ServerEntry> serverEntries = new List<ServerEntry>();

    // Events that other scripts can subscribe to
    public event Action<GameStatePacket> OnGameStateReceived;
    public event Action<string, string> OnChatReceived;
    public event Action<string> OnMapVoteReceived;
    public event Action<string> OnKicked;
    public event Action OnDisconnected;
    public event Action<bool> OnOpStatusChanged;
    public event Action<VoteKickPacket> OnVoteKickReceived;
    public event Action<VoteKickResultPacket> OnVoteKickResultReceived;

    // Widget prefabs assigned in the Inspector
    public GameObject fighterSelectWidget;
    public GameObject connectionMessageWidget;
    public GameObject netChatWidget;
    public GameObject netVoteKickWidget;

    [SerializeField] public string titleScreenWorldName = "_Title";
    public string LocalSpectatorNetworkId { get; private set; } = null;

    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]--------------------------------------------------------------------------------------*/

    public void Awake()
    {
        LogToFile($"[Awake] prefabRegistry={(prefabRegistry == null ? "NULL" : prefabRegistry.name)}");
        OnKicked += HandleKicked;
        OnDisconnected += HandleDisconnected;
    }

    public void Update()
    {
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();

        if (isConnected)
        {
            if (!widgetManager.GetExistingWidget(netChatWidget.gameObject.name))
                widgetManager.AddWidget(netChatWidget);

            if (!widgetManager.GetExistingWidget(netVoteKickWidget.gameObject.name))
                widgetManager.AddWidget(netVoteKickWidget);
        }
        else
        {
            var existingChat = widgetManager.GetExistingWidget(netChatWidget.gameObject.name);
            if (existingChat != null) Destroy(existingChat);

            var existingVoteKick = widgetManager.GetExistingWidget(netVoteKickWidget.gameObject.name);
            if (existingVoteKick != null) Destroy(existingVoteKick);
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }


    /*-----[ Internal Functions ]----------------------------------------------------------------------------------*/

    /// <summary>
    /// Saves the server browser list to a config file on disk.
    /// </summary>
    private void SaveServerConfigFile()
    {
        var wrapper = new ServerEntryListWrapper { servers = serverEntries };
        File.WriteAllText(configFilePath, JsonUtility.ToJson(wrapper, prettyPrint: true));
    }

    /// <summary>
    /// Shows a popup message to the player and then loads the title screen world.
    /// Used when the player is kicked or disconnected.
    /// </summary>
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

    /// <summary>
    /// Spawns the local player's networked spectator body and gives it local authority.
    /// Does nothing if one is already alive.
    /// </summary>
    private void SpawnSpectatorBody()
    {
        LogToFile($"[SpawnSpectatorBody] Called. LocalSpectatorNetworkId='{LocalSpectatorNetworkId}'");
        if (!string.IsNullOrEmpty(LocalSpectatorNetworkId)) return;

        NetSpawner.Spawn(spectatorPrefabKey, spectatorSpawnPosition, Quaternion.identity, (go, networkId) =>
        {
            LocalSpectatorNetworkId = networkId;

            var spectator = go.GetComponent<Pawn_Spectator>();
            if (spectator != null)
            {
                spectator.controlMode = ControlMode.LocalPlayer;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                LogToFile("[SpawnSpectatorBody] WARNING: Pawn_Spectator component not found on spawned object!");
            }

            LogToFile($"[SpawnSpectatorBody] Spectator body spawned. networkId={networkId} controlMode={spectator?._controlMode}");
        });
    }

    /// <summary>
    /// Despawns the local player's networked spectator body if one exists.
    /// Restores the cursor for UI use.
    /// </summary>
    public void DespawnSpectatorBody()
    {
        if (string.IsNullOrEmpty(LocalSpectatorNetworkId)) return;

        LogToFile($"[DespawnSpectatorBody] Despawning id='{LocalSpectatorNetworkId}'");
        NetSpawner.Despawn(LocalSpectatorNetworkId);
        LocalSpectatorNetworkId = null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Loads the named world and calls the callback once it is ready.
    /// Any spawn packets that arrived while the world was loading are flushed before the callback fires.
    /// </summary>
    private void LoadWorldAndThen(string mapName, Action onLoaded)
    {
        LogToFile($"[LoadWorldAndThen] Requested load of '{mapName}'. isLoadingWorld={isLoadingWorld}");
        if (isLoadingWorld) return;

        isLoadingWorld = true;
        worldIsReady = false;

        var worldLoader = GameInstance.Get<GI_WorldLoader>();
        if (worldLoader == null)
        {
            LogToFile("[LoadWorldAndThen] ERROR: GI_WorldLoader not found!");
            return;
        }

        void OnLoaded()
        {
            GI_WorldLoader.OnWorldLoaded -= OnLoaded;
            isLoadingWorld = false;
            worldIsReady = true;

            LogToFile($"[LoadWorldAndThen] '{mapName}' ready. Flushing {pendingSpawnQueue.Count} queued spawn(s).");
            foreach (var spawnBroadcastPacket in pendingSpawnQueue)
                HandleSpawnBroadcast(spawnBroadcastPacket);
            pendingSpawnQueue.Clear();

            onLoaded?.Invoke();
        }

        GI_WorldLoader.OnWorldLoaded += OnLoaded;
        worldLoader.LoadWorld(mapName);
    }

    /// <summary>
    /// Called when the server kicks this client. Shows a popup and returns to the title screen.
    /// </summary>
    private void HandleKicked(string reason)
    {
        LogToFile($"[HandleKicked] reason='{reason}'");
        DespawnSpectatorBody();
        ShowConnectionPopupAndReturnToTitle(
            "Kicked",
            string.IsNullOrEmpty(reason) ? "You were kicked from the server." : reason);
    }

    /// <summary>
    /// Called when the client disconnects from the server. Returns to the title screen.
    /// </summary>
    private void HandleDisconnected()
    {
        LogToFile("[HandleDisconnected] Returning to title.");
        ShowConnectionPopupAndReturnToTitle("Disconnected", "You have been disconnected from the server.");
    }

    /// <summary>
    /// Stops all network coroutines, closes the socket, and resets all connection-related state.
    /// Safe to call from any context.
    /// </summary>
    private void CleanupConnection()
    {
        LogToFile("[CleanupConnection] Cleaning up connection state.");

        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
        if (listenCoroutine != null) StopCoroutine(listenCoroutine);
        if (varSyncCoroutine != null) StopCoroutine(varSyncCoroutine);

        gameUdp?.Close();
        gameUdp = null;
        serverEndpoint = null;

        isConnected = false;
        connectedAddress = "";
        currentPhase = null;
        lastKnownMapName = "";
        LocalSpectatorNetworkId = null;
        worldIsReady = false;
        receivedFirstState = false;

        pendingSpawnCallbacks.Clear();
        netObjects.Clear();
        pendingSpawnQueue.Clear();

        // Drain any packets that arrived before the socket closed
        while (inboundPackets.TryDequeue(out _)) { }
    }


    /*-----[ Background Loops ]-----------------------------------------------------------------------------------*/

    /// <summary>
    /// Sends a heartbeat packet to the server every 5 seconds so it knows the client is still alive.
    /// </summary>
    private IEnumerator HeartbeatLoop()
    {
        byte[] heartbeat = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":HEARTBEAT");
        while (isConnected)
        {
            yield return new WaitForSecondsRealtime(5f);
            try
            {
                gameUdp?.Send(heartbeat, heartbeat.Length, serverEndpoint);
            }
            catch
            {
                LogToFile("[HeartbeatLoop] Send failed, stopping.");
                break;
            }
        }
    }

    /// <summary>
    /// Collects all dirty NetVar values and sends them to the server at the configured rate.
    /// Only sends values for objects this client has authority over.
    /// </summary>
    private IEnumerator VarSyncLoop()
    {
        float interval = 1f / Mathf.Max(1f, varSyncRate);
        while (isConnected)
        {
            yield return new WaitForSecondsRealtime(interval);

            foreach (var kvp in netVarOwners)
            {
                if (!netTransforms.TryGetValue(kvp.Key, out var netTransform) || !netTransform.hasAuthority)
                    continue;

                var dirtyVars = kvp.Value.FlushDirtyVars();
                if (dirtyVars.Count == 0) continue;

                var packet = new VariableSyncPacket { ObjectId = kvp.Key, Vars = dirtyVars };
                SendPacket(NetProtocol.Magic + ":VARSYNC:" + JsonUtility.ToJson(packet));
            }
        }
    }

    /// <summary>
    /// Runs on a dedicated background thread. Receives raw UDP packets from the server
    /// and pushes them into the inbound queue for the main thread to process.
    /// </summary>
    private void StartReceiveThread()
    {
        receiveThread = new Thread(() =>
        {
            LogToFile("[ReceiveThread] Started.");
            while (isConnected)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = gameUdp.Receive(ref remote);
                    string packet = Encoding.UTF8.GetString(data);
                    inboundPackets.Enqueue(packet);
                }
                catch
                {
                    // Socket was closed or an error occurred, exit the loop
                    break;
                }
            }
            LogToFile("[ReceiveThread] Exited.");
        })
        { IsBackground = true, Name = "FeKa-Receive" };

        receiveThread.Start();
    }

    /// <summary>
    /// Runs each frame on the main thread. Drains the inbound packet queue
    /// and routes each packet to ProcessPacket for handling.
    /// </summary>
    private IEnumerator PacketDrainLoop()
    {
        LogToFile("[PacketDrainLoop] Started.");
        while (isConnected)
        {
            while (inboundPackets.TryDequeue(out string packet))
                ProcessPacket(packet);
            yield return null;
        }
        LogToFile("[PacketDrainLoop] Exited.");
    }

    /// <summary>
    /// Reads an incoming packet string and routes it to the appropriate handler based on its command prefix.
    /// Called from PacketDrainLoop on the Unity main thread, so Unity API calls are safe here.
    /// </summary>
    private void ProcessPacket(string packet)
    {
        // --- Opped ---
        if (packet == NetProtocol.Magic + NetProtocol.Opped)
        {
            isOp = true;
            OnOpStatusChanged?.Invoke(true);
            return;
        }

        // --- Deopped ---
        if (packet == NetProtocol.Magic + NetProtocol.Deopped)
        {
            isOp = false;
            OnOpStatusChanged?.Invoke(false);
            return;
        }

        // --- Kicked ---
        if (packet.StartsWith(NetProtocol.Magic + ":KICKED:"))
        {
            string reason = packet.Substring((NetProtocol.Magic + ":KICKED:").Length);
            LogToFile($"[ProcessPacket] KICKED reason='{reason}'");
            CleanupConnection();
            OnKicked?.Invoke(reason);
            return;
        }

        // --- Game State ---
        if (packet.StartsWith(NetProtocol.Magic + ":STATE:"))
        {
            string json = packet.Substring((NetProtocol.Magic + ":STATE:").Length);
            var gameState = JsonUtility.FromJson<GameStatePacket>(json);
            LogToFile($"[ProcessPacket] STATE Phase={gameState.Phase} Map={gameState.MapName}");

            lastGameState = gameState;
            lastKnownMapName = gameState.MapName;

            string previousPhase = currentPhase;
            currentPhase = gameState.Phase;

            // First state received after connecting: load the server's current map
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
                // Game went in-progress but we missed the Loading phase
                else if (gameState.Phase == "InProgress" && previousPhase == "Intermission")
                {
                    LoadWorldAndThen(gameState.MapName, () => SpawnSpectatorBody());
                }
                // Game ended, return to intermission
                else if (gameState.Phase == "Intermission" && previousPhase == "InProgress")
                {
                    DespawnSpectatorBody();
                    StartCoroutine(ShowFighterSelect());
                }
            }

            OnGameStateReceived?.Invoke(gameState);
            return;
        }

        // --- Map Vote ---
        if (packet.StartsWith(NetProtocol.Magic + ":MAPVOTE:"))
        {
            OnMapVoteReceived?.Invoke(packet.Substring((NetProtocol.Magic + ":MAPVOTE:").Length));
            return;
        }

        // --- Chat ---
        if (packet.StartsWith(NetProtocol.Magic + NetProtocol.Chat))
        {
            string body = packet[(NetProtocol.Magic + NetProtocol.Chat).Length..];
            int separatorIndex = body.IndexOf(':');
            if (separatorIndex >= 0)
                OnChatReceived?.Invoke(body[..separatorIndex], body[(separatorIndex + 1)..]);
            return;
        }

        // --- Ping ---
        if (packet.StartsWith(NetProtocol.Magic + ":PING"))
        {
            byte[] pong = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":PONG");
            try { gameUdp?.Send(pong, pong.Length, serverEndpoint); } catch { }
            return;
        }

        // --- Transform Sync ---
        if (packet.StartsWith(NetProtocol.Magic + ":TSYNC:"))
        {
            var transformSyncPacket = JsonUtility.FromJson<TransformSyncPacket>(
                packet.Substring((NetProtocol.Magic + ":TSYNC:").Length));
            if (transformSyncPacket != null && netTransforms.TryGetValue(transformSyncPacket.objectUId, out var netTransform))
                netTransform.ReceiveTransformPacket(transformSyncPacket);
            return;
        }

        // --- Spawn ---
        if (packet.StartsWith(NetProtocol.Magic + ":SPAWN:"))
        {
            var spawnBroadcastPacket = JsonUtility.FromJson<SpawnBroadcastPacket>(
                packet.Substring((NetProtocol.Magic + ":SPAWN:").Length));
            if (spawnBroadcastPacket != null) HandleSpawnBroadcast(spawnBroadcastPacket);
            return;
        }

        // --- Despawn ---
        if (packet.StartsWith(NetProtocol.Magic + ":DESPAWN:"))
        {
            var despawnPacket = JsonUtility.FromJson<DespawnPacket>(
                packet.Substring((NetProtocol.Magic + ":DESPAWN:").Length));
            if (despawnPacket != null) HandleDespawnBroadcast(despawnPacket);
            return;
        }

        // --- Variable Sync ---
        if (packet.StartsWith(NetProtocol.Magic + ":VARSYNC:"))
        {
            var variableSyncPacket = JsonUtility.FromJson<VariableSyncPacket>(
                packet.Substring((NetProtocol.Magic + ":VARSYNC:").Length));
            if (variableSyncPacket != null && netVarOwners.TryGetValue(variableSyncPacket.ObjectId, out var varOwner))
            {
                foreach (var entry in variableSyncPacket.Vars)
                    varOwner.ApplyRemoteEntry(entry);
            }
            return;
        }

        // --- Vote Kick ---
        if (packet.StartsWith(NetProtocol.Magic + ":VOTEKICK:"))
        {
            var voteKickPacket = JsonUtility.FromJson<VoteKickPacket>(
                packet.Substring((NetProtocol.Magic + ":VOTEKICK:").Length));
            if (voteKickPacket != null) OnVoteKickReceived?.Invoke(voteKickPacket);
            return;
        }

        // --- Vote Kick Result ---
        if (packet.StartsWith(NetProtocol.Magic + ":VOTEKICKRESULT:"))
        {
            var voteKickResultPacket = JsonUtility.FromJson<VoteKickResultPacket>(
                packet.Substring((NetProtocol.Magic + ":VOTEKICKRESULT:").Length));
            if (voteKickResultPacket != null) OnVoteKickResultReceived?.Invoke(voteKickResultPacket);
            return;
        }
    }


    /*-----[ Spawn and Despawn Handlers ]--------------------------------------------------------------------------*/

    private void HandleSpawnBroadcast(SpawnBroadcastPacket spawnBroadcastPacket)
    {
        LogToFile($"[HandleSpawnBroadcast] key='{spawnBroadcastPacket.PrefabKey}' requestId={spawnBroadcastPacket.RequestId} worldReady={worldIsReady} pendingCallbacks=[{string.Join(", ", pendingSpawnCallbacks.Keys)}]");

        if (!worldIsReady)
        {
            LogToFile($"[HandleSpawnBroadcast] World not ready, queuing spawn. Queue size will be {pendingSpawnQueue.Count + 1}.");
            pendingSpawnQueue.Add(spawnBroadcastPacket);
            return;
        }

        if (prefabRegistry == null)
        {
            LogToFile("[HandleSpawnBroadcast] ERROR: prefabRegistry is NULL. Assign it in the Inspector.");
            return;
        }

        GameObject prefab = prefabRegistry.GetPrefab(spawnBroadcastPacket.PrefabKey);
        LogToFile($"[HandleSpawnBroadcast] GetPrefab('{spawnBroadcastPacket.PrefabKey}') = {(prefab == null ? "NULL" : prefab.name)}");
        if (prefab == null) return;

        Vector3 spawnPosition = new Vector3(spawnBroadcastPacket.PX, spawnBroadcastPacket.PY, spawnBroadcastPacket.PZ);
        Quaternion spawnRotation = Quaternion.Euler(spawnBroadcastPacket.RX, spawnBroadcastPacket.RY, spawnBroadcastPacket.RZ);
        GameObject spawnedObject = Instantiate(prefab, spawnPosition, spawnRotation);

        var netTransform = spawnedObject.GetComponent<NetTransform>();
        if (netTransform != null)
        {
            netTransform.networkObjectUId = spawnBroadcastPacket.NetworkObjectId;
            bool hasRequestId = !string.IsNullOrEmpty(spawnBroadcastPacket.RequestId);
            bool isInPendingCallbacks = pendingSpawnCallbacks.ContainsKey(spawnBroadcastPacket.RequestId ?? "");
            netTransform.hasAuthority = hasRequestId && isInPendingCallbacks;
            LogToFile($"[HandleSpawnBroadcast] NetTransform set. networkId={spawnBroadcastPacket.NetworkObjectId} hasAuthority={netTransform.hasAuthority} (hasRequestId={hasRequestId} isInPending={isInPendingCallbacks})");
        }
        else
        {
            LogToFile($"[HandleSpawnBroadcast] WARNING: No NetTransform found on spawned prefab '{spawnBroadcastPacket.PrefabKey}'.");
        }

        netObjects[spawnBroadcastPacket.NetworkObjectId] = spawnedObject;

        if (netTransform != null && netTransform.hasAuthority
            && pendingSpawnCallbacks.TryGetValue(spawnBroadcastPacket.RequestId, out var spawnCallback))
        {
            LogToFile($"[HandleSpawnBroadcast] Firing spawn callback for requestId={spawnBroadcastPacket.RequestId}.");
            pendingSpawnCallbacks.Remove(spawnBroadcastPacket.RequestId);
            spawnCallback?.Invoke(spawnedObject, spawnBroadcastPacket.NetworkObjectId);
        }
        else
        {
            LogToFile($"[HandleSpawnBroadcast] No callback fired (remote spawn or authority mismatch). netTransform={netTransform != null} hasAuthority={netTransform?.hasAuthority}");
        }
    }

    private void HandleDespawnBroadcast(DespawnPacket despawnPacket)
    {
        LogToFile($"[HandleDespawnBroadcast] networkId={despawnPacket.NetworkObjectId}");
        if (netObjects.TryGetValue(despawnPacket.NetworkObjectId, out var objectToDestroy))
        {
            if (objectToDestroy != null) Destroy(objectToDestroy);
            netObjects.Remove(despawnPacket.NetworkObjectId);
        }
        netTransforms.Remove(despawnPacket.NetworkObjectId);
        netVarOwners.Remove(despawnPacket.NetworkObjectId);
    }


    /*-----[ External Functions ]----------------------------------------------------------------------------------*/

    public void LoadServerConfigFile()
    {
        if (!File.Exists(configFilePath))
        {
            serverEntries = new List<ServerEntry>();
            SaveServerConfigFile();
            return;
        }
        var wrapper = JsonUtility.FromJson<ServerEntryListWrapper>(File.ReadAllText(configFilePath));
        serverEntries = wrapper?.servers ?? new List<ServerEntry>();
    }

    public void DeleteServerEntry(int index)
    {
        serverEntries.RemoveAt(index);
        SaveServerConfigFile();
    }

    public void EditServerEntry(int index, string newServerName, string newServerAddress)
    {
        if (index < 0 || index >= serverEntries.Count) return;
        serverEntries[index].serverName = newServerName;
        serverEntries[index].serverAddress = newServerAddress;
        SaveServerConfigFile();
    }

    public void AddServerEntry(string serverName, string serverAddress)
    {
        serverEntries.Add(new ServerEntry { serverName = serverName, serverAddress = serverAddress });
        SaveServerConfigFile();
    }

    /// <summary>
    /// Sends a UDP query to a server and waits for a response.
    /// Calls onSuccess with the server's info and measured ping, or onFailure if it times out.
    /// </summary>
    public IEnumerator QueryServer(string address, Action<ServerQueryResponse, int> onSuccess, Action onFailure)
    {
        string host = address;
        int port = NetProtocol.QueryPort;
        if (address.Contains(':'))
        {
            var addressParts = address.Split(':');
            host = addressParts[0];
            int.TryParse(addressParts[1], out port);
        }

        // Resolve the hostname on a background thread to avoid blocking the main thread
        UdpClient udpClient = null;
        IPEndPoint resolvedEndpoint = null;
        bool resolved = false;
        bool resolveFailed = false;

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try { resolvedEndpoint = new IPEndPoint(Dns.GetHostAddresses(host)[0], port); resolved = true; }
            catch { resolveFailed = true; }
        });

        float elapsed = 0f;
        while (!resolved && !resolveFailed)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > NetProtocol.TimeoutSeconds) { onFailure?.Invoke(); yield break; }
            yield return null;
        }
        if (resolveFailed) { onFailure?.Invoke(); yield break; }

        // Send the query packet
        try
        {
            udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = Mathf.RoundToInt(NetProtocol.TimeoutSeconds * 1000);
            byte[] queryBytes = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":QUERY");
            udpClient.Send(queryBytes, queryBytes.Length, resolvedEndpoint);
        }
        catch { udpClient?.Close(); onFailure?.Invoke(); yield break; }

        // Wait for the response on a background thread
        bool received = false;
        bool timedOut = false;
        string rawResponse = null;
        int measuredPing = 0;
        DateTime sendTime = DateTime.UtcNow;

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remote);
                measuredPing = (int)(DateTime.UtcNow - sendTime).TotalMilliseconds;
                rawResponse = Encoding.UTF8.GetString(data);
                received = true;
            }
            catch { timedOut = true; }
            finally { udpClient.Close(); }
        });

        float waitTime = 0f;
        while (!received && !timedOut)
        {
            waitTime += Time.unscaledDeltaTime;
            if (waitTime > NetProtocol.TimeoutSeconds + 0.5f) { timedOut = true; }
            yield return null;
        }
        if (timedOut || rawResponse == null) { onFailure?.Invoke(); yield break; }

        string responsePrefix = NetProtocol.Magic + ":RESPONSE:";
        if (!rawResponse.StartsWith(responsePrefix)) { onFailure?.Invoke(); yield break; }

        ServerQueryResponse queryResult;
        try { queryResult = JsonUtility.FromJson<ServerQueryResponse>(rawResponse.Substring(responsePrefix.Length)); }
        catch { onFailure?.Invoke(); yield break; }

        onSuccess?.Invoke(queryResult, measuredPing);
    }

    public void LoadNetProfile()
    {
        if (!File.Exists(profileFilePath))
        {
            localProfile = new NetProfile();
            SaveNetProfile();
            return;
        }
        localProfile = JsonUtility.FromJson<NetProfile>(File.ReadAllText(profileFilePath)) ?? new NetProfile();
    }

    public void SaveNetProfile()
    {
        File.WriteAllText(profileFilePath, JsonUtility.ToJson(localProfile, true));
    }

    /// <summary>
    /// Connects to a server at the given address. Resolves DNS, sends a JOIN packet,
    /// and waits for ACCEPTED or REJECTED before starting the background loops.
    /// Calls onSuccess on a successful connection, or onFailure with a reason string otherwise.
    /// </summary>
    public IEnumerator Connect(string address, Action onSuccess, Action<string> onFailure)
    {
        LogToFile($"[Connect] Attempting connection to '{address}'.");

        string host = address;
        int port = NetProtocol.QueryPort;
        if (address.Contains(':'))
        {
            var addressParts = address.Split(':');
            host = addressParts[0];
            int.TryParse(addressParts[1], out port);
        }

        // Resolve DNS on a background thread
        bool resolved = false;
        bool resolveFailed = false;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try { serverEndpoint = new IPEndPoint(Dns.GetHostAddresses(host)[0], port); resolved = true; }
            catch { resolveFailed = true; }
        });

        float elapsed = 0f;
        while (!resolved && !resolveFailed)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > NetProtocol.TimeoutSeconds) { onFailure?.Invoke("Could not resolve host."); yield break; }
            yield return null;
        }
        if (resolveFailed)
        {
            LogToFile("[Connect] DNS resolution failed.");
            onFailure?.Invoke("Could not resolve host.");
            yield break;
        }

        // Open the socket and send the JOIN packet, including our stored session token
        gameUdp = new UdpClient();
        byte[] joinBytes = Encoding.UTF8.GetBytes(
            NetProtocol.Magic + ":JOIN:" + localProfile.playerName + ":" + localProfile.sessionToken);

        try { gameUdp.Send(joinBytes, joinBytes.Length, serverEndpoint); }
        catch { gameUdp.Close(); onFailure?.Invoke("Could not reach server."); yield break; }

        // Wait for the ACCEPTED or REJECTED reply from the server
        bool gotReply = false;
        bool accepted = false;
        string rejectMessage = "";
        string receivedToken = "";

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                string reply = Encoding.UTF8.GetString(gameUdp.Receive(ref remote));
                string acceptedPrefix = NetProtocol.Magic + ":JOIN:ACCEPTED";

                if (reply.StartsWith(acceptedPrefix))
                {
                    accepted = true;
                    // The token follows the colon after ACCEPTED
                    if (reply.Length > acceptedPrefix.Length + 1)
                        receivedToken = reply[(acceptedPrefix.Length + 1)..];
                }
                else if (reply.StartsWith(NetProtocol.Magic + ":JOIN:REJECTED:"))
                {
                    rejectMessage = reply.Substring((NetProtocol.Magic + ":JOIN:REJECTED:").Length);
                }

                gotReply = true;
            }
            catch { gotReply = true; }
        });

        float waitTime = 0f;
        while (!gotReply)
        {
            waitTime += Time.unscaledDeltaTime;
            if (waitTime > NetProtocol.TimeoutSeconds) break;
            yield return null;
        }

        if (!accepted)
        {
            LogToFile($"[Connect] Connection refused. reason='{rejectMessage}'");
            gameUdp.Close();
            onFailure?.Invoke(string.IsNullOrEmpty(rejectMessage) ? "Connection timed out." : rejectMessage);
            yield break;
        }

        // Back on the main thread here, so Unity APIs are safe to call
        if (!string.IsNullOrEmpty(receivedToken))
        {
            localProfile.sessionToken = receivedToken;
            SaveNetProfile();
            LogToFile($"[Connect] Session token saved: {receivedToken}");
        }

        isConnected = true;
        connectedAddress = address;
        LogToFile($"[Connect] Connected to '{address}'.");

        StartReceiveThread();
        heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
        listenCoroutine = StartCoroutine(PacketDrainLoop());
        varSyncCoroutine = StartCoroutine(VarSyncLoop());

        onSuccess?.Invoke();
    }

    /// <summary>
    /// Sends a LEAVE packet to the server and tears down the connection.
    /// </summary>
    public void Disconnect()
    {
        if (!isConnected) return;
        LogToFile("[Disconnect] Disconnecting.");

        DespawnSpectatorBody();
        try
        {
            byte[] leavePacket = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":LEAVE");
            gameUdp?.Send(leavePacket, leavePacket.Length, serverEndpoint);
        }
        catch { }

        CleanupConnection();
        OnDisconnected?.Invoke();
    }

    /// <summary>
    /// Serializes the given NetTransform's current state and sends it to the server as a TSYNC packet.
    /// </summary>
    public void SendTransformSync(NetTransform netTransform)
    {
        if (!isConnected) return;

        var packet = new TransformSyncPacket
        {
            objectUId = netTransform.networkObjectUId,
            syncPosition = netTransform.syncPosition,
            syncRotation = netTransform.syncRotation,
            syncScale = netTransform.syncScale
        };

        if (netTransform.syncPosition)
        {
            packet.px = netTransform.transform.position.x;
            packet.py = netTransform.transform.position.y;
            packet.pz = netTransform.transform.position.z;
        }
        if (netTransform.syncRotation)
        {
            var eulerAngles = netTransform.transform.rotation.eulerAngles;
            packet.rx = eulerAngles.x;
            packet.ry = eulerAngles.y;
            packet.rz = eulerAngles.z;
        }
        if (netTransform.syncScale)
        {
            packet.sx = netTransform.transform.localScale.x;
            packet.sy = netTransform.transform.localScale.y;
            packet.sz = netTransform.transform.localScale.z;
        }

        SendPacket(NetProtocol.Magic + ":TSYNC:" + JsonUtility.ToJson(packet));
    }

    /// <summary>
    /// Asks the server to spawn a networked prefab at the given position and rotation.
    /// The onSpawned callback fires on this client after the server confirms the spawn,
    /// passing back the instantiated GameObject and its assigned network ID.
    /// Called by NetSpawner.
    /// </summary>
    public void RequestSpawn(string prefabKey, Vector3 position, Quaternion rotation, Action<GameObject, string> onSpawned)
    {
        string requestId = System.Guid.NewGuid().ToString();
        if (onSpawned != null) pendingSpawnCallbacks[requestId] = onSpawned;
        LogToFile($"[RequestSpawn] key='{prefabKey}' requestId={requestId} callbackStored={onSpawned != null}");

        var spawnRequest = new SpawnRequestPacket
        {
            PrefabKey = prefabKey,
            RequestId = requestId,
            PX = position.x,
            PY = position.y,
            PZ = position.z,
            RX = rotation.eulerAngles.x,
            RY = rotation.eulerAngles.y,
            RZ = rotation.eulerAngles.z
        };

        SendPacket(NetProtocol.Magic + ":SPAWNREQ:" + JsonUtility.ToJson(spawnRequest));
    }

    /// <summary>
    /// Asks the server to despawn the networked object with the given ID.
    /// Called by NetSpawner.
    /// </summary>
    public void RequestDespawn(string networkObjectId)
    {
        if (!isConnected) return;
        LogToFile($"[RequestDespawn] networkId={networkObjectId}");
        SendPacket(NetProtocol.Magic + ":DESPAWNREQ:" + JsonUtility.ToJson(new DespawnPacket { NetworkObjectId = networkObjectId }));
    }

    /// <summary>
    /// Tells the server this player is ready to start the game.
    /// </summary>
    public void SendReady()
    {
        if (!isConnected) return;
        LogToFile("[SendReady] Sending READY.");
        SendPacket(NetProtocol.Magic + NetProtocol.Ready);
    }

    /// <summary>
    /// Casts a vote for the given map name during intermission.
    /// </summary>
    public void SendVote(string mapName)
    {
        if (!isConnected) return;
        LogToFile($"[SendVote] Voting for '{mapName}'.");
        SendPacket(NetProtocol.Magic + NetProtocol.Vote + mapName);
    }

    /// <summary>
    /// Tells the server this player is switching to spectator mode.
    /// </summary>
    public void SendSpectate()
    {
        if (!isConnected) return;
        LogToFile("[SendSpectate] Sending SPECTATE.");
        SendPacket(NetProtocol.Magic + NetProtocol.Spectate);
    }

    /// <summary>
    /// Sends a chat message to the server. The server stamps the sender name
    /// and relays it to all connected clients.
    /// </summary>
    public void SendChat(string text)
    {
        if (!isConnected || string.IsNullOrWhiteSpace(text)) return;
        SendPacket(NetProtocol.Magic + NetProtocol.Chat + text);
    }

    /// <summary>
    /// Requests a vote kick against the player with the given name.
    /// The server will start a vote if one is not already in progress.
    /// </summary>
    public void RequestVoteKick(string targetName)
    {
        if (!isConnected) return;
        SendPacket(NetProtocol.Magic + ":VOTEKICKREQ:" + JsonUtility.ToJson(new VoteKickRequest { TargetName = targetName }));
    }

    /// <summary>
    /// Casts a yes or no vote in an active vote kick.
    /// </summary>
    public void CastVoteKick(bool votedYes)
    {
        if (!isConnected) return;
        SendPacket(NetProtocol.Magic + ":VOTEKICKCAST:" + JsonUtility.ToJson(new VoteKickCast { VotedYes = votedYes }));
    }

    /// <summary>
    /// Serializes and sends a raw string packet to the server over UDP.
    /// TODO I have temporarily made this method public since WB_NetPlayerlist needs access to it to initiate a vote-kick ~Liz
    /// </summary>
    public void SendPacket(string message)
    {
        if (gameUdp == null || serverEndpoint == null) return;
        byte[] data = Encoding.UTF8.GetBytes(message);
        try
        {
            gameUdp.Send(data, data.Length, serverEndpoint);
        }
        catch (Exception error)
        {
            LogToFile($"[SendPacket] ERROR: {error.Message}");
        }
    }

    /// <summary>
    /// Waits one frame for the world to finish initializing, then shows the fighter select widget.
    /// </summary>
    private IEnumerator ShowFighterSelect()
    {
        yield return null;
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && fighterSelectWidget != null)
            widgetManager.AddWidget(fighterSelectWidget);
        else
            LogToFile("[ShowFighterSelect] WARNING: widgetManager or fighterSelectWidget is null.");
    }


    /*-----[ Logging ]---------------------------------------------------------------------------------------------*/

    public static void LogToFile(string message)
    {
        // Uncomment to enable network logging to disk
        // string path = Path.Combine(Application.persistentDataPath, "netlog.txt");
        // File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
    }

    #endregion


    #region========================================( Registration )===================================================//

    // Called by NetTransform.Start() to register itself so transform sync packets can be routed to it.
    public void RegisterNetTransform(string objectId, NetTransform netTransform)
    {
        if (string.IsNullOrEmpty(objectId)) return;
        netTransforms[objectId] = netTransform;
        LogToFile($"[RegisterNetTransform] id={objectId}");
    }

    // Called by NetTransform.OnDestroy() to remove itself from the registry.
    public void UnregisterNetTransform(string objectId)
    {
        netTransforms.Remove(objectId);
        LogToFile($"[UnregisterNetTransform] id={objectId}");
    }

    // Called by NetVarOwner.Start() to register itself so variable sync packets can be routed to it.
    public void RegisterNetVarOwner(NetVarOwner owner)
    {
        string id = owner.NetworkObjectId;
        if (string.IsNullOrEmpty(id)) return;
        netVarOwners[id] = owner;
        LogToFile($"[RegisterNetVarOwner] id={id}");
    }

    // Called by NetVarOwner.OnDestroy() to remove itself from the registry.
    public void UnregisterNetVarOwner(NetVarOwner owner)
    {
        netVarOwners.Remove(owner.NetworkObjectId);
        LogToFile($"[UnregisterNetVarOwner] id={owner.NetworkObjectId}");
    }

    #endregion
}


// ============================================( Supporting Types )====================================================//

// A single entry in the server browser list
[Serializable]
public class ServerEntry
{
    public string serverName;
    public string serverAddress;
}

// JsonUtility does not support raw top-level lists, so the server list is wrapped in this class
[Serializable]
public class ServerEntryListWrapper
{
    public List<ServerEntry> servers;
}

// Protocol constants shared between GI_NetworkManager and any script that sends or receives packets.
// The magic string and port must match the dedicated server's configuration exactly.
public static class NetProtocol
{
    public const string Magic = "FeKa";
    public const int QueryPort = 27015;
    public const float TimeoutSeconds = 3f;

    // Outgoing (client -> server)
    public const string Ready = ":READY";
    public const string Vote = ":VOTE:";
    public const string Spectate = ":SPECTATE";
    public const string Pong = ":PONG";
    public const string Chat = ":CHAT:";
    public const string VoteKickRequest = ":VOTEKICKREQ:";
    public const string VoteKick = ":VOTEKICK:";
    public const string VoteKickCast = ":VOTEKICKCAST:";
    public const string VoteKickResult = ":VOTEKICKRESULT:";

    // Incoming (server -> client)
    public const string Opped = ":OPPED";
    public const string Deopped = ":DEOPPED";
    public const string State = ":STATE:";
    public const string MapVote = ":MAPVOTE:";
    public const string LoadMap = ":LOADMAP:";
    public const string Spawn = ":SPAWN:";
    public const string Ping = ":PING";
}

// The player's locally stored profile, saved to disk between sessions
[Serializable]
public class NetProfile
{
    public string playerName = "NetPlayer";
    public string sessionToken = "";
}

// Data returned by the server in response to a QUERY packet
[Serializable]
public class ServerQueryResponse
{
    public string ServerName;
    public string MapName;
    public string GameMode;
    public int MaxPlayers;
    public int PlayerCount;
    public string IconBase64;
}

// A compact player entry included in STATE packets
[Serializable]
public class PlayerNameEntry
{
    public string name;
    public int ping;
}

// The full game state snapshot sent by the server to all clients
[Serializable]
public class GameStatePacket
{
    public string Phase;
    public string MapName;
    public string GameMode;
    public int TimeLeft;
    public List<PlayerNameEntry> PlayerNames;
}

// Sent by a client to request a vote kick against another player
[Serializable]
public class VoteKickRequest
{
    public string TargetName;
}

// Broadcast by the server to all clients when a vote kick starts
[Serializable]
public class VoteKickPacket
{
    public string TargetName;
    public int TimeSeconds;
}

// Sent by a client to cast their vote in an active vote kick
[Serializable]
public class VoteKickCast
{
    public bool VotedYes;
}

// Broadcast by the server to all clients when a vote kick resolves
[Serializable]
public class VoteKickResultPacket
{
    public string TargetName;
    public bool Passed;
}