using System;
using System.Collections.Generic;
using System.Net;

// Live runtime state visible to all threads
public class ServerState
{
    public string ServerName { get; set; } = "";
    public int MaxPlayers { get; set; } = 16;
    public int PlayerCount { get; set; } = 0;
    public string IconBase64 { get; set; } = "";
}
