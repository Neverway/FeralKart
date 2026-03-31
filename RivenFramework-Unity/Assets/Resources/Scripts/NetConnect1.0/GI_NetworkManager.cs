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
    public NetProfile localProfile = new NetProfile();
    public bool isConnected  = false;
    public string connectedAddress = "";
    public string currentPhase = null;
    public string lastKnownMapName = "";
    public GameStatePacket lastGameState = null;

    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private string configFilePath => Path.Combine(Application.persistentDataPath, "serverlist.config");
    private string profileFilePath => Path.Combine(Application.persistentDataPath, "netprofile.profile");
    private UdpClient _gameUdp;
    private IPEndPoint _serverEndpoint;
    private Coroutine _heartbeatCoroutine;
    private Coroutine _listenCoroutine;
    private Coroutine _varSyncCoroutine;
    private bool _receivedFirstState = false;
    
    private readonly Dictionary<string, NetTransform> netTransforms = new Dictionary<string, NetTransform>();
    private readonly Dictionary<string, NetVarOwner> netVarOwners = new Dictionary<string, NetVarOwner>();
    
    // Pending network spawns
    private readonly Dictionary<string, Action<GameObject, string>> pendingSpawnCallbacks = new Dictionary<string, Action<GameObject, string>>();
    // Syncing network objects
    private readonly Dictionary<string, GameObject> netObjects = new Dictionary<string, GameObject>();
    // Serverside counter to generate network object unique id's
    private static int nextObjectId = 1;
    

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;
    [SerializeField] public List<ServerEntry> serverEntries = new List<ServerEntry>();
    public event Action<GameStatePacket> OnGameStateReceived;
    public event Action<string, string> OnChatReceived;
    public event Action<string> OnMapVoteReceived;
    public event Action<string> OnKicked;
    public event Action OnDisconnected;
    public GameObject fighterSelectWidget, connectionMessageWidget, netChatWidget;
    [SerializeField] public string titleScreenWorldName = "_Title";
    public string LocalSpectatorNetworkId { get; private set; } = null;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Awake()
    {
        OnKicked      += HandleKicked;
        OnDisconnected += HandleDisconnected;
    }

    public void Update()
    {
        if (isConnected)
        {
            widgetManager ??= GameInstance.Get<GI_WidgetManager>();
            if (!widgetManager.GetExistingWidget(netChatWidget.gameObject.name))
            {
                widgetManager.AddWidget(netChatWidget);
            }
        }
        else
        {
            widgetManager ??= GameInstance.Get<GI_WidgetManager>();
            if (widgetManager.GetExistingWidget(netChatWidget.gameObject.name))
            {
                Destroy(widgetManager.GetExistingWidget(netChatWidget.gameObject.name));
            }
        }
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void SaveServerConfigFile()
    {
        var wrapper = new ServerEntryListWrapper { servers = serverEntries };
        string json = JsonUtility.ToJson(wrapper, prettyPrint:true);
        File.WriteAllText(configFilePath, json);
    }
    
    /// <summary>
    /// Opens the connection message popup with the given text, then returns the player to the title screen.
    /// </summary>
    private void ShowConnectionPopupAndReturnToTitle(string title, string message)
    {
        widgetManager ??= FindObjectOfType<GI_WidgetManager>();
        var worldLoader = GameInstance.Get<GI_WorldLoader>();
        
        if (worldLoader != null)
        {
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
                var popup = FindObjectOfType<WB_ConnectionMessage>();
                popup?.Setup(title, message, "Close");
            }

            GI_WorldLoader.OnWorldLoaded += OnLoaded;
            worldLoader.LoadWorld(titleScreenWorldName);
        }
    }
    
    /// <summary>
    /// Spawns the local player's networked spectator body and gives it local authority.
    /// Skips silently if one is already alive.
    /// </summary>
    private void SpawnSpectatorBody()
    {
        Debug.Log($"[SpawnSpectatorBody] Called. LocalSpectatorNetworkId='{LocalSpectatorNetworkId}'");
        if (!string.IsNullOrEmpty(LocalSpectatorNetworkId)) return;
 
        Debug.Log("called spawn spectator");
        NetSpawner.Spawn(spectatorPrefabKey, spectatorSpawnPosition, Quaternion.identity, (go, networkId) =>
        {
            LocalSpectatorNetworkId = networkId;
 
            var spectator = go.GetComponent<Pawn_Spectator>();
            if (spectator != null)
            {
                spectator.controlMode = ControlMode.LocalPlayer;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
 
            Debug.Log($"[NetworkManager] Spectator body spawned (id={networkId})");
        });
    }
 
    /// <summary>
    /// Despawns the local player's networked spectator body if one exists.
    /// Restores the cursor for UI use.
    /// </summary>
    public void DespawnSpectatorBody()
    {
        if (string.IsNullOrEmpty(LocalSpectatorNetworkId)) return;
 
        NetSpawner.Despawn(LocalSpectatorNetworkId);
        LocalSpectatorNetworkId = null;
 
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
 
        Debug.Log("[NetworkManager] Spectator body despawned");
    }
    
    
    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void LoadServerConfigFile()
    {
        // Does the file exist?
        if (!File.Exists(configFilePath))
        {
            // No -> Create file with no entries
            serverEntries = new List<ServerEntry>();
            SaveServerConfigFile();
            return;
        }
        // Yes -> Load the server entries
        string json = File.ReadAllText(configFilePath);
        var wrapper = JsonUtility.FromJson<ServerEntryListWrapper>(json);
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
        // Add code here to write to the server config file and add a new entry
        serverEntries.Add(new ServerEntry { serverName = serverName, serverAddress = serverAddress });
        SaveServerConfigFile();
    }

    /// <summary>
    /// Sends a UDP query packet to a server and waits for a response
    /// Calls onSuccess with the response from the server with whatever data we needed, or onFailure if it times out
    /// </summary>
    /// <param name="address">The IP address of the server</param>
    public IEnumerator QueryServer(string address, Action<ServerQueryResponse, int> onSuccess, Action onFailure)
    {
        string host = address;
        int port = NetProtocol.QueryPort;

        if (address.Contains(':'))
        {
            var parts = address.Split(':');
            host = parts[0];
            int.TryParse(parts[1], out port);
        }

        UdpClient udp = null;
        IPEndPoint endpoint = null;
        
        // Try to resolve the DNS on a background thread so unity doesn't start whining
        bool resolved = false;
        bool resolveFailed = false;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var addresses = Dns.GetHostAddresses(host);
                endpoint = new IPEndPoint(addresses[0], port);
                resolved = true;
            }
            catch (Exception e)
            {
                resolveFailed = true;
            }
        });
        
        // Wait for the DNS
        float resolveTimer = 0f;
        while (!resolved && !resolveFailed)
        {
            resolveTimer += Time.unscaledDeltaTime;
            if (resolveTimer > NetProtocol.TimeoutSeconds)
            {
                onFailure?.Invoke();
                yield break;
            }
            yield return null;
        }

        if (resolveFailed)
        {
            onFailure?.Invoke();
            yield break;
        }

        // Send the query packet
        try
        {
            udp = new UdpClient();
            udp.Client.ReceiveTimeout = Mathf.RoundToInt(NetProtocol.TimeoutSeconds * 1000);

            byte[] queryPacket = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":QUERY");
            udp.Send(queryPacket, queryPacket.Length, endpoint);
        }
        catch
        {
            udp?.Close();
            onFailure?.Invoke();
            yield break;
        }
        
        // Wait for the response on a background thread, also keep track of the ping
        bool recieved = false;
        bool timedOut = false;
        string rawResponse = null;
        int measuredPing = 0;

        DateTime sendTime = DateTime.UtcNow;

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udp.Receive(ref remote);
                measuredPing = (int)(DateTime.UtcNow - sendTime).TotalMilliseconds;
                rawResponse = Encoding.UTF8.GetString(data);
                recieved = true;
            }
            catch
            {
                timedOut = true;
            }
            finally
            {
                udp.Close();
            }
        });
        
        // Wait for that to complete
        float waitTimer = 0f;
        while (!recieved && !timedOut)
        {
            waitTimer += Time.unscaledDeltaTime;
            if (waitTimer > NetProtocol.TimeoutSeconds + 0.5f)
            {
                timedOut = true;
            }
            yield return null;
        }

        if (timedOut || rawResponse == null)
        {
            onFailure?.Invoke();
            yield break;
        }
        
        // Strip the protocol header and parse the json
        string prefix = NetProtocol.Magic + ":RESPONSE:";
        if (!rawResponse.StartsWith(prefix))
        {
            onFailure?.Invoke();
            yield break;
        }
        
        string json = rawResponse.Substring(prefix.Length);

        ServerQueryResponse result;
        try
        {
            result = JsonUtility.FromJson<ServerQueryResponse>(json);
        }
        catch
        {
            onFailure?.Invoke();
            yield break;
        }
        
        onSuccess?.Invoke(result, measuredPing);
    }
    
    // Player
    public void LoadNetProfile()
    {
        if (!File.Exists(profileFilePath))
        {
            localProfile = new NetProfile();
            SaveNetProfile();
            return;
        }
        string json = File.ReadAllText(profileFilePath);
        localProfile = JsonUtility.FromJson<NetProfile>(json) ?? new NetProfile();
    }
    
    public void SaveNetProfile()
    {
        File.WriteAllText(profileFilePath, JsonUtility.ToJson(localProfile, true));
    }
    
    public IEnumerator Connect(string address, Action onSuccess, Action<string> onFailure)    
    {
        // Parse host and port
        string host = address;
        int    port = NetProtocol.QueryPort;
        if (address.Contains(':'))
        {
            var parts = address.Split(':');
            host = parts[0];
            int.TryParse(parts[1], out port);
        }

        // Resolve DNS
        bool resolved      = false;
        bool resolveFailed = false;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var addrs    = Dns.GetHostAddresses(host);
                _serverEndpoint = new IPEndPoint(addrs[0], port);
                resolved     = true;
            }
            catch { resolveFailed = true; }
        });

        float t = 0f;
        while (!resolved && !resolveFailed)
        {
            t += Time.unscaledDeltaTime;
            if (t > NetProtocol.TimeoutSeconds) { onFailure?.Invoke("Could not resolve host."); yield break; }
            yield return null;
        }
        if (resolveFailed) { onFailure?.Invoke("Could not resolve host."); yield break; }
        
        // Open a persistent UDP socket for this session
        _gameUdp = new UdpClient();
        _gameUdp.Client.ReceiveTimeout = Mathf.RoundToInt(NetProtocol.TimeoutSeconds * 1000);

        // Send join packet
        string joinMsg    = NetProtocol.Magic + ":JOIN:" + localProfile.playerName;
        byte[] joinBytes  = Encoding.UTF8.GetBytes(joinMsg);

        try { _gameUdp.Send(joinBytes, joinBytes.Length, _serverEndpoint); }
        catch { _gameUdp.Close(); onFailure?.Invoke("Could not reach server."); yield break; }
        
        // Wait for ACCEPTED or REJECTED on background thread
        bool   gotReply  = false;
        bool   accepted  = false;
        string rejectMsg = "";

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var    remote = new IPEndPoint(IPAddress.Any, 0);
                string reply  = Encoding.UTF8.GetString(_gameUdp.Receive(ref remote));

                if (reply == NetProtocol.Magic + ":JOIN:ACCEPTED")
                {
                    accepted = true;
                }
                else if (reply.StartsWith(NetProtocol.Magic + ":JOIN:REJECTED:"))
                {
                    rejectMsg = reply.Substring((NetProtocol.Magic + ":JOIN:REJECTED:").Length);
                }
                gotReply = true;
            }
            catch { gotReply = true; } // timed out
        });

        float wait = 0f;
        while (!gotReply)
        {
            wait += Time.unscaledDeltaTime;
            if (wait > NetProtocol.TimeoutSeconds) break;
            yield return null;
        }

        if (!accepted)
        {
            _gameUdp.Close();
            onFailure?.Invoke(string.IsNullOrEmpty(rejectMsg) ? "Connection timed out." : rejectMsg);
            yield break;
        }

        isConnected      = true;
        connectedAddress = address;

        // Start heartbeat and server listen loops
        _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
        _listenCoroutine    = StartCoroutine(ListenLoop());
        _varSyncCoroutine = StartCoroutine(VarSyncLoop());

        onSuccess?.Invoke();
    }

    public void Disconnect()
    {        
        if (!isConnected) return;
        
        DespawnSpectatorBody();
 
        // Tell the server we are leaving
        try
        {
            byte[] leaveBytes = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":LEAVE");
            _gameUdp?.Send(leaveBytes, leaveBytes.Length, _serverEndpoint);
        }
        catch { }
 
        CleanupConnection();
        OnDisconnected?.Invoke();
    }
    
    private void HandleKicked(string reason)
    {
        DespawnSpectatorBody();
        ShowConnectionPopupAndReturnToTitle("Kicked", string.IsNullOrEmpty(reason) ? "You were kicked from the server." : reason);
    }
 
    private void HandleDisconnected()
    {
        ShowConnectionPopupAndReturnToTitle("Disconnected", "You have been disconnected from the server.");
    }

    private void CleanupConnection()
    {
        if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);
        if (_listenCoroutine    != null) StopCoroutine(_listenCoroutine);
        _gameUdp?.Close();
        _gameUdp         = null;
        _serverEndpoint  = null;
        isConnected      = false;
        connectedAddress = "";
        currentPhase     = null;
        lastKnownMapName = ""; 
        LocalSpectatorNetworkId = null;
        _receivedFirstState = false;
    }
    
    /// <summary>
    /// Sends a heartbeat to the server every 5 seconds so it knows we're still alive
    /// </summary>
    private IEnumerator HeartbeatLoop()
    {
        byte[] heartbeat = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":HEARTBEAT");
        while (isConnected)
        {
            yield return new WaitForSecondsRealtime(5f);
            try { _gameUdp?.Send(heartbeat, heartbeat.Length, _serverEndpoint); }
            catch { break; }
        }
    }
    
    private IEnumerator VarSyncLoop()
    {
        float interval = 1f / Mathf.Max(1f, varSyncRate);
        while (isConnected)
        {
            yield return new WaitForSecondsRealtime(interval);
 
            foreach (var kv in netVarOwners)
            {
                // Only send for objects this client owns
                if (!netTransforms.TryGetValue(kv.Key, out var nt) || !nt.hasAuthority) continue;
 
                var dirty = kv.Value.FlushDirtyVars();
                if (dirty.Count == 0) continue;
 
                var packet = new VariableSyncPacket { ObjectId = kv.Key, Vars = dirty };
                SendPacket(NetProtocol.Magic + ":VARSYNC:" + JsonUtility.ToJson(packet));
            }
        }
    }
    
    /// <summary>
    /// Listens for packets from the server while connected (kicks, game state, etc.)
    /// </summary>
    private IEnumerator ListenLoop()
    {
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

            if (packet.StartsWith(NetProtocol.Magic + ":KICKED:"))
            {
                string reason = packet.Substring((NetProtocol.Magic + ":KICKED:").Length);
                Debug.LogWarning($"Kicked: {reason}");
                CleanupConnection();
                OnKicked?.Invoke(reason);
                yield break;
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":STATE:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":STATE:").Length);
                var    gs   = JsonUtility.FromJson<GameStatePacket>(json);
                Debug.Log($"[ListenLoop] STATE received. Phase={gs.Phase} previousPhase={currentPhase} _receivedFirstState={_receivedFirstState}");
                lastGameState    = gs;
                lastKnownMapName = gs.MapName;
                
                string previousPhase = currentPhase;
                currentPhase = gs.Phase;
 
                // First state packet after connecting - load the server's current map
                if (!_receivedFirstState)
                {
                    _receivedFirstState = true;
                    Debug.Log($"[NetworkManager] First state received. Phase={gs.Phase} Map={gs.MapName}");
    
                    var worldLoader = GameInstance.Get<GI_WorldLoader>();
                    if (worldLoader != null)
                    {
                        if (gs.Phase != "InProgress")
                        {
                            void OnLoaded()
                            {
                                GI_WorldLoader.OnWorldLoaded -= OnLoaded;
                                StartCoroutine(ShowFighterSelect());
                            }
                            GI_WorldLoader.OnWorldLoaded += OnLoaded;
                        }
                        else
                        {
                            // Joined mid-game: load the world and go straight to spectator
                            void OnLoaded()
                            {
                                GI_WorldLoader.OnWorldLoaded -= OnLoaded;
                                SpawnSpectatorBody();
                            }
                            GI_WorldLoader.OnWorldLoaded += OnLoaded;
                        }
                        worldLoader.LoadWorld(gs.MapName);
                    }
                }
                else
                {
                    // Server moved to Loading: despawn spectator (fighter select's local camera takes over),
                    // load the new map, then spawn the spectator body once it's ready
                    if (gs.Phase == "Loading" && previousPhase != "Loading")
                    {
                        DespawnSpectatorBody();
 
                        var worldLoader = GameInstance.Get<GI_WorldLoader>();
                        if (worldLoader != null)
                        {
                            void OnLoaded()
                            {
                                GI_WorldLoader.OnWorldLoaded -= OnLoaded;
                                SpawnSpectatorBody();
                            }
                            GI_WorldLoader.OnWorldLoaded += OnLoaded;
                            worldLoader.LoadWorld(gs.MapName);
                        }
                    }
                    else if (gs.Phase == "InProgress" && previousPhase == "Intermission")
                    {
                        // Missed the Loading phase — spawn spectator directly
                        var worldLoader = GameInstance.Get<GI_WorldLoader>();
                        if (worldLoader != null)
                        {
                            void OnLoaded()
                            {
                                GI_WorldLoader.OnWorldLoaded -= OnLoaded;
                                SpawnSpectatorBody();
                            }
                            GI_WorldLoader.OnWorldLoaded += OnLoaded;
                            worldLoader.LoadWorld(gs.MapName);
                        }
                    }
                    // Game ended and we're back in intermission: despawn spectator, show fighter select
                    else if (gs.Phase == "Intermission" && previousPhase == "InProgress")
                    {
                        DespawnSpectatorBody();
                        StartCoroutine(ShowFighterSelect());
                    }
                }
 
                OnGameStateReceived?.Invoke(gs);
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":MAPVOTE:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":MAPVOTE:").Length);
                OnMapVoteReceived?.Invoke(json);
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":LOADMAP:"))
            {
                string mapName  = packet.Substring((NetProtocol.Magic + ":LOADMAP:").Length);
                var worldLoader = FindObjectOfType<RivenFramework.GI_WorldLoader>();
                if (worldLoader != null) worldLoader.LoadWorld(mapName);
            }
            else if (packet.StartsWith(NetProtocol.Magic + NetProtocol.Chat))
            {
                string body   = packet[(NetProtocol.Magic + NetProtocol.Chat).Length..];
                int    sep    = body.IndexOf(':');
                if (sep < 0) continue;

                string sender  = body[..sep];
                string text    = body[(sep + 1)..];
                OnChatReceived?.Invoke(sender, text);
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":PING"))
            {
                byte[] pong = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":PONG");
                try { _gameUdp?.Send(pong, pong.Length, _serverEndpoint); }
                catch { }
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":TSYNC:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":TSYNC:").Length);
                var    tsp  = JsonUtility.FromJson<TransformSyncPacket>(json);
                if (tsp != null && netTransforms.TryGetValue(tsp.objectUId, out var nt))
                    nt.ReceiveTransformPacket(tsp);
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":SPAWN:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":SPAWN:").Length);
                var    sbp  = JsonUtility.FromJson<SpawnBroadcastPacket>(json);
                if (sbp != null) HandleSpawnBroadcast(sbp);
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":DESPAWN:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":DESPAWN:").Length);
                var    dp   = JsonUtility.FromJson<DespawnPacket>(json);
                if (dp != null) HandleDespawnBroadcast(dp);
            }
            else if (packet.StartsWith(NetProtocol.Magic + ":VARSYNC:"))
            {
                string json = packet.Substring((NetProtocol.Magic + ":VARSYNC:").Length);
                var    vsp  = JsonUtility.FromJson<VariableSyncPacket>(json);
                if (vsp != null && netVarOwners.TryGetValue(vsp.ObjectId, out var owner))
                    foreach (var entry in vsp.Vars)
                        owner.ApplyRemoteEntry(entry);
            }
        }
    }
    
    /// <summary>Serialize and send a TSYNC packet to the server.</summary>
    public void SendTransformSync(NetTransform nt)
    {
        if (!isConnected) return;
 
        var packet = new TransformSyncPacket
        {
            objectUId = nt.networkObjectUId,
            syncPosition = nt.syncPosition,
            syncRotation = nt.syncRotation,
            syncScale = nt.syncScale,
        };
 
        if (nt.syncPosition)
        {
            packet.px = nt.transform.position.x;
            packet.py = nt.transform.position.y;
            packet.pz = nt.transform.position.z;
        }
        if (nt.syncRotation)
        {
            Vector3 euler = nt.transform.rotation.eulerAngles;
            packet.rx = euler.x;
            packet.ry = euler.y;
            packet.rz = euler.z;
        }
        if (nt.syncScale)
        {
            packet.sx = nt.transform.localScale.x;
            packet.sy = nt.transform.localScale.y;
            packet.sz = nt.transform.localScale.z;
        }
 
        SendPacket(NetProtocol.Magic + ":TSYNC:" + JsonUtility.ToJson(packet));
    }
    
    /// <summary>
    /// Request the server to spawn a networked prefab, called by NetSpawner
    /// </summary>
    public void RequestSpawn(string prefabKey, Vector3 position, Quaternion rotation, Action<GameObject, string> onSpawned)
    {
        string requestId = System.Guid.NewGuid().ToString();
    
        if (onSpawned != null)
            pendingSpawnCallbacks[requestId] = onSpawned;

        var req = new SpawnRequestPacket
        {
            PrefabKey = prefabKey,
            RequestId = requestId,
            PX = position.x, PY = position.y, PZ = position.z,
            RX = rotation.eulerAngles.x, RY = rotation.eulerAngles.y, RZ = rotation.eulerAngles.z,
        };

        SendPacket(NetProtocol.Magic + ":SPAWNREQ:" + JsonUtility.ToJson(req));
    }
 
    /// <summary>Request the server to despawn a networked object. Called by NetSpawner.</summary>
    public void RequestDespawn(string networkObjectId)
    {
        if (!isConnected) return;
        var pkt = new DespawnPacket { NetworkObjectId = networkObjectId };
        SendPacket(NetProtocol.Magic + ":DESPAWNREQ:" + JsonUtility.ToJson(pkt));
    }
 
    private void HandleSpawnBroadcast(SpawnBroadcastPacket sbp)
    {
        Debug.Log($"[HandleSpawnBroadcast] Received SPAWN broadcast. PrefabKey={sbp.PrefabKey} NetworkId={sbp.NetworkObjectId} RequestId={sbp.RequestId}");

        if (prefabRegistry == null)
        {
            Debug.LogError("[GI_NetworkManager] No NetPrefabRegistry assigned — cannot spawn networked object.");
            return;
        }

        GameObject prefab = prefabRegistry.GetPrefab(sbp.PrefabKey);
        if (prefab == null)
        {
            Debug.LogError($"[GI_NetworkManager] Prefab key '{sbp.PrefabKey}' not found in registry.");
            return;
        }

        Vector3    pos = new Vector3(sbp.PX, sbp.PY, sbp.PZ);
        Quaternion rot = Quaternion.Euler(sbp.RX, sbp.RY, sbp.RZ);
        GameObject go  = Instantiate(prefab, pos, rot);
        Debug.Log($"[HandleSpawnBroadcast] Instantiated prefab at {pos}");

        var nt = go.GetComponent<NetTransform>();
        Debug.Log($"[HandleSpawnBroadcast] NetTransform found={nt != null}");

        if (nt != null)
        {
            nt.networkObjectUId = sbp.NetworkObjectId;
            bool hasRequestId = !string.IsNullOrEmpty(sbp.RequestId);
            bool isInPending  = pendingSpawnCallbacks.ContainsKey(sbp.RequestId ?? "");
            nt.hasAuthority   = hasRequestId && isInPending;
            Debug.Log($"[HandleSpawnBroadcast] hasRequestId={hasRequestId} isInPending={isInPending} hasAuthority={nt.hasAuthority}");
            Debug.Log($"[HandleSpawnBroadcast] pendingSpawnCallbacks keys: {string.Join(", ", pendingSpawnCallbacks.Keys)}");
        }

        netObjects[sbp.NetworkObjectId] = go;

        if (nt != null && nt.hasAuthority && pendingSpawnCallbacks.TryGetValue(sbp.RequestId, out var cb))
        {
            Debug.Log("[HandleSpawnBroadcast] Firing spawn callback!");
            pendingSpawnCallbacks.Remove(sbp.RequestId);
            cb?.Invoke(go, sbp.NetworkObjectId);
        }
        else
        {
            Debug.LogWarning($"[HandleSpawnBroadcast] Callback NOT fired. nt={nt != null} hasAuthority={nt?.hasAuthority} requestId={sbp.RequestId}");
        }
    }
 
    private void HandleDespawnBroadcast(DespawnPacket dp)
    {
        if (netObjects.TryGetValue(dp.NetworkObjectId, out var go))
        {
            if (go != null) Destroy(go);
            netObjects.Remove(dp.NetworkObjectId);
        }
        netTransforms.Remove(dp.NetworkObjectId);
        netVarOwners.Remove(dp.NetworkObjectId);
    }
    
    public void SendReady()
    {
        if (!isConnected) return;
        SendPacket(NetProtocol.Magic + NetProtocol.Ready);
    }
 
    public void SendVote(string mapName)
    {
        if (!isConnected) return;
        SendPacket(NetProtocol.Magic + NetProtocol.Vote + mapName);
    }
 
    public void SendSpectate()
    {
        if (!isConnected) return;
        SendPacket(NetProtocol.Magic + NetProtocol.Spectate);
    }
    
    /// <summary>
    /// Sends a chat message to the server. The server will stamp our name and relay it to all clients.
    /// </summary>
    /// <param name="text">The message text. Clamped to 200 characters server-side.</param>
    public void SendChat(string text)
    {
        if (!isConnected || string.IsNullOrWhiteSpace(text)) return;
        SendPacket(NetProtocol.Magic + NetProtocol.Chat + text);
    }
 
    private void SendPacket(string message)
    {
        if (_gameUdp == null || _serverEndpoint == null) return;
        byte[] data = Encoding.UTF8.GetBytes(message);
        try { _gameUdp.Send(data, data.Length, _serverEndpoint); }
        catch (Exception e) { Debug.LogWarning($"[GI_NetworkManager] Send failed: {e.Message}"); }
    }
    
    private IEnumerator ShowFighterSelect()
    {
        yield return null;
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && fighterSelectWidget != null)
        {
            widgetManager.AddWidget(fighterSelectWidget);
        }
        else
        {
            Debug.Log("No fighter select");
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }



    #endregion
    
    
    #region ========================================( Registration )=================================================//
    // Called by NetTransform.Start()
    public void RegisterNetTransform(string objectId, NetTransform nt)
    {
        if (string.IsNullOrEmpty(objectId)) return;
        netTransforms[objectId] = nt;
    }
 
    // Called by NetTransform.OnDestroy()
    public void UnregisterNetTransform(string objectId)
    {
        netTransforms.Remove(objectId);
    }
 
    // Called by NetVarOwner.Start()
    public void RegisterNetVarOwner(NetVarOwner owner)
    {
        string id = owner.NetworkObjectId;
        if (string.IsNullOrEmpty(id)) return;
        netVarOwners[id] = owner;
    }
 
    // Called by NetVarOwner.OnDestroy()
    public void UnregisterNetVarOwner(NetVarOwner owner)
    {
        netVarOwners.Remove(owner.NetworkObjectId);
    }
 
    #endregion
    
    
}

// Server browser list
[Serializable]
public class ServerEntry
{
    public string serverName;
    public string serverAddress;
}

// JsonUtil is a whiny little bitch baby and doesn't want a raw list of T at the top, so I guess there is a wrapper for it now >:[
[Serializable]
public class ServerEntryListWrapper
{
    public List<ServerEntry> servers;
}

// Server heartbeat
public static class NetProtocol
{
    // This should match the data that I set in the dedicated server's program.cs, otherwise the server won't talk
    public const string Magic = "FeKa";
    public const int QueryPort = 27015;
    public const float TimeoutSeconds = 3f;

    // Outgoing
    public const string Ready    = ":READY";
    public const string Vote     = ":VOTE:";
    public const string Spectate = ":SPECTATE";
    public const string Pong     = ":PONG";
    public const string Chat     = ":CHAT:";  

    // Incoming
    public const string State    = ":STATE:";
    public const string MapVote  = ":MAPVOTE:";
    public const string LoadMap  = ":LOADMAP:";
    public const string Spawn    = ":SPAWN:";
    public const string Ping     = ":PING";
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

// Player
[Serializable]
public class NetProfile
{
    public string playerName = "NetPlayer";
}

// Json utility is being a whiny baby and doesn't like lists of strings, so this will have to do
[Serializable]
public class PlayerNameEntry
{
    public string name;
    public int ping;
}

[Serializable]
public class GameStatePacket
{
    public string       Phase;
    public string       MapName;
    public string       GameMode;
    public int          TimeLeft;
    public List<PlayerNameEntry> PlayerNames;
}