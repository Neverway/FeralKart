using UnityEngine;

public class DebugDisplayListAsStringsAttribute : PropertyAttribute 
{
    public string FieldName;
    public bool Hide;
    public DebugDisplayListAsStringsAttribute(string fieldName, bool hide = false)
    {
        FieldName = fieldName;
        Hide = hide;
    }
}