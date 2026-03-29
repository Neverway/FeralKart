//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using RivenFramework;
using UnityEngine;
using UnityEngine.UI;

public class WB_Title : MonoBehaviour
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public string targetLevelID;


    //=-----------------=
    // Private Variables
    //=-----------------=


    //=-----------------=
    // Reference Variables
    //=-----------------=
    private GI_WidgetManager widgetManager;
    private GI_WorldLoader worldLoader;
    //private LevelManager levelLoader; // Added for loading the overworld levels from Cartographer

    [SerializeField] private Button buttonLocal,
        buttonOnline,
        buttonExtras,
        buttonRanking,
        buttonSettings,
        buttonQuit,
        buttonCredits,
        buttonLanguage;

    [SerializeField] private GameObject localWidget, onlineWidget, extrasWidget, rankingWidget, settingsWidget, creditsWidget, languageWidget;


    //=-----------------=
    // Mono Functions
    //=-----------------=
    private void Start()
    {
        widgetManager = FindObjectOfType<GI_WidgetManager>();
        worldLoader = FindObjectOfType<GI_WorldLoader>();
        //levelLoader = FindObjectOfType<LevelManager>();
        buttonLocal.onClick.AddListener(delegate { OnClick("buttonLocal"); });
        buttonOnline.onClick.AddListener(delegate { OnClick("buttonOnline"); });
        buttonExtras.onClick.AddListener(delegate { OnClick("buttonExtras"); });
        buttonRanking.onClick.AddListener(delegate { OnClick("buttonRanking"); });
        buttonSettings.onClick.AddListener(delegate { OnClick("buttonSettings"); });
        buttonQuit.onClick.AddListener(delegate { OnClick("buttonQuit"); });
        buttonCredits.onClick.AddListener(delegate { OnClick("buttonCredits"); });
        buttonLanguage.onClick.AddListener(delegate { OnClick("buttonLanguage"); });
    }

    private void Update()
    {

    }

    //=-----------------=
    // Internal Functions
    //=-----------------=
    private void OnClick(string button)
    {
        switch (button)
        {
            case "buttonLocal":
                if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(localWidget);
                break;
            case "buttonOnline":
                if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(onlineWidget);
                break;
            case "buttonExtras":
                if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(extrasWidget);
                break;
            case "buttonRanking":
                if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(rankingWidget);
                break;
            case "buttonSettings":
                if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(settingsWidget); // Create the settings widget
                //Destroy(gameObject); // Remove the current widget
                //GameInstance.GetWidget("WB_Settings").GetComponent<WB_Settings>().Init();
                break;
            case "buttonQuit":
                Application.Quit();
                break;
            case "buttonCredits":
                if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(creditsWidget);
                break;
            case "buttonLanguage":
                if (!widgetManager) widgetManager = FindObjectOfType<GI_WidgetManager>();
                widgetManager.AddWidget(languageWidget);
                break;
        }
    }


    //=-----------------=
    // External Functions
    //=-----------------=
}