//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WidgetSelectable_TMPText : WidgetSelectable
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("")]
    public bool useTextIndicators=true;
    [Tooltip("Text that is added before the main string content to indicate that it is currently selected")]
    public string selectedIndicator = ">";
    [Tooltip("Text that is added before the main string content when this element is not selected (Mainly for creating even spacing)")]
    public string unselectedIndicator = " ";
    [Tooltip("")]
    public bool useColorIndicators=true;
    [Tooltip("The color the text is changed to when selected")]
    public Color selectedColor = new Color(1,1,1,1);
    [Tooltip("The color the text is changed to when not selected")]
    public Color unselectedColor = new Color(1,1,1,0.25f);


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    [HideInInspector] public bool initialized;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("A variable to hold the text content before we add or remove anything from it using the selection indicators")]
    public string originalTextContent;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [SerializeField] private TextMeshProUGUI text;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Awake()
    {
        GetInitValues();
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public override void SetSelected(bool _isSelected)
    {
        if (!initialized) GetInitValues();
        
        base.SetSelected(_isSelected);
        
        // Remember about ternary conditional operators, "Condition ? true : false;" ~Liz
        if (useTextIndicators) text.text = _isSelected ? (selectedIndicator + originalTextContent) : (unselectedIndicator + originalTextContent);
        if (useColorIndicators) text.color =  _isSelected ? selectedColor : unselectedColor;
    }

    public void GetInitValues()
    {
        if (text == null) { text = GetComponent<TextMeshProUGUI>(); }
        
        originalTextContent = text.text;
        initialized = true;
    }

    public void SetText(string _text)
    {
        // Puts text in the text text text 
        // TEXT :3
        originalTextContent = _text;
        text.text = _text;
    }


    #endregion
}
