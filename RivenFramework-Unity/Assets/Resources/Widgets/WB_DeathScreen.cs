//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RivenFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WB_DeathScreen : WidgetBlueprint
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public float endAlpha=0.45f;
    public float fadeSpeed=1;
    public float timeTillRemoval = 0;


    //=-----------------=
    // Private Variables
    //=-----------------=
    [SerializeField] private bool acceptingInputs;


    //=-----------------=
    // Reference Variables
    //=-----------------=
    private GI_WorldLoader worldLoader;
    
    [SerializeField] private Image image;


    //=-----------------=
    // Mono Functions
    //=-----------------=
    private void Start()
    {
        if (timeTillRemoval > 0)
        {
            Destroy(gameObject, timeTillRemoval);
        }
        
        //StartCoroutine(InputDelay());
        DOVirtual.Color(
            new Color(image.color.r, image.color.g, image.color.b, 0), 
            new Color(image.color.r, image.color.g, image.color.b, endAlpha),
            fadeSpeed,
            (value) =>
            {
                
               image.color = value;
            });
    }

    private void Update()
    {
        if (!acceptingInputs) return;
        if (Input.anyKeyDown)
        {
            worldLoader = GameInstance.Get<GI_WorldLoader>();
            worldLoader.ForceLoadWorld(SceneManager.GetActiveScene().name);
        }
    }

    //=-----------------=
    // Internal Functions
    //=-----------------=
    private IEnumerator InputDelay()
    {
        yield return new WaitForSeconds(0.2f);
        acceptingInputs = true;
    }


    //=-----------------=
    // External Functions
    //=-----------------=
}
