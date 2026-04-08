using System;
using System.Collections.Generic;
using System.Net;

// Payload returned in response to a QUERY packet.
// Kept separate from ServerState so we can control exactly what the server browser sees.
public class ServerQueryResponse
{
    public string ServerName { get; set; } = "";
    public int MaxPlayers { get; set; } = 16;
    public int PlayerCount { get; set; } = 0;
    public string IconBase64 { get; set; } = "";

    public string MapName { get; set; } = "";
    public string GameMode { get; set; } = "";
}
