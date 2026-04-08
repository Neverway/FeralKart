//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
// Information
//      Feral Kart specific network packet types used on the Unity client side.
//      These mirror the packet classes defined in the FeKa dedicated server project.
//      Subscribe to GI_NetworkManager.OnRawPacketReceived and check for the FeKa: prefix
//      to receive and deserialize these packets.
//
//====================================================================================================================//

using System;
using System.Collections.Generic;
using UnityEngine;

// The full game state snapshot broadcast by the Feral Kart server during intermission and loading.
// Received as: FeKa:STATE:<json>
[Serializable]
public class FeKa_GameStatePacket
{
    public string Phase;
    public string MapName;
    public string GameMode;
    public int TimeLeft;
    public List<PlayerNameEntry> PlayerNames;
}

// Broadcast by the Feral Kart server to all clients at the end of a race.
// Received as: FeKa:RACERESULTS:<json>
[Serializable]
public class FeKa_RaceResultsPacket
{
    public List<FeKa_RaceResultEntry> Results;
}

// A single player's result entry inside FeKa_RaceResultsPacket
[Serializable]
public class FeKa_RaceResultEntry
{
    public string PlayerName;
    public bool Failed;
    public int Placement;
    public float FinishTime;
    public float HealthRemaining;
    public int LivesRemaining;
    public int Kills;
    public float DamageTaken;
    public float DamageDealt;
    public float DamageHealed;
}

// Sent by this client to report race completion or failure to the Feral Kart server.
// Sent as: FeKa:FINISH:<json>
[Serializable]
public class FeKa_FinishPacket
{
    public bool Failed;
    public int Placement;
    public float FinishTime;
    public float HealthRemaining;
    public int LivesRemaining;
    public int Kills;
    public float DamageTaken;
    public float DamageDealt;
    public float DamageHealed;
}