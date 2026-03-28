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
using RivenFramework;
using UnityEngine;
using UnityEngine.UI;

public class UI_RocketTargeting : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Header("UI References")]
    public RectTransform searchReticle;
    public RectTransform lockReticle;
    public CanvasScaler canvasScaler;

    [Header("Settings")]
    public float lockDelay = 0.5f;       

    [HideInInspector] public List<FeKaPawn> candidates = new List<FeKaPawn>();
    [HideInInspector] public FeKaPawn lockedTarget { get; private set; }

    private Camera _cam;
    private float _candidateTimer = 0f;
    private bool _locked = false;

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    private void Awake()
    {
        _cam = GameInstance.Get<GI_PawnManager>().localPlayerCharacter.GetComponent<Pawn>().viewPoint.GetComponent<Camera>();
        lockReticle.gameObject.SetActive(false);
        searchReticle.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (_locked)
        {
            TrackLockedTarget();
        }
        else
        {
            ScanForTarget();
        }
    }

    private void ScanForTarget()
    {
        var validNow = new List<FeKaPawn>();
        foreach (var pawn in candidates)
        {
            if (pawn == null) continue;
            if (IsInsideSearchReticle(pawn))
                validNow.Add(pawn);
        }

        if (validNow.Count > 0)
        {
            _candidateTimer += Time.deltaTime;
            if (_candidateTimer >= lockDelay)
                LockOn(validNow[Random.Range(0, validNow.Count)]);
        }
        else
        {
            _candidateTimer = 0f;
        }
    }

    private bool IsInsideSearchReticle(FeKaPawn pawn)
    {
        var screenPos = _cam.WorldToScreenPoint(pawn.transform.position);

        if (screenPos.z < 0) return false;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            searchReticle, screenPos, null, out var localPoint);

        return searchReticle.rect.Contains(localPoint);
    }

    private void LockOn(FeKaPawn target)
    {
        lockedTarget = target;
        _locked = true;
        searchReticle.gameObject.SetActive(false);
        lockReticle.gameObject.SetActive(true);
    }

    private void TrackLockedTarget()
    {
        if (lockedTarget == null)
        {
            _locked = false;
            lockedTarget = null;
            _candidateTimer = 0f;
            lockReticle.gameObject.SetActive(false);
            searchReticle.gameObject.SetActive(true);
            return;
        }

        var screenPos = _cam.WorldToScreenPoint(lockedTarget.transform.position);

        if (screenPos.z < 0)
        {
            _locked = false;
            lockedTarget = null;
            _candidateTimer = 0f;
            lockReticle.gameObject.SetActive(false);
            searchReticle.gameObject.SetActive(true);
            return;
        }

        lockReticle.position = screenPos;
    }
    
    

    #endregion
}
