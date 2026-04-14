//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class WB_FighterSelect_FighterButton : MonoBehaviour
{
    #region========================================( Variables )======================================================//

    /*-----[ Inspector Variables ]---------------------------------------------------------------------------------*/
    public string characterID;
    [Header("Selection Colorization")]
    public Color normalColor, selectedColor;
    public Image colorizedImage;


    /*-----[ Reference Variables ]---------------------------------------------------------------------------------*/
    public WB_FighterSelect fighterSelect;


    #endregion


    #region=======================================( Functions )=======================================================//

    /*-----[ Mono Functions ]--------------------------------------------------------------------------------------*/

    public void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnButtonClicked);
    }


    /*-----[ Internal Functions ]----------------------------------------------------------------------------------*/

    public void OnButtonClicked()
    {
        fighterSelect.SelectCharacter(characterID);
        if (string.IsNullOrEmpty(characterID) || characterID == "Random") return;
        SetVisuallySelected(true);
    }


    /*-----[ External Functions ]----------------------------------------------------------------------------------*/

    public void SetVisuallySelected(bool isSelected)
    {
        colorizedImage.color = isSelected ? selectedColor : normalColor;
    }


    #endregion
}