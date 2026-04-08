using System;
using System.Collections.Generic;
using System.Net;

// Configuration loaded from server.config at startup
public class FeKa_ServerConfig
{
    public string GameMode { get; set; } = "Race";
    public List<string> MapPool { get; set; } = new();
    public int IntermissionDuration { get; set; } = 30;
}
