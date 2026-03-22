//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using UnityEngine;

/// <summary>
/// A puzzle element that creates teleportation bubbles
/// </summary>
public class CorGeo_RealityPuncher : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("The other reality puncher that this one will swap locations with")]
    public CorGeo_RealityPuncher linkedPuncher;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Reference to the sphere trigger volume, used to get which actors are in the teleport bubble")]
    public Volume bubbleVolume;
    [Tooltip("Reference to this puncher's animator, used for easy access to sync animations between linked punchers")]
    public Animator animator;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Wait for correct point in the animation before swapping
    /// </summary>
    private IEnumerator CoSwap()
    {
        yield return new WaitForSeconds(4);
        SwapBubbledActors();
        SwapBubbledIslands();
    }
    
    /// <summary>
    /// Switch the level geometry with the linked puncher's
    /// </summary>
    private void SwapBubbledIslands()
    {
        // Swap bubble islands
        // ReSharper disable once SwapViaDeconstruction
        var originalPosition = gameObject.transform.position;
        var originalParent = transform.parent;
        var originalPlaybackTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        gameObject.transform.position = linkedPuncher.transform.position;
        gameObject.transform.parent = linkedPuncher.transform.parent;
        linkedPuncher.transform.position = originalPosition;
        linkedPuncher.transform.parent = originalParent;
        
        // Link the animation playback times so that both islands being swapped are seamless
        linkedPuncher.animator.Play("On", 0, originalPlaybackTime);
    }

    /// <summary>
    /// Switch the actors in the teleport bubble with the linked puncher's
    /// </summary>
    private void SwapBubbledActors()
    {
        // Move current bubble actors to other bubble
        var originalOffset = linkedPuncher.transform.position-this.transform.position;
        foreach (var pawn in bubbleVolume.pawnsInTrigger)
        {
            pawn.transform.position += originalOffset;
        }
        foreach (var prop in bubbleVolume.propsInTrigger)
        {
            prop.transform.position += originalOffset;
        }
        
        // Move other bubble actors to current bubble
        foreach (var pawn in linkedPuncher.bubbleVolume.pawnsInTrigger)
        {
            pawn.transform.position -= originalOffset;
        }
        foreach (var prop in linkedPuncher.bubbleVolume.propsInTrigger)
        {
            prop.transform.position -= originalOffset;
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Switch this puncher with the linked one
    /// </summary>
    public void Swap()
    {
        animator.Play("On");
        StopAllCoroutines();
        StartCoroutine(CoSwap());
    }


    #endregion
}
