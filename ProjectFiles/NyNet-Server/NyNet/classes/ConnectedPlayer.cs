using System;
using System.Collections.Generic;
using System.Net;

// A player currently connected to the server
public class ConnectedPlayer
{
    public string Name { get; set; } = "Player";
    public IPEndPoint? EndPoint { get; set; } = null;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastPingSent { get; set; } = DateTime.UtcNow;
    public int LastPingMs { get; set; } = 0;
    public string SessionToken { get; set; } = "";
    public bool IsOp { get; set; } = false;
}
