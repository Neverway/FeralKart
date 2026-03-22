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
using RivenFramework;
using UnityEngine;

public class FenRigPart : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public List<FenRigSprite> fenRigSprites;
    [SerializeField] private float updateRate = 0.25f;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    private bool active = true;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private GI_PawnManager pawnManager;
    private Transform target;
    private SpriteRenderer spriteRenderer;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        pawnManager = GameInstance.Get<GI_PawnManager>();
        InvokeRepeating(nameof(UpdateBillboard), 0, updateRate);
        spriteRenderer = GetComponent<SpriteRenderer>();
    }


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void UpdateBillboard()
    {
        if (!active) return;
        if (!pawnManager)
        {
            pawnManager = GameInstance.Get<GI_PawnManager>();
            return;
        }

        if (pawnManager.localPlayerCharacter && !target)
        {
            target = pawnManager.localPlayerCharacter.GetComponentInChildren<Camera>().transform;
            return;
        }
            
        if (!target) return;
        transform.LookAt(target, Vector3.up);
        UpdateFenRigSprite();
    }

    private void UpdateFenRigSprite()
    {
        var parentBone = transform.parent;
        var directionToViewer = target.transform.position - parentBone.transform.position;
        directionToViewer = directionToViewer.normalized;
        FenRigSprite closestMatchSprite = null;
        float closestMatchingDistance = float.MaxValue;

        foreach (var _sprite in fenRigSprites)
        {
            Vector3 worldSpriteDirection = parentBone.transform.TransformDirection(_sprite.activatingDirection);
            worldSpriteDirection = worldSpriteDirection.normalized;
            
            float currentDistance = Vector3.Distance(directionToViewer, worldSpriteDirection);
            
            if (currentDistance <= closestMatchingDistance)
            {
                closestMatchingDistance = currentDistance;
                closestMatchSprite = _sprite;
            }
            print($"dir2View {directionToViewer} | world {worldSpriteDirection} | dist {currentDistance} | closest {closestMatchingDistance}");
        }

        spriteRenderer.sprite = closestMatchSprite.sprite;
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void SetBillboardActive(bool _active)
    {
        active = _active;
    }


    #endregion
}

[Serializable]
public class FenRigSprite
{
    public Sprite sprite;
    public Vector3 activatingDirection;
}