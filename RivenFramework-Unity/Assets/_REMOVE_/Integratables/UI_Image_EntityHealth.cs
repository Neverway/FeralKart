//===================== (Neverway 2024) Written by Liz M. and Connorses =====================
//
// Purpose: Displays health of the possessed pawn.
// Notes: Connorses modified this to use a tween.
//
//=============================================================================

using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Neverway.Framework.PawnManagement
{
    [RequireComponent(typeof(Image))]
    public class UI_Image_PawnHealth : MonoBehaviour
    {
        //=-----------------=
        // Public Variables
        //=-----------------=
        public Pawn targetPawn;
        public bool findPossessedPawn;


        //=-----------------=
        // Private Variables
        //=-----------------=
        private float previousHealth;


        //=-----------------=
        // Reference Variables
        //=-----------------=
        private Image image;


        //=-----------------=
        // Mono Functions
        //=-----------------=

        private void Start ()
        {
            image = GetComponent<Image>();
        }

        private void Update()
        {
            if (findPossessedPawn && targetPawn == null)
            {
                targetPawn = FindPossessedPawn();
                if (targetPawn != null )
                {
                    previousHealth = targetPawn.currentStats.health;
                    image.fillAmount = (targetPawn.currentStats.health / targetPawn.defaultStats.stats.health) * 100 * 0.01f;
                }
            }
            if (targetPawn)
            {
                //If health has changed, animate health.
                if (targetPawn.currentStats.health != previousHealth)
                {
                    image.DOKill();
                    //image.DoFillAmount((targetPawn.currentState.health / targetPawn.defaultState.health) * 100 * 0.01f, 0.3f);
                    image.fillAmount=((targetPawn.currentStats.health / targetPawn.defaultStats.stats.health) * 100 * 0.01f);
                }
                previousHealth = targetPawn.currentStats.health;
            }
            else
            {
                image.fillAmount = 0;
            }
        }

        //=-----------------=
        // Internal Functions
        //=-----------------=
        private Pawn FindPossessedPawn()
        {
            foreach (var entity in FindObjectsByType<Pawn>(FindObjectsSortMode.None))
            {
                if (entity.isPossessed) return entity;
            }
            return null;
        }


        //=-----------------=
        // External Functions
        //=-----------------=
    }
    
}