//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class DebugConsole
{
    private static HashSet<object> loggingEnabled = new ();

    [RuntimeInitializeOnLoadMethod]
    private static void Init()
    {
        loggingEnabled = new HashSet<object>();
    }
    
    public static void Log<T>(this T source, string message)
    {
        if (!loggingEnabled.Contains(source))
        {
            if (source is not ILoggable loggable || !loggable.EnableRuntimeLogging) return;
        }

        Debug.Log($"[{source.GetType()}] {message}");
    }
    
    public static void EnableLogging<T>(this T source)
    {
        loggingEnabled.Add(source);
    }
    
    public static void DisableLogging<T>(this T source)
    {
        loggingEnabled.Remove(source);
    }
}
