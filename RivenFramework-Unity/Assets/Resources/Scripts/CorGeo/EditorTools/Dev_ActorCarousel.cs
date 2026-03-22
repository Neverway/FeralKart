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
using TMPro;
using UnityEngine;

public class Dev_ActorCarousel : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public int currentIndex;
    public LogicInput<bool> Last = new(false);
    public LogicInput<bool> Next = new(false);

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [SerializeField] private Transform previewLocation;
    [SerializeField] private TMP_Text actorId;
    [SerializeField] private TMP_Text actorName;


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        Last.CallOnSourceChanged(LastActor);
        Next.CallOnSourceChanged(NextActor);
        UpdateActiveActors();
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void UpdateActiveActors()
    {
        for (int i = 0; i <= transform.childCount; i++)
        {
            // Enable target
            var targetActor = transform.GetChild(currentIndex).gameObject.GetComponent<Actor>();
            targetActor.gameObject.SetActive(true);
            
            // Reset their location in case the user moved them
            targetActor.transform.position = previewLocation.position;
            targetActor.transform.rotation = previewLocation.rotation;
            
            // Set display text
            actorId.text = targetActor.id;
            actorName.text = targetActor.displayName;
            
            // Make sure previously active actor is disabled
            if (currentIndex-1 >= 0) transform.GetChild(currentIndex-1).gameObject.SetActive(false);
            else transform.GetChild(transform.childCount-1).gameObject.SetActive(false);
            
            if (currentIndex+1 <= transform.childCount-1) transform.GetChild(currentIndex+1).gameObject.SetActive(false);
            else transform.GetChild(0).gameObject.SetActive(false);
        }
    }
    
    private void LastActor()
    {
        if (Last.Get())
        {
            if (currentIndex - 1 < 0) currentIndex = transform.childCount-1;
            else currentIndex--;
            UpdateActiveActors();
        }
    }
    
    private void NextActor()
    {
        if (Next.Get())
        {
            if (currentIndex + 1 > transform.childCount-1) currentIndex = 0;
            else currentIndex++;
            UpdateActiveActors();
        }
    }

    
    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
