//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose: 
// Notes:
//
//=============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RivenFramework;

public class WB_Pause : WidgetBlueprint
{
    //=-----------------=
    // Public Variables
    //=-----------------=

    //=-----------------=
    // Private Variables
    //=-----------------=


    //=-----------------=
    // Reference Variables
    //=-----------------=
    private GI_WidgetManager widgetManager;
    private GI_WorldLoader worldLoader;
    [SerializeField] private Button buttonResume, buttonSettings, buttonTitle, buttonQuit, buttonSelectCharacter;
    [SerializeField] private GameObject settingsWidget;


    //=-----------------=
    // Mono Functions
    //=-----------------=
    private void Start()
    {
        widgetManager = GameInstance.Get<GI_WidgetManager>();
        worldLoader = GameInstance.Get<GI_WorldLoader>();
        buttonResume.onClick.AddListener(delegate { OnClick("buttonResume"); });
        buttonSettings.onClick.AddListener(delegate { OnClick("buttonSettings"); });
        buttonTitle.onClick.AddListener(delegate { OnClick("buttonTitle"); });
        buttonQuit.onClick.AddListener(delegate { OnClick("buttonQuit"); });
        buttonSelectCharacter.onClick.AddListener(delegate { OnClick("buttonSelectCharacter"); });
    }

    private void OnDestroy()
    {
        Destroy(widgetManager.GetExistingWidget(settingsWidget.name));
    }


    //=-----------------=
    // Internal Functions
    //=-----------------=
    private void OnClick(string _button)
    {
        switch (_button)
        {
            case "buttonResume":
                if (widgetManager == null) widgetManager = GameInstance.Get<GI_WidgetManager>();
                widgetManager.ToggleWidget("WB_Pause");
                break;
            case "buttonSettings":
                if (widgetManager == null) widgetManager = GameInstance.Get<GI_WidgetManager>();
                widgetManager.AddWidget(settingsWidget);
                break;
            case "buttonTitle":
                var networkManager = GameInstance.Get<GI_NetworkManager>();
                networkManager?.Disconnect();
                worldLoader.LoadWorld("_Title");
                break;
            case "buttonQuit":
                Application.Quit();
                break;
            case "buttonSelectCharacter":
                
                break;
        }
    }


    //=-----------------=
    // External Functions
    //=-----------------=
}