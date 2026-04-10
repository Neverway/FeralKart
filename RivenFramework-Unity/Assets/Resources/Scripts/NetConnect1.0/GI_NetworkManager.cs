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
    [Header("Protocol")]
    [Tooltip("Must match the ProtocolMagic field in the server's server.config")]
    public string protocolMagic = "NyNet";

    [Header("Network Object Syncing")]
    [Tooltip("Registry mapping prefab keys to prefabs for spawning over the network")]
    public NetPrefabRegistry prefabRegistry;
    [Tooltip("How many times per second variable changes are sent, 10 is default")]
    public float varSyncRate = 10f;


    /*-----[ External Variables ]---------------------------------------------------------------------------------*/
    public NetProfile localProfile = new NetProfile();
    public bool isConnected = false;
    public string connectedAddress = "";
    public bool isOp = false;


    /*-----[ Internal Variables ]---------------------------------------------------------------------------------*/
    private string configFilePath => Path.Combine(Application.persistentDataPath, "serverlist.config");
    private string profileFilePath => Path.Combine(Application.persistentDataPath, "netprofile.profile");

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> inboundPackets = new System.Collections.Concurrent.ConcurrentQueue<string>();
    private Thread receiveThread;

    // UDP socket and server connection
    private UdpClient gameUdp;
    private IPEndPoint serverEndpoint;

    // Active coroutines, stored so they can be stopped on disconnect
    private Coroutine heartbeatCoroutine;
    private Coroutine listenCoroutine;
    private Coroutine varSyncCoroutine;

    // Connection state flags
    private bool worldIsReady = false;
    private bool isLoadingWorld = false;

    // Network object tracking
    private readonly Dictionary<string, NetTransform> netTransforms = new Dictionary<string, NetTransform>();
    private readonly Dictionary<string, NetVarOwner> netVarOwners = new Dictionary<string, NetVarOwner>();
    private readonly Dictionary<string, Action<GameObject, string>> pendingSpawnCallbacks = new Dictionary<string, Action<GameObject, string>>();
    private readonly Dictionary<string, GameObject> netObjects = new Dictionary<string, GameObject>();
    private readonly List<SpawnBroadcastPacket> pendingSpawnQueue = new List<SpawnBroadcastPacket>();


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    private GI_WidgetManager widgetManager;

    [SerializeField] public List<ServerEntry> serverEntries = new List<ServerEntry>();

    // Events that other scripts can subscribe to
    public event Action<string> OnRawPacketReceived;
    public event Action<PlayerListPacket> OnPlayerListReceived;
    public event Action<string, string> OnChatReceived;
    public event Action<string> OnKicked;
    public event Action OnDisconnected;
    public event Action<bool> OnOpStatusChanged;
    public event Action<VoteKickPacket> OnVoteKickReceived;
    public event Action<VoteKickResultPacket> OnVoteKickResultReceived;

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
    /// Called when the server kicks this client.
    /// Fires OnKicked so game-specific code can react (show a popup, return to menu, etc.)
    /// </summary>
    private void HandleKicked(string reason)
    {
        LogToFile($"[HandleKicked] reason='{reason}'");
    }

    /// <summary>
    /// Called when the client disconnects from the server.
    /// Fires OnDisconnected so game-specific code can react.
    /// </summary>
    private void HandleDisconnected()
    {
        LogToFile("[HandleDisconnected] Disconnected from server.");
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
        worldIsReady = false;

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
        byte[] heartbeat = Encoding.UTF8.GetBytes(protocolMagic + ":HEARTBEAT");
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
                SendPacket(protocolMagic + ":VARSYNC:" + JsonUtility.ToJson(packet));
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
        { IsBackground = true, Name = "NyNet-Receive" };

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
    /// Game-specific packet handling should subscribe to OnRawPacketReceived rather than modifying this method.
    /// </summary>
    private void ProcessPacket(string packet)
    {
        // --- Opped ---
        if (packet == protocolMagic + ":OPPED")
        {
            isOp = true;
            OnOpStatusChanged?.Invoke(true);
            return;
        }

        // --- Deopped ---
        if (packet == protocolMagic + ":DEOPPED")
        {
            isOp = false;
            OnOpStatusChanged?.Invoke(false);
            return;
        }

        // --- Kicked ---
        if (packet.StartsWith(protocolMagic + ":KICKED:"))
        {
            string reason = packet.Substring((protocolMagic + ":KICKED:").Length);
            LogToFile($"[ProcessPacket] KICKED reason='{reason}'");
            CleanupConnection();
            OnKicked?.Invoke(reason);
            return;
        }

        // --- Player List ---
        // The engine sends the connected player list so the client can display it.
        // Game-specific state (phase, map, timeleft) is sent by game rules code and
        // handled via OnRawPacketReceived below.
        if (packet.StartsWith(protocolMagic + ":PLAYERLIST:"))
        {
            string json = packet.Substring((protocolMagic + ":PLAYERLIST:").Length);
            var playerList = JsonUtility.FromJson<PlayerListPacket>(json);
            if (playerList != null) OnPlayerListReceived?.Invoke(playerList);
            return;
        }

        // --- Chat ---
        if (packet.StartsWith(protocolMagic + ":CHAT:"))
        {
            string body = packet.Substring((protocolMagic + ":CHAT:").Length);
            int separatorIndex = body.IndexOf(':');
            if (separatorIndex >= 0)
                OnChatReceived?.Invoke(body.Substring(0, separatorIndex), body.Substring(separatorIndex + 1));
            return;
        }

        // --- Ping ---
        if (packet.StartsWith(protocolMagic + ":PING"))
        {
            byte[] pong = Encoding.UTF8.GetBytes(protocolMagic + ":PONG");
            try { gameUdp?.Send(pong, pong.Length, serverEndpoint); } catch { }
            return;
        }

        // --- Transform Sync ---
        if (packet.StartsWith(protocolMagic + ":TSYNC:"))
        {
            var transformSyncPacket = JsonUtility.FromJson<TransformSyncPacket>(
                packet.Substring((protocolMagic + ":TSYNC:").Length));
            if (transformSyncPacket != null && netTransforms.TryGetValue(transformSyncPacket.objectUId, out var netTransform))
                netTransform.ReceiveTransformPacket(transformSyncPacket);
            return;
        }

        // --- Spawn ---
        if (packet.StartsWith(protocolMagic + ":SPAWN:"))
        {
            var spawnBroadcastPacket = JsonUtility.FromJson<SpawnBroadcastPacket>(
                packet.Substring((protocolMagic + ":SPAWN:").Length));
            if (spawnBroadcastPacket != null) HandleSpawnBroadcast(spawnBroadcastPacket);
            return;
        }

        // --- Despawn ---
        if (packet.StartsWith(protocolMagic + ":DESPAWN:"))
        {
            var despawnPacket = JsonUtility.FromJson<DespawnPacket>(
                packet.Substring((protocolMagic + ":DESPAWN:").Length));
            if (despawnPacket != null) HandleDespawnBroadcast(despawnPacket);
            return;
        }

        // --- Variable Sync ---
        if (packet.StartsWith(protocolMagic + ":VARSYNC:"))
        {
            var variableSyncPacket = JsonUtility.FromJson<VariableSyncPacket>(
                packet.Substring((protocolMagic + ":VARSYNC:").Length));
            if (variableSyncPacket != null && netVarOwners.TryGetValue(variableSyncPacket.ObjectId, out var varOwner))
            {
                foreach (var entry in variableSyncPacket.Vars)
                    varOwner.ApplyRemoteEntry(entry);
            }
            return;
        }

        // --- Vote Kick ---
        if (packet.StartsWith(protocolMagic + ":VOTEKICK:"))
        {
            var voteKickPacket = JsonUtility.FromJson<VoteKickPacket>(
                packet.Substring((protocolMagic + ":VOTEKICK:").Length));
            if (voteKickPacket != null) OnVoteKickReceived?.Invoke(voteKickPacket);
            return;
        }

        // --- Vote Kick Result ---
        if (packet.StartsWith(protocolMagic + ":VOTEKICKRESULT:"))
        {
            var voteKickResultPacket = JsonUtility.FromJson<VoteKickResultPacket>(
                packet.Substring((protocolMagic + ":VOTEKICKRESULT:").Length));
            if (voteKickResultPacket != null) OnVoteKickResultReceived?.Invoke(voteKickResultPacket);
            return;
        }

        // --- Unknown / Game-Specific ---
        // Any packet that does not match a NyNet engine command is forwarded to subscribers.
        // Game-specific code (like FeralKart) subscribes to OnRawPacketReceived to handle
        // things like STATE, MAPVOTE, LOADMAP, and FINISH packets.
        OnRawPacketReceived?.Invoke(packet);
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

    /// <summary>
    /// Notifies the network manager that a world has finished loading.
    /// Call this from your world loader once the scene is ready so queued spawns can be flushed.
    /// </summary>
    public void NotifyWorldReady()
    {
        LogToFile($"[NotifyWorldReady] World ready. Flushing {pendingSpawnQueue.Count} queued spawn(s).");
        worldIsReady = true;
        foreach (var spawnBroadcastPacket in pendingSpawnQueue)
            HandleSpawnBroadcast(spawnBroadcastPacket);
        pendingSpawnQueue.Clear();
    }

    /// <summary>
    /// Notifies the network manager that a world is about to unload.
    /// Call this before loading a new scene so spawn packets do not fire into a scene mid-teardown.
    /// </summary>
    public void NotifyWorldUnloading()
    {
        LogToFile("[NotifyWorldUnloading] World unloading, clearing ready state.");
        worldIsReady = false;
    }
    
    
    public void DespawnAllNetworkObjects()
    {
        LogToFile($"[DespawnAllNetworkObjects] Destroying {netObjects.Count} networked object(s).");
        foreach (var netObject in netObjects)
        {
            if (netObject.Value != null)
            {
                Destroy(netObject.Value);
            }
        }
        netObjects.Clear();
        netTransforms.Clear();
        netVarOwners.Clear();
        pendingSpawnQueue.Clear();
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
        int port = NetProtocol.DefaultPort;
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
            byte[] queryBytes = Encoding.UTF8.GetBytes(protocolMagic + ":QUERY");
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

        string responsePrefix = protocolMagic + ":RESPONSE:";
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
        Disconnect();  // For the love of cats, make sure to disconnect from any connected servers before connecting to a new one!!!!
        LogToFile($"[Connect] Attempting connection to '{address}'.");

        string host = address;
        int port = NetProtocol.DefaultPort;
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
            protocolMagic + ":JOIN:" + localProfile.playerName + ":" + localProfile.sessionToken);

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
                string acceptedPrefix = protocolMagic + ":JOIN:ACCEPTED";

                if (reply.StartsWith(acceptedPrefix))
                {
                    accepted = true;
                    // The token follows the colon after ACCEPTED
                    if (reply.Length > acceptedPrefix.Length + 1)
                        receivedToken = reply.Substring(acceptedPrefix.Length + 1);
                }
                else if (reply.StartsWith(protocolMagic + ":JOIN:REJECTED:"))
                {
                    rejectMessage = reply.Substring((protocolMagic + ":JOIN:REJECTED:").Length);
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

        try
        {
            byte[] leavePacket = Encoding.UTF8.GetBytes(protocolMagic + ":LEAVE");
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

        SendPacket(protocolMagic + ":TSYNC:" + JsonUtility.ToJson(packet));
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

        SendPacket(protocolMagic + ":SPAWNREQ:" + JsonUtility.ToJson(spawnRequest));
    }

    /// <summary>
    /// Asks the server to despawn the networked object with the given ID.
    /// Called by NetSpawner.
    /// </summary>
    public void RequestDespawn(string networkObjectId)
    {
        if (!isConnected) return;
        LogToFile($"[RequestDespawn] networkId={networkObjectId}");
        SendPacket(protocolMagic + ":DESPAWNREQ:" + JsonUtility.ToJson(new DespawnPacket { NetworkObjectId = networkObjectId }));
    }

    /// <summary>
    /// Tells the server this player is ready to start, along with their chosen character or "spectate".
    /// The characterChoice should be a prefab key registered in NetPrefabRegistry, or the string "spectate".
    /// </summary>
    public void SendReady(string characterChoice)
    {
        if (!isConnected) return;
        LogToFile($"[SendReady] Sending READY with choice='{characterChoice}'.");
        SendPacket(protocolMagic + ":READY:" + characterChoice);
    }

    /// <summary>
    /// Sends a chat message to the server. The server stamps the sender name
    /// and relays it to all connected clients.
    /// </summary>
    public void SendChat(string text)
    {
        if (!isConnected || string.IsNullOrWhiteSpace(text)) return;
        SendPacket(protocolMagic + ":CHAT:" + text);
    }

    /// <summary>
    /// Requests a vote kick against the player with the given name.
    /// The server will start a vote if one is not already in progress.
    /// </summary>
    public void RequestVoteKick(string targetName)
    {
        if (!isConnected) return;
        SendPacket(protocolMagic + ":VOTEKICKREQ:" + JsonUtility.ToJson(new VoteKickRequest { TargetName = targetName }));
    }

    /// <summary>
    /// Casts a yes or no vote in an active vote kick.
    /// </summary>
    public void CastVoteKick(bool votedYes)
    {
        if (!isConnected) return;
        SendPacket(protocolMagic + ":VOTEKICKCAST:" + JsonUtility.ToJson(new VoteKickCast { VotedYes = votedYes }));
    }

    /// <summary>
    /// Serializes and sends a raw string packet to the server over UDP.
    /// Use this to send game-specific packets that NyNet does not know about.
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

// Protocol constants shared across NyNet. The magic string must match the server's ProtocolMagic config value.
// Game-specific constants (like vote commands or game state prefixes) should be defined in game-side code.
public static class NetProtocol
{
    public const int DefaultPort = 27015;
    public const float TimeoutSeconds = 3f;
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

// A compact player entry included in player list packets
[Serializable]
public class PlayerNameEntry
{
    public string name;
    public int ping;
}

// The player list packet sent by the server to all clients
[Serializable]
public class PlayerListPacket
{
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