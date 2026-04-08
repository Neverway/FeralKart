using System;
using System.Collections.Generic;
using System.Net;

// Full game state snapshot broadcast to all clients
public class GameStatePacket
{
    public string Phase { get; set; } = "Intermission";
    public string MapName { get; set; } = "";
    public string GameMode { get; set; } = "";
    public int TimeLeft { get; set; } = 30;
    public List<PlayerNameEntry> PlayerNames { get; set; } = new();
}

// Compact player entry sent inside STATE packets
public class PlayerNameEntry
{
    public string name { get; set; } = "";
    public int ping { get; set; } = 0;
}
