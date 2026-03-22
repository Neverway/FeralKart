using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public class DevMenuItem_Button : MonoBehaviour
{
    public TextMeshProUGUI buttonText;

    private DevMenuItemInfo menuItemInfo;

    public void SetupInfo(DevMenuItemInfo menuItemInfo)
    {
        this.menuItemInfo = menuItemInfo;
        DevMenuButtonAttribute buttonAttribute = menuItemInfo.attributeInfo.Attribute as DevMenuButtonAttribute;
        buttonText.text = buttonAttribute.name;
    }


    public void OnButtonPress()
    {
        if (menuItemInfo.attributeInfo.Member is MethodInfo methodInfo)
        {
            if (methodInfo.IsStatic && methodInfo.GetParameters().Length == 0)
                methodInfo.Invoke(null, null);
        }
    }
}
