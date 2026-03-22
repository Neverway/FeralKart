using RivenFramework;
using RivenFramework.Utils.Reflection;
using System;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

public static class DevMenuAttributes
{
    //Gets all DevMenuItemInfos based on all uses of DevMenuItemAttributes in the project
    public static DevMenuItemInfo[] GetDevMenuItemInfos() =>
        ReflectionCache.GetAttributeUsages<DevMenuItemAttribute>() //Get All AttributeUsages of DevMenuItemAttribute
        .Select(AttributeUsageToItemInfo).ToArray(); //Convert the AttributeUsage array into a DevMenuItemInfo array

    //Converts a single AttributeUsage into a DevMenuItemInfo
    private static DevMenuItemInfo AttributeUsageToItemInfo(AttributeUsage attributeUsage)
        => new DevMenuItemInfo(attributeUsage);

    [DevMenuButton("Toggle Show Logic")]
    public static void ToggleLogicView()
    {
        GameObject player = GameInstance.Get<GI_PawnManager>().localPlayerCharacter;
        player.GetComponentInChildren<Camera>().cullingMask ^= (1 << LayerMask.NameToLayer("<( x )> Hidden Logic"));
    }

    [DevMenuButton("Toggle Show Barriers")]
    public static void ToggleBarriersView()
    {
        GameObject player = GameInstance.Get<GI_PawnManager>().localPlayerCharacter;
        player.GetComponentInChildren<Camera>().cullingMask ^= (1 << LayerMask.NameToLayer("<( x )> Hidden Barriers"));
    }

    [DevMenuButton("Toggle Show Voxel Grid")]
    public static void ToggleVoxelsView()
    {
        GameObject player = GameInstance.Get<GI_PawnManager>().localPlayerCharacter;
        player.GetComponentInChildren<Camera>().cullingMask ^= (1 << LayerMask.NameToLayer("VoxelGrid"));
    }

    [DevMenuButton("Give Debug Items")]
    public static void GiveDebugItems()
    {
        GameObject player = GameInstance.Get<GI_PawnManager>().localPlayerCharacter;
        foreach (var debugItem in GameInstance.Get<GI_DebugItemGiver>().debugItems)
        {
            player.GetComponentInChildren<Pawn_Inventory>().AddItem(debugItem);
        }
    }
}

public struct DevMenuItemInfo
{
    public AttributeUsage attributeInfo;

    public DevMenuItemInfo(AttributeUsage attributeInfo)
    {
        this.attributeInfo = attributeInfo;
    }

}

public abstract class DevMenuItemAttribute : Attribute
{
    public string name = "";
    public string tab = "";
    public string group = "";

    public DevMenuItemAttribute(string name)
    {
        this.name = name;
    }

    public abstract void CheckForInvalidUsageErrors(MemberInfo memberInfo);
}

public class DevMenuButtonAttribute : DevMenuItemAttribute
{
    public DevMenuButtonAttribute(string name) : base(name) { }

    public override void CheckForInvalidUsageErrors(MemberInfo memberAttachedTo)
    {
        if (!(memberAttachedTo is MethodInfo methodInfo))
            throw new InvalidAttributeUsageException<DevMenuButtonAttribute>(memberAttachedTo, 
                "Must be attached to a static method");

        if (!methodInfo.IsStatic)
            throw new InvalidAttributeUsageException<DevMenuButtonAttribute>(memberAttachedTo,
                "Method attached to must be static");

        if (!methodInfo.HasParametersNone())
            throw new InvalidAttributeUsageException<DevMenuButtonAttribute>(memberAttachedTo,
                "Method must have no parameters");
    }
}
/*
public class DevMenuToggleAttribute : DevMenuItemAttribute
{
    public DevMenuButtonAttribute(string name) : base(name) { }
}
public class DevMenuSliderIntAttribute : DevMenuItemAttribute
{
    public int min = 1;
    public int max = 3;
}
public class DevMenuSliderFloatAttribute : DevMenuItemAttribute
{
    public float min = 0f;
    public float max = 1f;
}
public class DevMenuDropdownAttribute : DevMenuItemAttribute
{

}
// */

public class InvalidAttributeUsageException<T> : Exception where T : Attribute
{
    public InvalidAttributeUsageException(MemberInfo member, string missedRequirement)
        : base($"Invalid usage of {typeof(T).Name} on {MemberToName(member)}. {missedRequirement}") { }

    public static string MemberToName(MemberInfo member)
    {
        Type parentType = member.DeclaringType;
        if (parentType == null)
            return member.Name;

        return string.Join(".", parentType.Name, member.CSharpName(ActionDirection.Any));
    }
}