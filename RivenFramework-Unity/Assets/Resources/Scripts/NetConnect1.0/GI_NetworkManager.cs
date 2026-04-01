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
using RivenFramework;
using UnityEngine;

public class GI_NetworkManager : MonoBehaviour
{
    #region========================================( Variables )======================================================//

    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
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


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public NetProfile      localProfile     = new NetProfile();
    public bool            isConnected      = false;
    public string          connectedAddress = "";
    public string          currentPhase     = null;
    public string          lastKnownMapName = "";
    public GameStatePacket lastGameState    = null;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private string configFilePath  => Path.Combine(Application.persistentDataPath, "serverlist.config");
    private string profileFilePath => Path.Combine(Application.persistentDataPath, "netprofile.profile");

    // UDP
    private UdpClient  _gameUdp;
    private IPEndPoint _serverEndpoint;

    // Coroutines
    private Coroutine _heartbeatCoroutine;
    private Coroutine _listenCoroutine;
    private Coroutine _varSyncCoroutine;

    // Connection state flags
    private bool _receivedFirstState = false;
    private bool _worldIsReady       = false;
    private bool _isLoadingWorld     = false;

    // Network object tracking
    private readonly Dictionary<string, NetTransform>              netTransforms        = new Dictionary<string, NetTransform>();
    private readonly Dictionary<string, NetVarOwner>               netVarOwners         = new Dictionary<string, NetVarOwner>();
    private readonly Dictionary<string, Action<GameObject, string>> pendingSpawnCallbacks = new Dictionary<string, Action<GameObject, string>>();
    private readonly Dictionary<string, GameObject>                netObjects           = new Dictionary<string, GameObject>();
    private readonly List<SpawnBroadcastPacket>                    _pendingSpawnQueue   = new List<SpawnBroadcastPacket>();
    private static int nextObjectId = 1;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;

    [SerializeField] public List<ServerEntry> serverEntries = new List<ServerEntry>();

    // Events
    public event Action<GameStatePacket> OnGameStateReceived;
    public event Action<string, string>  OnChatReceived;
    public event Action<string>          OnMapVoteReceived;
    public event Action<string>          OnKicked;
    public event Action                  OnDisconnected;

    // Widget prefabs
    public GameObject fighterSelectWidget, connectionMessageWidget, netChatWidget;

    [SerializeField] public string titleScreenWorldName = "_Title";
    public string LocalSpectatorNetworkId { get; private set; } = null;

    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Awake()
    {
        LogToFile($"[Awake] prefabRegistry={(prefabRegistry == null ? "NULL" : prefabRegistry.name)}");
        OnKicked      += HandleKicked;
        OnDisconnected += HandleDisconnected;
    }

    public void Update()
    {
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();

        if (isConnected)
        {
            // Show chat widget while connected
            if (!widgetManager.GetExistingWidget(netChatWidget.gameObject.name))
                widgetManager.AddWidget(netChatWidget);
        }
        else
        {
            // Hide chat widget while disconnected
            var existing = widgetManager.GetExistingWidget(netChatWidget.gameObject.name);
            if (existing != null)
                Destroy(existing);
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/

    private void SaveServerConfigFile()
    {
        var wrapper = new ServerEntryListWrapper { servers = serverEntries };
        File.WriteAllText(configFilePath, JsonUtility.ToJson(wrapper, prettyPrint: true));
    }

    /// <summary>
    /// Opens the connection message popup with the given text, then returns the player to the title screen.
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
            if (existing != null) { existing.Setup(title, message, "Close"); return; }

            widgetManager.AddWidget(connectionMessageWidget);
            FindObjectOfType<WB_ConnectionMessage>()?.Setup(title, message, "Close");
        }

        GI_WorldLoader.OnWorldLoaded += OnLoaded;
        worldLoader.LoadWorld(titleScreenWorldName);
    }

    /// <summary>
    /// Spawns the local player's networked spectator body and gives it local authority.
    /// Skips silently if one is already alive.
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
                spectator.controlMode    = ControlMode.LocalPlayer;
                Cursor.lockState         = CursorLockMode.Locked;
                Cursor.visible           = false;
            }
            else
            {
                LogToFile($"[SpawnSpectatorBody] WARNING: Pawn_Spectator component not found on spawned object!");
            }

            LogToFile($"[SpawnSpectatorBody] Spectator body spawned (networkId={networkId}) controlMode={spectator?._controlMode}");
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
        Cursor.visible   = true;
    }

    /// <summary>
    /// Loads the named world, then invokes the callback once it is ready.
    /// Flushes any spawn packets that arrived while loading before calling the callback.
    /// </summary>
    private void LoadWorldAndThen(string mapName, Action onLoaded)
    {
        LogToFile($"[LoadWorldAndThen] Requested load of '{mapName}'. isLoadingWorld={_isLoadingWorld}");
        if (_isLoadingWorld) return;

        _isLoadingWorld = true;
        _worldIsReady   = false;

        var worldLoader = GameInstance.Get<GI_WorldLoader>();
        if (worldLoader == null)
        {
            LogToFile("[LoadWorldAndThen] ERROR: GI_WorldLoader not found!");
            return;
        }

        void OnLoaded()
        {
            GI_WorldLoader.OnWorldLoaded -= OnLoaded;
            _isLoadingWorld = false;
            _worldIsReady   = true;

            LogToFile($"[LoadWorldAndThen] '{mapName}' ready. Flushing {_pendingSpawnQueue.Count} queued spawn(s).");
            foreach (var sbp in _pendingSpawnQueue)
                HandleSpawnBroadcast(sbp);
            _pendingSpawnQueue.Clear();

            onLoaded?.Invoke();
        }

        GI_WorldLoader.OnWorldLoaded += OnLoaded;
        worldLoader.LoadWorld(mapName);
    }

    private void HandleKicked(string reason)
    {
        LogToFile($"[HandleKicked] reason='{reason}'");
        DespawnSpectatorBody();
        ShowConnectionPopupAndReturnToTitle("Kicked", string.IsNullOrEmpty(reason) ? "You were kicked from the server." : reason);
    }

    private void HandleDisconnected()
    {
        LogToFile("[HandleDisconnected] Returning to title.");
        ShowConnectionPopupAndReturnToTitle("Disconnected", "You have been disconnected from the server.");
    }

    /// <summary>
    /// Tears down all connection state. Safe to call from any context.
    /// </summary>
    private void CleanupConnection()
    {
        LogToFile("[CleanupConnection] Cleaning up connection state.");

        if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);
        if (_listenCoroutine    != null) StopCoroutine(_listenCoroutine);
        if (_varSyncCoroutine   != null) StopCoroutine(_varSyncCoroutine);

        _gameUdp?.Close();
        _gameUdp        = null;
        _serverEndpoint = null;

        isConnected          = false;
        connectedAddress     = "";
        currentPhase         = null;
        lastKnownMapName     = "";
        LocalSpectatorNetworkId = null;
        _worldIsReady        = false;
        _receivedFirstState  = false;

        pendingSpawnCallbacks.Clear();
        netObjects.Clear();
        _pendingSpawnQueue.Clear();
    }


    /*-----[ Background Loops ]-------------------------------------------------------------------------------------*/

    /// <summary>
    /// Sends a heartbeat to the server every 5 seconds so it knows we're still alive.
    /// </summary>
    private IEnumerator HeartbeatLoop()
    {
        byte[] heartbeat = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":HEARTBEAT");
        while (isConnected)
        {
            yield return new WaitForSecondsRealtime(5f);
            try { _gameUdp?.Send(heartbeat, heartbeat.Length, _serverEndpoint); }
            catch { LogToFile("[HeartbeatLoop] Send failed — stopping."); break; }
        }
    }

    /// <summary>
    /// Flushes dirty NetVar changes to the server at the configured rate.
    /// Only sends for objects this client owns.
    /// </summary>
    private IEnumerator VarSyncLoop()
    {
        float interval = 1f / Mathf.Max(1f, varSyncRate);
        while (isConnected)
        {
            yield return new WaitForSecondsRealtime(interval);

            foreach (var kv in netVarOwners)
            {
                if (!netTransforms.TryGetValue(kv.Key, out var nt) || !nt.hasAuthority) continue;

                var dirty = kv.Value.FlushDirtyVars();
                if (dirty.Count == 0) continue;

                var packet = new VariableSyncPacket { ObjectId = kv.Key, Vars = dirty };
                SendPacket(NetProtocol.Magic + ":VARSYNC:" + JsonUtility.ToJson(packet));
            }
        }
    }

    /// <summary>
    /// Listens for all incoming packets from the server while connected.
    /// </summary>
    private IEnumerator ListenLoop()
    {
        LogToFile("[ListenLoop] Started.");
        while (isConnected)
        {
            bool   hasPacket = false;
            string packet    = null;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var    remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data   = _gameUdp.Receive(ref remote);
                    packet    = Encoding.UTF8.GetString(data);
                    hasPacket = true;
                }
                catch { hasPacket = true; }
            });

            yield return new WaitUntil(() => hasPacket);
            if (packet == null) continue;

            // --- Kicked ---
            if (packet.StartsWith(NetProtocol.Magic + ":KICKED:"))
            {
                string reason = packet.Substring((NetProtocol.Magic + ":KICKED:").Length);
                LogToFile($"[ListenLoop] KICKED reason='{reason}'");
                CleanupConnection();
                OnKicked?.Invoke(reason);
                yield break;
            }

            // --- Game State ---
            else if (packet.StartsWith(NetProtocol.Magic + ":STATE:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":STATE:").Length);
                var    gs   = JsonUtility.FromJson<GameStatePacket>(json);
                LogToFile($"[ListenLoop] STATE Phase={gs.Phase} Map={gs.MapName} prev={currentPhase} firstState={_receivedFirstState}");

                lastGameState    = gs;
                lastKnownMapName = gs.MapName;

                string previousPhase = currentPhase;
                currentPhase = gs.Phase;

                // First state after connecting — load the server's current map
                if (!_receivedFirstState)
                {
                    _receivedFirstState = true;
                    LogToFile($"[ListenLoop] First state received. Phase={gs.Phase} Map={gs.MapName}");

                    if (gs.Phase != "InProgress")
                        LoadWorldAndThen(gs.MapName, () => StartCoroutine(ShowFighterSelect()));
                    else
                        LoadWorldAndThen(gs.MapName, () => SpawnSpectatorBody()); // Joined mid-game
                }
                else
                {
                    // Server entered Loading: load the new map then spawn spectator
                    if (gs.Phase == "Loading" && previousPhase != "Loading")
                    {
                        LogToFile("[ListenLoop] Phase -> Loading. Despawning spectator and loading new map.");
                        DespawnSpectatorBody();
                        LoadWorldAndThen(gs.MapName, () => SpawnSpectatorBody());
                    }
                    // Missed the Loading phase entirely — spawn spectator directly
                    else if (gs.Phase == "InProgress" && previousPhase == "Intermission")
                    {
                        LogToFile("[ListenLoop] Phase Intermission -> InProgress (missed Loading). Spawning spectator.");
                        LoadWorldAndThen(gs.MapName, () => SpawnSpectatorBody());
                    }
                    // Game ended — back to intermission
                    else if (gs.Phase == "Intermission" && previousPhase == "InProgress")
                    {
                        LogToFile("[ListenLoop] Phase InProgress -> Intermission. Despawning spectator, showing fighter select.");
                        DespawnSpectatorBody();
                        StartCoroutine(ShowFighterSelect());
                    }
                }

                OnGameStateReceived?.Invoke(gs);
            }

            // --- Map Vote ---
            else if (packet.StartsWith(NetProtocol.Magic + ":MAPVOTE:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":MAPVOTE:").Length);
                OnMapVoteReceived?.Invoke(json);
            }

            // --- Chat ---
            else if (packet.StartsWith(NetProtocol.Magic + NetProtocol.Chat))
            {
                string body = packet[(NetProtocol.Magic + NetProtocol.Chat).Length..];
                int    sep  = body.IndexOf(':');
                if (sep < 0) continue;
                OnChatReceived?.Invoke(body[..sep], body[(sep + 1)..]);
            }

            // --- Ping ---
            else if (packet.StartsWith(NetProtocol.Magic + ":PING"))
            {
                byte[] pong = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":PONG");
                try { _gameUdp?.Send(pong, pong.Length, _serverEndpoint); }
                catch { }
            }

            // --- Transform Sync ---
            else if (packet.StartsWith(NetProtocol.Magic + ":TSYNC:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":TSYNC:").Length);
                var    tsp  = JsonUtility.FromJson<TransformSyncPacket>(json);
                if (tsp != null && netTransforms.TryGetValue(tsp.objectUId, out var nt))
                    nt.ReceiveTransformPacket(tsp);
            }

            // --- Spawn Broadcast ---
            else if (packet.StartsWith(NetProtocol.Magic + ":SPAWN:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":SPAWN:").Length);
                var    sbp  = JsonUtility.FromJson<SpawnBroadcastPacket>(json);
                if (sbp != null) HandleSpawnBroadcast(sbp);
            }

            // --- Despawn Broadcast ---
            else if (packet.StartsWith(NetProtocol.Magic + ":DESPAWN:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":DESPAWN:").Length);
                var    dp   = JsonUtility.FromJson<DespawnPacket>(json);
                if (dp != null) HandleDespawnBroadcast(dp);
            }

            // --- Variable Sync ---
            else if (packet.StartsWith(NetProtocol.Magic + ":VARSYNC:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":VARSYNC:").Length);
                var    vsp  = JsonUtility.FromJson<VariableSyncPacket>(json);
                if (vsp != null && netVarOwners.TryGetValue(vsp.ObjectId, out var owner))
                    foreach (var entry in vsp.Vars)
                        owner.ApplyRemoteEntry(entry);
            }
        }
        LogToFile("[ListenLoop] Exited.");
    }


    /*-----[ Spawn / Despawn Handlers ]-------------------------------------------------------------------------------*/

    private void HandleSpawnBroadcast(SpawnBroadcastPacket sbp)
    {
        LogToFile($"[HandleSpawnBroadcast] key='{sbp.PrefabKey}' requestId={sbp.RequestId} worldReady={_worldIsReady} pendingCallbacks=[{string.Join(", ", pendingSpawnCallbacks.Keys)}]");

        if (!_worldIsReady)
        {
            LogToFile($"[HandleSpawnBroadcast] World not ready — queuing spawn. Queue size will be {_pendingSpawnQueue.Count + 1}.");
            _pendingSpawnQueue.Add(sbp);
            return;
        }

        if (prefabRegistry == null)
        {
            LogToFile("[HandleSpawnBroadcast] ERROR: prefabRegistry is NULL! Assign it in the Inspector.");
            return;
        }

        GameObject prefab = prefabRegistry.GetPrefab(sbp.PrefabKey);
        LogToFile($"[HandleSpawnBroadcast] GetPrefab('{sbp.PrefabKey}') = {(prefab == null ? "NULL" : prefab.name)}");
        if (prefab == null) return;

        Vector3    pos = new Vector3(sbp.PX, sbp.PY, sbp.PZ);
        Quaternion rot = Quaternion.Euler(sbp.RX, sbp.RY, sbp.RZ);
        GameObject go  = Instantiate(prefab, pos, rot);

        var nt = go.GetComponent<NetTransform>();
        if (nt != null)
        {
            nt.networkObjectUId = sbp.NetworkObjectId;
            bool hasRequestId   = !string.IsNullOrEmpty(sbp.RequestId);
            bool isInPending    = pendingSpawnCallbacks.ContainsKey(sbp.RequestId ?? "");
            nt.hasAuthority     = hasRequestId && isInPending;
            LogToFile($"[HandleSpawnBroadcast] NetTransform set. networkId={sbp.NetworkObjectId} hasAuthority={nt.hasAuthority} (hasRequestId={hasRequestId} isInPending={isInPending})");
        }
        else
        {
            LogToFile($"[HandleSpawnBroadcast] WARNING: No NetTransform found on spawned prefab '{sbp.PrefabKey}'.");
        }

        netObjects[sbp.NetworkObjectId] = go;

        if (nt != null && nt.hasAuthority && pendingSpawnCallbacks.TryGetValue(sbp.RequestId, out var cb))
        {
            LogToFile($"[HandleSpawnBroadcast] Firing spawn callback for requestId={sbp.RequestId}.");
            pendingSpawnCallbacks.Remove(sbp.RequestId);
            cb?.Invoke(go, sbp.NetworkObjectId);
        }
        else
        {
            LogToFile($"[HandleSpawnBroadcast] No callback fired (remote spawn or authority mismatch). nt={nt != null} hasAuthority={nt?.hasAuthority}");
        }
    }

    private void HandleDespawnBroadcast(DespawnPacket dp)
    {
        LogToFile($"[HandleDespawnBroadcast] networkId={dp.NetworkObjectId}");
        if (netObjects.TryGetValue(dp.NetworkObjectId, out var go))
        {
            if (go != null) Destroy(go);
            netObjects.Remove(dp.NetworkObjectId);
        }
        netTransforms.Remove(dp.NetworkObjectId);
        netVarOwners.Remove(dp.NetworkObjectId);
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/

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
        serverEntries[index].serverName    = newServerName;
        serverEntries[index].serverAddress = newServerAddress;
        SaveServerConfigFile();
    }

    public void AddServerEntry(string serverName, string serverAddress)
    {
        serverEntries.Add(new ServerEntry { serverName = serverName, serverAddress = serverAddress });
        SaveServerConfigFile();
    }

    /// <summary>
    /// Sends a UDP query packet to a server and waits for a response.
    /// Calls onSuccess with the server's response and measured ping, or onFailure if it times out.
    /// </summary>
    public IEnumerator QueryServer(string address, Action<ServerQueryResponse, int> onSuccess, Action onFailure)
    {
        string host = address;
        int    port = NetProtocol.QueryPort;
        if (address.Contains(':'))
        {
            var parts = address.Split(':');
            host = parts[0];
            int.TryParse(parts[1], out port);
        }

        // Resolve DNS on a background thread
        UdpClient  udp      = null;
        IPEndPoint endpoint = null;
        bool resolved = false, resolveFailed = false;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try   { endpoint = new IPEndPoint(Dns.GetHostAddresses(host)[0], port); resolved = true; }
            catch { resolveFailed = true; }
        });

        float t = 0f;
        while (!resolved && !resolveFailed)
        {
            t += Time.unscaledDeltaTime;
            if (t > NetProtocol.TimeoutSeconds) { onFailure?.Invoke(); yield break; }
            yield return null;
        }
        if (resolveFailed) { onFailure?.Invoke(); yield break; }

        // Send query packet
        try
        {
            udp = new UdpClient();
            udp.Client.ReceiveTimeout = Mathf.RoundToInt(NetProtocol.TimeoutSeconds * 1000);
            byte[] q = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":QUERY");
            udp.Send(q, q.Length, endpoint);
        }
        catch { udp?.Close(); onFailure?.Invoke(); yield break; }

        // Wait for response on background thread
        bool   received = false, timedOut = false;
        string rawResponse = null;
        int    measuredPing = 0;
        DateTime sendTime = DateTime.UtcNow;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var    remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data   = udp.Receive(ref remote);
                measuredPing  = (int)(DateTime.UtcNow - sendTime).TotalMilliseconds;
                rawResponse   = Encoding.UTF8.GetString(data);
                received      = true;
            }
            catch { timedOut = true; }
            finally { udp.Close(); }
        });

        float wait = 0f;
        while (!received && !timedOut)
        {
            wait += Time.unscaledDeltaTime;
            if (wait > NetProtocol.TimeoutSeconds + 0.5f) { timedOut = true; }
            yield return null;
        }
        if (timedOut || rawResponse == null) { onFailure?.Invoke(); yield break; }

        string prefix = NetProtocol.Magic + ":RESPONSE:";
        if (!rawResponse.StartsWith(prefix)) { onFailure?.Invoke(); yield break; }

        ServerQueryResponse result;
        try   { result = JsonUtility.FromJson<ServerQueryResponse>(rawResponse.Substring(prefix.Length)); }
        catch { onFailure?.Invoke(); yield break; }

        onSuccess?.Invoke(result, measuredPing);
    }

    public void LoadNetProfile()
    {
        if (!File.Exists(profileFilePath)) { localProfile = new NetProfile(); SaveNetProfile(); return; }
        localProfile = JsonUtility.FromJson<NetProfile>(File.ReadAllText(profileFilePath)) ?? new NetProfile();
    }

    public void SaveNetProfile()
    {
        File.WriteAllText(profileFilePath, JsonUtility.ToJson(localProfile, true));
    }

    /// <summary>
    /// Connects to a server at the given address. Resolves DNS, sends a JOIN packet,
    /// and waits for ACCEPTED or REJECTED before starting the background loops.
    /// </summary>
    public IEnumerator Connect(string address, Action onSuccess, Action<string> onFailure)
    {
        LogToFile($"[Connect] Attempting connection to '{address}'.");

        string host = address;
        int    port = NetProtocol.QueryPort;
        if (address.Contains(':'))
        {
            var parts = address.Split(':');
            host = parts[0];
            int.TryParse(parts[1], out port);
        }

        // Resolve DNS
        bool resolved = false, resolveFailed = false;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try   { _serverEndpoint = new IPEndPoint(Dns.GetHostAddresses(host)[0], port); resolved = true; }
            catch { resolveFailed = true; }
        });

        float t = 0f;
        while (!resolved && !resolveFailed)
        {
            t += Time.unscaledDeltaTime;
            if (t > NetProtocol.TimeoutSeconds) { onFailure?.Invoke("Could not resolve host."); yield break; }
            yield return null;
        }
        if (resolveFailed) { LogToFile("[Connect] DNS resolution failed."); onFailure?.Invoke("Could not resolve host."); yield break; }

        // Open socket and send JOIN
        _gameUdp = new UdpClient();
        byte[] joinBytes = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":JOIN:" + localProfile.playerName);
        try   { _gameUdp.Send(joinBytes, joinBytes.Length, _serverEndpoint); }
        catch { _gameUdp.Close(); onFailure?.Invoke("Could not reach server."); yield break; }

        // Wait for ACCEPTED / REJECTED
        bool   gotReply = false, accepted = false;
        string rejectMsg = "";
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var    remote = new IPEndPoint(IPAddress.Any, 0);
                string reply  = Encoding.UTF8.GetString(_gameUdp.Receive(ref remote));
                if      (reply == NetProtocol.Magic + ":JOIN:ACCEPTED")       accepted  = true;
                else if (reply.StartsWith(NetProtocol.Magic + ":JOIN:REJECTED:")) rejectMsg = reply.Substring((NetProtocol.Magic + ":JOIN:REJECTED:").Length);
                gotReply = true;
            }
            catch { gotReply = true; }
        });

        float wait = 0f;
        while (!gotReply) { wait += Time.unscaledDeltaTime; if (wait > NetProtocol.TimeoutSeconds) break; yield return null; }

        if (!accepted)
        {
            LogToFile($"[Connect] Connection refused. reason='{rejectMsg}'");
            _gameUdp.Close();
            onFailure?.Invoke(string.IsNullOrEmpty(rejectMsg) ? "Connection timed out." : rejectMsg);
            yield break;
        }

        isConnected      = true;
        connectedAddress = address;
        LogToFile($"[Connect] Connected to '{address}'.");

        _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
        _listenCoroutine    = StartCoroutine(ListenLoop());
        _varSyncCoroutine   = StartCoroutine(VarSyncLoop());

        onSuccess?.Invoke();
    }

    public void Disconnect()
    {
        if (!isConnected) return;
        LogToFile("[Disconnect] Disconnecting.");

        DespawnSpectatorBody();
        try
        {
            byte[] leave = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":LEAVE");
            _gameUdp?.Send(leave, leave.Length, _serverEndpoint);
        }
        catch { }

        CleanupConnection();
        OnDisconnected?.Invoke();
    }

    /// <summary>Serialize and send a TSYNC packet to the server.</summary>
    public void SendTransformSync(NetTransform nt)
    {
        if (!isConnected) return;

        var packet = new TransformSyncPacket
        {
            objectUId    = nt.networkObjectUId,
            syncPosition = nt.syncPosition,
            syncRotation = nt.syncRotation,
            syncScale    = nt.syncScale,
        };

        if (nt.syncPosition) { packet.px = nt.transform.position.x;        packet.py = nt.transform.position.y;        packet.pz = nt.transform.position.z; }
        if (nt.syncRotation) { var e = nt.transform.rotation.eulerAngles;   packet.rx = e.x; packet.ry = e.y; packet.rz = e.z; }
        if (nt.syncScale)    { packet.sx = nt.transform.localScale.x;       packet.sy = nt.transform.localScale.y;      packet.sz = nt.transform.localScale.z; }

        SendPacket(NetProtocol.Magic + ":TSYNC:" + JsonUtility.ToJson(packet));
    }

    /// <summary>
    /// Requests the server to spawn a networked prefab. Called by NetSpawner.
    /// onSpawned fires on this client once the server confirms the spawn.
    /// </summary>
    public void RequestSpawn(string prefabKey, Vector3 position, Quaternion rotation, Action<GameObject, string> onSpawned)
    {
        string requestId = System.Guid.NewGuid().ToString();
        if (onSpawned != null) pendingSpawnCallbacks[requestId] = onSpawned;
        LogToFile($"[RequestSpawn] key='{prefabKey}' requestId={requestId} callbackStored={onSpawned != null}");

        var req = new SpawnRequestPacket
        {
            PrefabKey = prefabKey, RequestId = requestId,
            PX = position.x, PY = position.y, PZ = position.z,
            RX = rotation.eulerAngles.x, RY = rotation.eulerAngles.y, RZ = rotation.eulerAngles.z,
        };
        SendPacket(NetProtocol.Magic + ":SPAWNREQ:" + JsonUtility.ToJson(req));
    }

    /// <summary>Requests the server to despawn a networked object. Called by NetSpawner.</summary>
    public void RequestDespawn(string networkObjectId)
    {
        if (!isConnected) return;
        LogToFile($"[RequestDespawn] networkId={networkObjectId}");
        SendPacket(NetProtocol.Magic + ":DESPAWNREQ:" + JsonUtility.ToJson(new DespawnPacket { NetworkObjectId = networkObjectId }));
    }

    public void SendReady()
    {
        if (!isConnected) return;
        LogToFile("[SendReady] Sending READY.");
        SendPacket(NetProtocol.Magic + NetProtocol.Ready);
    }

    public void SendVote(string mapName)
    {
        if (!isConnected) return;
        LogToFile($"[SendVote] Voting for '{mapName}'.");
        SendPacket(NetProtocol.Magic + NetProtocol.Vote + mapName);
    }

    public void SendSpectate()
    {
        if (!isConnected) return;
        LogToFile("[SendSpectate] Sending SPECTATE.");
        SendPacket(NetProtocol.Magic + NetProtocol.Spectate);
    }

    /// <summary>
    /// Sends a chat message to the server.
    /// The server stamps the sender name and relays it to all clients.
    /// </summary>
    public void SendChat(string text)
    {
        if (!isConnected || string.IsNullOrWhiteSpace(text)) return;
        SendPacket(NetProtocol.Magic + NetProtocol.Chat + text);
    }

    private void SendPacket(string message)
    {
        if (_gameUdp == null || _serverEndpoint == null) return;
        byte[] data = Encoding.UTF8.GetBytes(message);
        try   { _gameUdp.Send(data, data.Length, _serverEndpoint); }
        catch (Exception e) { LogToFile($"[SendPacket] ERROR: {e.Message}"); }
    }

    private IEnumerator ShowFighterSelect()
    {
        yield return null; // Wait one frame for the world to finish initialising
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && fighterSelectWidget != null)
            widgetManager.AddWidget(fighterSelectWidget);
        else
            LogToFile("[ShowFighterSelect] WARNING: widgetManager or fighterSelectWidget is null.");
    }


    /*-----[ Logging ]------------------------------------------------------------------------------------------------*/

    public static void LogToFile(string message)
    {
        string path = Path.Combine(Application.persistentDataPath, "netlog.txt");
        File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
    }

    #endregion


    #region ========================================( Registration )=================================================//

    // Called by NetTransform.Start()
    public void RegisterNetTransform(string objectId, NetTransform nt)
    {
        if (string.IsNullOrEmpty(objectId)) return;
        netTransforms[objectId] = nt;
        LogToFile($"[RegisterNetTransform] id={objectId}");
    }

    // Called by NetTransform.OnDestroy()
    public void UnregisterNetTransform(string objectId)
    {
        netTransforms.Remove(objectId);
        LogToFile($"[UnregisterNetTransform] id={objectId}");
    }

    // Called by NetVarOwner.Start()
    public void RegisterNetVarOwner(NetVarOwner owner)
    {
        string id = owner.NetworkObjectId;
        if (string.IsNullOrEmpty(id)) return;
        netVarOwners[id] = owner;
        LogToFile($"[RegisterNetVarOwner] id={id}");
    }

    // Called by NetVarOwner.OnDestroy()
    public void UnregisterNetVarOwner(NetVarOwner owner)
    {
        netVarOwners.Remove(owner.NetworkObjectId);
        LogToFile($"[UnregisterNetVarOwner] id={owner.NetworkObjectId}");
    }

    #endregion
}


// ============================================( Supporting Types )====================================================//

// Server browser list
[Serializable]
public class ServerEntry
{
    public string serverName;
    public string serverAddress;
}

// JsonUtility doesn't support raw top-level lists, so wrap it
[Serializable]
public class ServerEntryListWrapper
{
    public List<ServerEntry> servers;
}

public static class NetProtocol
{
    // Must match the dedicated server's protocol magic string
    public const string Magic          = "FeKa";
    public const int    QueryPort      = 27015;
    public const float  TimeoutSeconds = 3f;

    // Outgoing (client -> server)
    public const string Ready    = ":READY";
    public const string Vote     = ":VOTE:";
    public const string Spectate = ":SPECTATE";
    public const string Pong     = ":PONG";
    public const string Chat     = ":CHAT:";

    // Incoming (server -> client)
    public const string State   = ":STATE:";
    public const string MapVote = ":MAPVOTE:";
    public const string LoadMap = ":LOADMAP:";
    public const string Spawn   = ":SPAWN:";
    public const string Ping    = ":PING";
}

[Serializable]
public class ServerQueryResponse
{
    public string ServerName;
    public string MapName;
    public string GameMode;
    public int    MaxPlayers;
    public int    PlayerCount;
    public string IconBase64;
}

[Serializable]
public class NetProfile
{
    public string playerName = "NetPlayer";
}

[Serializable]
public class PlayerNameEntry
{
    public string name;
    public int    ping;
}

[Serializable]
public class GameStatePacket
{
    public string                Phase;
    public string                MapName;
    public string                GameMode;
    public int                   TimeLeft;
    public List<PlayerNameEntry> PlayerNames;
}