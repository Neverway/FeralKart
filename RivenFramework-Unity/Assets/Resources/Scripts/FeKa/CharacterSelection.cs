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
using UnityEngine;

public class CharacterSelection : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public Camera viewCamera;
    public List<CharacterSelectionViewpoint> characterSelectionViewpoints = new List<CharacterSelectionViewpoint>();


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Enter the id of the character, it will move the css camera to look at them
    /// </summary>
    public void ViewCharacter(string characterID)
    {
        print($"ViewCharacter {characterID}");
        foreach (var selectionViewpoint in characterSelectionViewpoints)
        {
            if (selectionViewpoint.characterID == characterID)
            {
                viewCamera.transform.position = selectionViewpoint.cameraViewpoint.position;
                viewCamera.transform.rotation = selectionViewpoint.cameraViewpoint.rotation;
                return;
            }
        }
        print($"ViewCharacter {characterID} not found");
    }    
    
    /// <summary>
    /// Enter the index of the character, it will move the css camera to look at them
    /// </summary>
    public void ViewCharacter(int characterIndex)
    {
        var target = characterSelectionViewpoints[characterIndex];
        viewCamera.transform.position = target.cameraViewpoint.position;
        viewCamera.transform.rotation = target.cameraViewpoint.rotation;
    }

    /// <summary>
    /// Enables the character select's view camera and moves it to a fighter
    /// </summary>
    public void Initialize()
    {
        viewCamera.gameObject.SetActive(true);
        ViewCharacter(0);
    }

    /// <summary>
    /// Disables the character select's view camera
    /// </summary>
    public void Close()
    {
        viewCamera.gameObject.SetActive(false);
    }


    #endregion
}

[Serializable]
public struct CharacterSelectionViewpoint
{
    public string characterID;
    public Transform cameraViewpoint;
}
