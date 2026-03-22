
using ErryLib.Reflection;
using System;
using System.Collections;
using System.Reflection;

public static class EnumerableExtensions
{
    public static void Test()
    {

    }

    /// <summary> true if this collection is empty (does not check for null) </summary>
    public static bool IsEmpty(this ICollection collection) => collection.Count == 0;
    /// <summary> true if this collection contains at least 1 item (does not check for null) </summary>
    public static bool IsNotEmpty(this ICollection collection) => collection.Count > 0;
    /// <summary> true if this collection is empty or null </summary>
    public static bool IsEmptyOrNull(this ICollection collection) => collection == null || collection.Count == 0;
    /// <summary> true if this collection contains at least 1 item and is not null </summary>
    public static bool IsNotEmptyOrNull(this ICollection collection) => collection != null && collection.Count > 0;
    /// <summary> true if <paramref name="index"/> is valid for this collection (0 &lt;= <paramref name="index"/> &lt; size) </summary>
    public static bool IsIndexInRange(this ICollection collection, int index) => index >= 0 && index < collection.Count;
    /// <summary> true if <paramref name="index"/> is out of range for this collection </summary>
    public static bool IsIndexOutOfRange(this ICollection collection, int index) => index < 0 || index >= collection.Count;
}