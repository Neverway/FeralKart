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
using UnityEngine;

public class GI_NetworkManager : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private string configFilePath => Path.Combine(Application.persistentDataPath, "serverlist.config");
    

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [SerializeField] public List<ServerEntry> serverEntries = new List<ServerEntry>();


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void SaveServerConfigFile()
    {
        var wrapper = new ServerEntryListWrapper { servers = serverEntries };
        string json = JsonUtility.ToJson(wrapper, prettyPrint:true);
        File.WriteAllText(configFilePath, json);
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


    #endregion
}

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

public static class NetProtocol
{
    // This should match the data that I set in the dedicated server's program.cs, otherwise the server won't talk
    public const string Magic = "FeKa";
    public const int QueryPort = 27015;
    public const float TimeoutSeconds = 3f;
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