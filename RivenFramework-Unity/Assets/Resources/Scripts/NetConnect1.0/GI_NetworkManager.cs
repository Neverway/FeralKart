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
