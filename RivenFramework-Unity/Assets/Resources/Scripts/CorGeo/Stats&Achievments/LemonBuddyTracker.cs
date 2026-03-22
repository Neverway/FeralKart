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

public class LemonBuddyTracker : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public bool clearBuddiesOnAwake = false;
    public static List<int> scenesBuddyWasDestroyedIn = new List<int>();

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    public void Awake()
    {
        if (clearBuddiesOnAwake)
            scenesBuddyWasDestroyedIn = new List<int>();
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    public void OnBuddyDestroyed()
    {
        int newSceneIndex = gameObject.scene.buildIndex;
        if (!scenesBuddyWasDestroyedIn.Contains(newSceneIndex))
        {
            scenesBuddyWasDestroyedIn.Add(newSceneIndex);
            EndOfDemoStatsTracker.instance.UpdateBuddies(scenesBuddyWasDestroyedIn.Count);
        }
    }

    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    //=----Reload Static Fields----=
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeStaticFields()
    {
        scenesBuddyWasDestroyedIn = new List<int>();
    }


    #endregion
}
