//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using UnityEngine;

public class Logic_Fizzler : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public LogicInput<bool> inputClearRift = new(false);
    public LogicInput<bool> inputDisableFizzler = new(false);

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public GameObject fizzlerObjectToDisable;
    private Item_Utility_Geogun geogun;

    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        if (inputClearRift.HasLogicOutputSource) inputClearRift.CallOnSourceChanged(InputClearRiftChanged);
        if (inputDisableFizzler.HasLogicOutputSource) inputDisableFizzler.CallOnSourceChanged(InputDisableFizzlerChanged);
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void InputClearRiftChanged()
    {
        if (inputClearRift.Get()) ClearGeogunRifts();
    }
    
    private void InputDisableFizzlerChanged()
    {
        fizzlerObjectToDisable.SetActive(!inputDisableFizzler.Get());
    }
    
    private void ClearGeogunRifts()
    {
        if (geogun == null)
        {
            geogun = FindObjectOfType<Item_Utility_Geogun>();
            if (geogun == null) return;
        }
        geogun.DestroyMarkers();
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/

    #endregion
}
