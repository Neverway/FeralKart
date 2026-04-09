using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RivenFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WB_HUDSpectator : MonoBehaviour
{
    public FeKaPawn targetFeKaPawn;
    public bool findPossessedPawn;

    public TMP_Text timer;
    private FeKa_GameRules fekaGameRules;

    private void Start()
    {
        fekaGameRules = FindObjectOfType<FeKa_GameRules>();
        if (fekaGameRules != null) fekaGameRules.OnGameStateReceived += OnGameStateReceived;
        
        if (fekaGameRules != null && fekaGameRules.lastGameState != null)
            OnGameStateReceived(fekaGameRules.lastGameState);
    }
    private void OnDestroy()
    {
        if (fekaGameRules != null)
            fekaGameRules.OnGameStateReceived -= OnGameStateReceived;
    }

    void Update()
    {
        // Pawn reference check
        if (findPossessedPawn)
        {
            targetFeKaPawn = GameInstance.Get<GI_PawnManager>().localPlayerCharacter.GetComponent<FeKaPawn>();
        }
        if (!targetFeKaPawn) return;
        
    }

    private void OnGameStateReceived(FeKa_GameStatePacket gameState)
    {
        timer.text = ($"{gameState.TimeLeft}");
    }
}