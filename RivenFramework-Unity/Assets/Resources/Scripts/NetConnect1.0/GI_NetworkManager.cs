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


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public NetProfile localProfile = new NetProfile();
    public bool isConnected  = false;
    public string connectedAddress = "";
    public string currentPhase = null;
    public string lastKnownMapName = "";


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private string configFilePath => Path.Combine(Application.persistentDataPath, "serverlist.config");
    private string profileFilePath => Path.Combine(Application.persistentDataPath, "netprofile.profile");
    private UdpClient       _gameUdp;
    private IPEndPoint      _serverEndpoint;
    private Coroutine       _heartbeatCoroutine;
    private Coroutine       _listenCoroutine;
    private bool _receivedFirstState = false;
    

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [SerializeField] public List<ServerEntry> serverEntries = new List<ServerEntry>();
    public event Action<GameStatePacket>      OnGameStateReceived;
    public event Action<string>               OnMapVoteReceived;
    public event Action<string>               OnKicked;
    public event Action                       OnDisconnected;
    public GameObject fighterSelectWidget, connectionMessageWidget;
    [SerializeField] public string titleScreenWorldName = "_Title";


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Awake()
    {
        OnKicked      += HandleKicked;
        OnDisconnected += HandleDisconnected;
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
        var widgetManager = FindObjectOfType<GI_WidgetManager>();
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

        onSuccess?.Invoke();
    }

    public void Disconnect()
    {        
        if (!isConnected) return;
 
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
                lastKnownMapName = gs.MapName;

                //Debug.Log($"[NetworkManager] STATE received. previousPhase='{currentPhase}' newPhase='{gs?.Phase}' map='{gs?.MapName}'");
                
                string previousPhase = currentPhase;
                currentPhase = gs.Phase;

                // First state packet after connecting, load the servers current map
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
                        worldLoader.LoadWorld(gs.MapName);
                    }
                }
                else
                {
                    // Ongoing: server moved to Loading, load the new map
                    if (gs.Phase == "Loading" && previousPhase != "Loading")
                    {
                        var worldLoader = GameInstance.Get<GI_WorldLoader>();
                        if (worldLoader != null) worldLoader.LoadWorld(gs.MapName);
                    }
                    // Ongoing: game ended and we're back in intermission
                    else if (gs.Phase == "Intermission" && previousPhase == "InProgress")
                    {
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
            else if (packet.StartsWith(NetProtocol.Magic + ":PING"))
            {
                byte[] pong = Encoding.UTF8.GetBytes(NetProtocol.Magic + ":PONG");
                try { _gameUdp?.Send(pong, pong.Length, _serverEndpoint); }
                catch { }
            }
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }
    
    public void SendReady()
    {
        if (!isConnected) return;
        byte[] data = Encoding.UTF8.GetBytes(NetProtocol.Magic + NetProtocol.Ready);
        try { _gameUdp?.Send(data, data.Length, _serverEndpoint); }
        catch { }
    }

    public void SendVote(string mapName)
    {
        if (!isConnected) return;
        byte[] data = Encoding.UTF8.GetBytes(NetProtocol.Magic + NetProtocol.Vote + mapName);
        try { _gameUdp?.Send(data, data.Length, _serverEndpoint); }
        catch { }
    }

    public void SendSpectate()
    {
        if (!isConnected) return;
        byte[] data = Encoding.UTF8.GetBytes(NetProtocol.Magic + NetProtocol.Spectate);
        try { _gameUdp?.Send(data, data.Length, _serverEndpoint); }
        catch { }
    }
    
    private IEnumerator ShowFighterSelect()
    {
        yield return null;
        var widgetManager = GameInstance.Get<GI_WidgetManager>();
        if (widgetManager != null && fighterSelectWidget != null)
        {
            widgetManager.AddWidget(fighterSelectWidget);
        }
        else
        {
            Debug.Log("No fighter select");
        }
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
    public const string Pong = ":PONG";

    // Incoming
    public const string State    = ":STATE:";
    public const string MapVote  = ":MAPVOTE:";
    public const string LoadMap  = ":LOADMAP:";
    public const string Spawn    = ":SPAWN:";
    public const string Ping = ":PING";
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