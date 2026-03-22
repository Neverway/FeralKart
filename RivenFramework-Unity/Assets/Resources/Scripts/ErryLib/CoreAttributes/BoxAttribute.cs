using UnityEngine;

[AttributeNeedsPolymorphicDrawerToIgnoreContentsButDrawDropdown]
public class BoxAttribute : PropertyAttribute
{
    public bool label = true;
    public bool box = true;
    public bool foldout = true;

    public BoxAttribute() { }
}

[AttributeNeedsPolymorphicDrawerToIgnoreContentsButDrawDropdown]
public class UnboxAttribute : BoxAttribute
{
    public UnboxAttribute() 
    {
        box = false;
    }
}