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

public class Prop_Destructable : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("The sprites in order from least destroyed to most destroyed")]
    public List<Sprite> levelOfDestructionSprites;
    public float requiredImpactMagnitude = 2;
    public bool repeatLastTwoStatesWhenAtEndOfIndex;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private int currentLevelOfDestruction;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    public SpriteRenderer spriteRenderer;
    public GameObject emitOnDestruction;
    public Transform emitDestructionTransform;
    public AudioClip impactSound;
    public AudioSource audioSource;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude > requiredImpactMagnitude)
        {
            SetLevelOfDestruction();
        }    
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void SetLevelOfDestruction()
    {
        audioSource.PlayOneShot(impactSound);
        Instantiate(emitOnDestruction, emitDestructionTransform.position, emitDestructionTransform.rotation, null);
        if (currentLevelOfDestruction < levelOfDestructionSprites.Count-1)
        {
            currentLevelOfDestruction++;
        }
        else if (currentLevelOfDestruction == levelOfDestructionSprites.Count-1 && repeatLastTwoStatesWhenAtEndOfIndex)
        {
            currentLevelOfDestruction = levelOfDestructionSprites.Count - 2;
        }
        spriteRenderer.sprite = levelOfDestructionSprites[currentLevelOfDestruction];
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
