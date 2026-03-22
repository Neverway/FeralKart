using ErryLib.Reflection;
using System;
using System.Reflection;
using UnityEngine;

public class ReloadAttribute : Attribute
{
    object Value = null;
    bool UseDefault = true;

    public ReloadAttribute() 
    {
        UseDefault = true;
    }
    public ReloadAttribute(object value) 
    { 
        Value = value; 
        UseDefault = false; 
    }
    public bool ReloadValueTo(MemberInfo member)
    {
        if (UseDefault)
            return member.TryAssignDefault();
        else
            return member.TryAssign(Value);
    }

    [InvokeOnReflectionCacheLoad]
    public static void ReloadAllValues()
    {
        var attributes = ReflectionCache.GetAttributeUsageInfos<ReloadAttribute>();
        foreach (var attribute in attributes)
        {
            var success = attribute.As<ReloadAttribute>().ReloadValueTo(attribute.Member);
            if (!success)
                Debug.LogWarning($"[Reload] attribute was applied incorrectly to " +
                    $"{attribute.Member.Name}");
        }
    }
}
