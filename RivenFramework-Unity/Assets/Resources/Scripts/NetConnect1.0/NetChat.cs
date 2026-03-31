using System;
using RivenFramework;
using UnityEngine;

/// <summary>
/// Convenience wrapper for sending chat messages.
/// </summary>
public static class NetChat
{
    /// <summary>
    /// Sends a chat message to the server, which will stamp your player name and
    /// relay it to every connected client via the OnChatReceived event.
    /// </summary>
    /// <param name="text">The message to send. Clamped to 200 characters server-side.</param>
    public static void Send(string text)
    {
        var nm = GameInstance.Get<GI_NetworkManager>();
        if (nm == null)
        {
            Debug.LogError("[NetChat] GI_NetworkManager not found.");
            return;
        }
        nm.SendChat(text);
    }

    /// <summary>
    /// Subscribes a callback to receive chat messages from the server.
    /// The callback receives (senderName, messageText).
    /// Remember to unsubscribe in OnDestroy to avoid stale references.
    /// </summary>
    public static void Subscribe(Action<string, string> callback)
    {
        var nm = GameInstance.Get<GI_NetworkManager>();
        if (nm != null) nm.OnChatReceived += callback;
    }

    /// <summary>Unsubscribes a previously registered chat callback.</summary>
    public static void Unsubscribe(Action<string, string> callback)
    {
        var nm = GameInstance.Get<GI_NetworkManager>();
        if (nm != null) nm.OnChatReceived -= callback;
    }
}