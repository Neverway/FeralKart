using ErryLib.ExtensionMethods;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(UnboxAttribute))]
[CustomPropertyDrawer(typeof(BoxAttribute))]
public class BoxDrawer : PropertyDrawer
{
    public static Stack<VisualElement> highlightStack = new Stack<VisualElement>();
    public static List<string> collapsed = new List<string>();
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        //Setup unique property ID
        string propertyID = "";
        try
        {
            propertyID += property.serializedObject.targetObject.GetHashCode();
            propertyID += property.boxedValue.GetHashCode();
            propertyID += property.propertyPath;
        }
        catch { propertyID = ""; }

        BoxAttribute attributeInfo = (BoxAttribute)attribute;
        // Root container, with label at the top
        var fullDrawer = GetContainer(attributeInfo);
        VisualElement divider = null;
        var title = GetPropertyLabel(property);
        var contents = new VisualElement();
        if (attributeInfo.label)
        {
            if (!attributeInfo.box)
            {
                Color unhighlight = new Color(0.4f, 0.4f, 0.4f);
                Color highlight = new Color(0.8f, 0.8f, 0.8f);
                contents.style.borderLeftColor = unhighlight;
                contents.style.borderTopColor = unhighlight;
                contents.style.borderLeftWidth = 1;
                contents.style.borderTopWidth = 1;
                contents.style.marginLeft = 2;
                contents.style.transitionProperty = new List<StylePropertyName> { "border-color" };
                contents.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f) };
                contents.OnMouseEnter(_ =>
                {
                    contents.style.borderLeftColor = highlight;
                    contents.style.borderTopColor = highlight;
                });
                contents.OnMouseLeave(_ =>
                {
                    contents.style.borderLeftColor = unhighlight;
                    contents.style.borderTopColor = unhighlight;
                });
            }
            else
                divider = contents.AddDivider();

            fullDrawer.Add(title);
        }
        fullDrawer.Add(contents);
        
        if (attributeInfo.foldout)
        {
            contents.SetActive(string.IsNullOrEmpty(propertyID) || !collapsed.Contains(propertyID));
            title.OnMouseDown(_ =>
            {
                if (highlightStack.TryPeek(out VisualElement topElement) && topElement == fullDrawer)
                {
                    if (string.IsNullOrEmpty(propertyID))
                    {
                        contents.ToggleActive();
                        return;
                    }

                    if (collapsed.Contains(propertyID))
                    {
                        collapsed.Remove(propertyID);
                        contents.SetActive(true);
                    }
                    else
                    {
                        collapsed.Add(propertyID);
                        contents.SetActive(false);
                    }
                }
            });
        }
        
        

        var iterator = property.Copy();
        var end = iterator.GetEndProperty();
        if (iterator.NextVisible(true))
        {
            do
            {
                if (SerializedProperty.EqualContents(iterator, end))
                    break;

                var field = new PropertyField(iterator);

                //if (iterator.isArray && iterator.propertyType != SerializedPropertyType.String)
                //    field.style.marginLeft = 100;

                field.style.SetMargin(1);
                contents.Add(field);
            }
            while (iterator.NextVisible(false));
        }
        bool hasDivider = divider != null;
        if (contents.childCount == (hasDivider ? 1 : 0))
        {
            contents.SetActive(false);
            if (hasDivider) divider.SetActive(false);
            if (attributeInfo.label)
            {
                title.style.unityFontStyleAndWeight = FontStyle.Normal;
                title.style.paddingLeft = 2;
                if (!attributeInfo.box)
                {
                    title.style.height = EditorGUIUtility.singleLineHeight;
                    title.style.maxHeight = EditorGUIUtility.singleLineHeight;
                    title.style.minHeight = EditorGUIUtility.singleLineHeight;
                    title.style.marginTop = 0;
                    title.style.marginBottom = 0;
                    title.style.paddingTop = 0;
                    title.style.paddingBottom = 0;
                }
            }
            if (!attributeInfo.box)
            {
                fullDrawer.style.height = EditorGUIUtility.singleLineHeight;
                fullDrawer.style.maxHeight = EditorGUIUtility.singleLineHeight;
                fullDrawer.style.minHeight = EditorGUIUtility.singleLineHeight;
                fullDrawer.style.marginTop = 0;
                fullDrawer.style.marginBottom = 0;
                fullDrawer.style.paddingTop = 0;
                fullDrawer.style.paddingBottom = 0;
            }
        }

        return fullDrawer;
    }
    public VisualElement GetContainer(BoxAttribute attributeInfo)
    {
        var container = new VisualElement();

        //Define transition data
        container.style.transitionProperty = new List<StylePropertyName> { "background-color", "border-color" };
        container.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f) };
        //Assign style changes to mouse enter/leave events
        
        if (attributeInfo.box)
        {
            container.style.SetPadding(4);
            container.style.marginBottom = 1;
            container.style.marginTop = 1;
            container.style.SetBorderWidth(1);
            container.style.SetBorderRadius(4);
            container.OnMouseEnter(e => SetContainerHighlight(container, true));
            container.OnMouseLeave(e => SetContainerHighlight(container, false));
            SetContainerHighlight(container, null);
        }
        else
        {
            if (attributeInfo.label)
                container.style.marginTop = 4;
        }

        //Set values for box not being highlighted at first
        
        return container;
    }
    public void SetContainerHighlight(VisualElement box, bool? entered)
    {
        if (entered != null)
        {
            if (entered.Value)
            {
                highlightStack.TryPeek(out VisualElement oldBox);
                highlightStack.Push(box);
                if (oldBox != null) SetContainerHighlight(oldBox, null);
            }
            else
                highlightStack.TryPop(out VisualElement myself);

            foreach(VisualElement ve in highlightStack)
                SetContainerHighlight(ve, null);

        }
        bool highlight = highlightStack.Count != 0 && highlightStack.Peek() == box;
        bool subhighlight = highlightStack.Contains(box);

        float g = (highlight ? 0.325f : 0.25f);
        box.style.backgroundColor = new Color(g, g, g);
        g = (highlight ? 0.85f : (subhighlight ? 0.6f : 0f));
        box.style.SetBorderColor(new Color (g, g, g));

        box.MarkDirtyRepaint();
    }
    public VisualElement GetPropertyLabel(SerializedProperty property)
    {
        bool isPolymorphic = fieldInfo.HasAttribute<PolymorphicAttribute>();
        if (isPolymorphic)
        {
            VisualElement polymorphicTitle = new VisualElement();
            polymorphicTitle.style.marginBottom = 3;
            polymorphicTitle.style.flexDirection = FlexDirection.Row;
            var label = new Label(property.displayName);
            label.style.flexShrink = 0;
            label.style.flexGrow = 1;
            label.style.minWidth = 80; 
            label.style.maxWidth = 250;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            polymorphicTitle.Add(label);
            var prop = new PropertyField(property);
            prop.style.flexGrow = 5;
            prop.style.flexShrink = 1;
            prop.style.maxWidth = Length.Percent(75);
            prop.style.flexBasis = StyleKeyword.Auto;
            prop.style.height = 18;
            prop.style.top = -2;
            polymorphicTitle.Add(prop);

            return polymorphicTitle;
        }
        return new Label(property.displayName)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 1,
                unityTextAlign = TextAnchor.MiddleLeft
            }
        };
    }

    public VisualElement GetFoldout(SerializedProperty property)
    {
        throw new System.Exception();
    }
}