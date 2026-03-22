//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Neverway.Framework.PawnManagement;

namespace Neverway.Framework
{
    [RequireComponent(typeof(TMP_Text))]
    public class UI_Text_PawnName : MonoBehaviour
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
                targetPawn = FindPossessedPawn();
            }

            if (targetPawn)
            {
                //TODO RevampFix
                //GetComponent<TMP_Text>().text = targetPawn.currentStats.name;
            }
            else
            {
                GetComponent<TMP_Text>().text = "---";
            }
        }

        //=-----------------=
        // Internal Functions
        //=-----------------=
        private Pawn FindPossessedPawn()
        {
            foreach (var entity in FindObjectsByType<Pawn>(FindObjectsSortMode.None))
            {
                //TODO RevampFix
                //if (entity.isPossessed) return entity;
            }

            return null;
        }


        //=-----------------=
        // External Functions
        //=-----------------=
    }
}