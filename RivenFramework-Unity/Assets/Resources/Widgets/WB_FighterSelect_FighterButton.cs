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
using UnityEngine;
using UnityEngine.UI;

public class WB_FighterSelect_FighterButton : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public string characterID;
    [Header("Selection Colorization")]
    public Color normalColor, selectedColor;
    public Image colorizedImage;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public WB_FighterSelect fighterSelect;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void Start()
    {
        GetComponent<Button>().onClick.AddListener(SetNetConnectsSelectedServer);
    }

    private void SetNetConnectsSelectedServer()
    {
        fighterSelect.SelectCharacter(characterID);
        SetVisuallySelected(true);
    }

    public void SetVisuallySelected(bool isSelected)
    {
        colorizedImage.color = isSelected ? selectedColor : normalColor;
    }


    #endregion
}
