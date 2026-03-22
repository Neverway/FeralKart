//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using RivenFramework;
using UnityEngine;
using UnityEngine.Events;

public class Func_TextEvent : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public bool overrideExistingEvents;
    [Box] public TextEvent textEvent;
    public UnityEvent OnCallFailed = new UnityEvent();


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_TextboxManager textboxManager;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void CallEvent()
    {
        if (textboxManager == null)
        {
            textboxManager = GameInstance.Get<GI_TextboxManager>();
        }

        var result = textboxManager.TryStartTextEvent(textEvent, overrideExistingEvents);
        if (result is false)
        {
            OnCallFailed.Invoke();
        }
    }

    #endregion
}
