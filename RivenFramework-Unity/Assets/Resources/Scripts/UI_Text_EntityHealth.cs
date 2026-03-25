//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using TMPro;
using UnityEngine;

    [RequireComponent(typeof(TMP_Text))]
    public class UI_Text_PawnHealth : MonoBehaviour
    {
        //=-----------------=
        // Public Variables
        //=-----------------=
        public Pawn targetPawn;
        public bool findPossessedPawn;


        //=-----------------=
        // Private Variables
        //=-----------------=


        //=-----------------=
        // Reference Variables
        //=-----------------=


        //=-----------------=
        // Mono Functions
        //=-----------------=
        private void Update()
        {
            if (findPossessedPawn)
            {
                targetPawn = GameInstance.Get<GI_PawnManager>().localPlayerCharacter.GetComponent<Pawn>();
            }

            if (targetPawn)
            {
                GetComponent<TMP_Text>().text =
                    $"{(targetPawn.currentStats.health / targetPawn.defaultStats.health) * 100}";
            }
            else
            {
                GetComponent<TMP_Text>().text = "---";
            }
        }

        //=-----------------=
        // Internal Functions
        //=-----------------=


        //=-----------------=
        // External Functions
        //=-----------------=
    }