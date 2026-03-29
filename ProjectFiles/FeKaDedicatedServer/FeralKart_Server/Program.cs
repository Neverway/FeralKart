using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

int QUERY_PORT = 27015;
string ICON_PATH = "";
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--port" && int.TryParse(args[i + 1], out int customPort))
        QUERY_PORT = customPort;
    else if (args[i] == "--icon")
        ICON_PATH = args[i + 1];
}

const string PROTOCOL_MAGIC = "FeKa";

// Shared server state
var state = new ServerState
{
    ServerName = "Feral Kart Server",
    MapName = "None",
    GameMode = "None",
    MaxPlayers = 16,
    PlayerCount = 0,
    IconBase64 = ""
};

if (!string.IsNullOrEmpty(ICON_PATH) && File.Exists(ICON_PATH))
{
    state.IconBase64 = Convert.ToBase64String(File.ReadAllBytes(ICON_PATH));
    Console.WriteLine($"Loaded icon from {ICON_PATH}");
}

// Logging
void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

// UDP query listener
var querySocket = new UdpClient(new IPEndPoint(IPAddress.Any, QUERY_PORT));
Log($"Query listener on UDP port {QUERY_PORT}");

new Thread(() =>
{
    while (true)
    {
        try
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            string message = Encoding.UTF8.GetString(querySocket.Receive(ref remote));
            
            // Ignore packets that aren't ours
            if (!message.StartsWith(PROTOCOL_MAGIC + ":QUERY")) continue;

            var payload = new ServerQueryResponse
            {
                ServerName = state.ServerName,
                MapName = state.MapName,
                GameMode = state.GameMode,
                MaxPlayers = state.MaxPlayers,
                PlayerCount = state.PlayerCount,
                IconBase64 = state.IconBase64,
            };

            byte[] reply = Encoding.UTF8.GetBytes(
                PROTOCOL_MAGIC + ":RESPONSE:" + JsonSerializer.Serialize(payload));
            
            querySocket.Send(reply, reply.Length, remote);
            Log($"Query answered: {remote.Address}");
        }
        catch (Exception e)
        {
            Log($"ERR: {e.Message}");
        }
    }
}) { IsBackground = true }.Start();

// Command loop
Log("Server ready! Type 'help' for a list of available commands.");

while (true)
{
    Console.Write("==> ");
    string? raw = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(raw)) continue;

    string[] parts = raw.Split(' ', 2);
    string cmd = parts[0].ToLower();
    string arg = parts.Length > 1 ? parts[1] : "";

    switch (cmd)
    {
        case "help":
            Console.WriteLine("  status                     show the current server info");
            Console.WriteLine("  setname       <text>       set the servers name");
            Console.WriteLine("  setmap        <text>       set the current map");
            Console.WriteLine("  setgamemode   <text>       set the current gamemode");
            Console.WriteLine("  setmaxplayers <n>          set the max player count");
            Console.WriteLine("  seticon       <base64>     set the server icon (paste in a base64-encoded PNG)");
            Console.WriteLine("  quit                       shut down the server");
            break;
        
        case "status":
            Console.WriteLine($"  Name:         {state.ServerName}");
            Console.WriteLine($"  Map:          {state.MapName}");
            Console.WriteLine($"  GameMode:     {state.GameMode}");
            Console.WriteLine($"  Players:      {state.PlayerCount}/{state.MaxPlayers}");
            Console.WriteLine($"  Icon:         {(string.IsNullOrEmpty(state.IconBase64))}");
            break;
        
        case "setname": state.ServerName = arg; Log($"Set server name to '{arg}'"); break;
        case "setmap": state.MapName = arg; Log($"Set current map to '{arg}'"); break;
        case "setgamemode": state.GameMode = arg; Log($"Set current game mode to '{arg}'"); break;
        case "seticon": state.IconBase64 = arg; Log($"Updated server icon"); break;
        
        case "setmaxplayers":
            if (int.TryParse(arg, out int n))
            {
                state.MaxPlayers = n;
                Log($"Updated max player count to '{n}'");
            }
            else
            {
                Console.WriteLine($"Couldn't interpert '{arg}' as number");
                Console.WriteLine($"Usage: Setmaxplayers <number>");
            }
            break;
        
        case "quit":
            Log("Shutting down...");
            querySocket.Close();
            return ;
        
        default:
            Console.WriteLine($"Unknown command '{cmd}'. Type 'help' for a list of available commands.");
            break;
    }
}

// My super cool data classes
class ServerState
{
    public string ServerName { get; set; } = "";
    public string MapName { get; set; } = "";
    public string GameMode { get; set; } = "";
    public int MaxPlayers { get; set; } = 16;
    public int PlayerCount { get; set; } = 0;
    public string IconBase64 { get; set; } = "";
}

class ServerQueryResponse
{
    public string ServerName { get; set; } = "";
    public string MapName { get; set; } = "";
    public string GameMode { get; set; } = "";
    public int MaxPlayers { get; set; } = 16;
    public int PlayerCount { get; set; } = 0;
    public string IconBase64 { get; set; } = "";
}