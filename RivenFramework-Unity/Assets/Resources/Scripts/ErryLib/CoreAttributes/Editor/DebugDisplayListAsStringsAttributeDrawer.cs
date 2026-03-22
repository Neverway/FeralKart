using ErryLib.ExtensionMethods;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(DebugDisplayListAsStringsAttribute), useForChildren: false)]
public class DebugDisplayListAsStringsAttributeDrawer : PropertyDrawer
{
    VisualElement listContainer = new VisualElement();
    DebugDisplayListAsStringsAttribute attr;
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        base.OnGUI(position, property, label);
        RefreshList(property, attr);
    }
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        attr = (DebugDisplayListAsStringsAttribute)attribute;
        var root = new VisualElement();
        var label = new Label(attr.FieldName)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 2
            }
        };

        root.Add(label);
        root.AddDivider();
        root.Add(listContainer);
        if (!attr.Hide)
            root.Add(new PropertyField(property));

        var scheduled = root.schedule.Execute(() => RefreshList(property, attr)).Every(50);

        return root;
    }

    public void RefreshList(SerializedProperty property, DebugDisplayListAsStringsAttribute attribute)
    {
        listContainer.Clear();
        if (attr == null)
            return;

        var target = property.serializedObject.targetObject;
        var field = target.GetType().GetField(attribute.FieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null)
        {
            listContainer.Add(new Label($"Field '{attribute.FieldName}' not found"));
            return;
        }

        if (field.GetValue(target) is not IEnumerable enumerable)
        {
            listContainer.Add(new Label($"'{attribute.FieldName}' is not a list or array"));
            return;
        }

        foreach (var item in enumerable)
        {
            VisualElement ve = new Label($"{item}");
            ve.style.SetMargin(1);
            ve.style.SetPadding(2);
            ve.style.SetBorderWidth(1);
            ve.style.SetBorderColor(new Color(0.4f, 0.4f, 0.4f));
            ve.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            listContainer.Add(ve);
        }
    }
}