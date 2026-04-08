using System;
using System.Collections.Generic;
using System.Net;

// Sent by a client to request a vote kick against another player
public class VoteKickRequest
{
    public string TargetName { get; set; } = "";
}

// Broadcast by the server to all clients when a vote kick starts
public class VoteKickPacket
{
    public string TargetName { get; set; } = "";
    public int TimeSeconds { get; set; }
}

// Sent by a client to cast their vote in an active vote kick
public class VoteKickCast
{
    public bool VotedYes { get; set; }
}

// Broadcast by the server to all clients when a vote kick resolves
public class VoteKickResult
{
    public string TargetName { get; set; } = "";
    public bool Passed { get; set; }
}

// Tracks the state of an active vote kick on the server
public class ActiveVoteKick
{
    public string TargetToken { get; set; } = "";
    public string TargetName { get; set; } = "";
    public HashSet<string> VotedYes { get; set; } = new();
    public HashSet<string> VotedNo { get; set; } = new();
    public DateTime Deadline { get; set; }
}
