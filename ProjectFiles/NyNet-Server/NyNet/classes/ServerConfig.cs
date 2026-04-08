using System;
using System.Collections.Generic;
using System.Net;

// Configuration loaded from server.config at startup
public class ServerConfig
{
    public string ProtocolMagic { get; set; } = "NyNet";
    public string ServerName { get; set; } = "NyNet Server";
    public int Port { get; set; } = 27015;
    public int MaxPlayers { get; set; } = 16;
    public string IconPath { get; set; } = "";
    public bool AllowChatCommands { get; set; } = true;
    public float VoteKickDuration { get; set; } = 30f;
    public float VoteKickThreshold { get; set; } = 0.6f;
    public float VoteKickRepeatDelay { get; set; } = 120f;
    public float KickRejoinCooldown { get; set; } = 120f;
    public bool AnnounceJoin { get; set; } = true;
    public bool AnnounceLeave { get; set; } = true;
    public bool AnnounceKick { get; set; } = true;
    public bool AnnounceBan { get; set; } = true;
}
