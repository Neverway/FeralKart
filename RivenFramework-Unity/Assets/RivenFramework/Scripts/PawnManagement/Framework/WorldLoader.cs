//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose: Handle loading scenes including showing a loading screen
// Notes: This script is still quite messy, future me will clean it up eventually
//
//=============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Neverway.Framework.PawnManagement
{
    public class WorldLoader : MonoBehaviour
    {
        //=-----------------=
        // Public Variables
        //=-----------------=
        public static WorldLoader Instance;

        [SerializeField] public float delayBeforeWorldChange = 0.25f;
        [Tooltip("The minimum amount of time to wait on the loading screen between level changes")]
        [SerializeField] public float minimumRequiredLoadTime = 1f;
        [Tooltip("The ID of the level that we will wait on during loading (It's the loading screen level)")]
        [SerializeField] private string loadingWorldID = "_Travel";
        [Tooltip("The ID of the level that we store objects in between level changes to avoid them being unloaded")]
        public string streamingWorldID = "_Streaming";
        public static event Action OnWorldLoaded;
        [Tooltip("An exposed value used for referencing if the game is currently in the process of loading the level")]
        public bool isLoading;


        //=-----------------=
        // Private Variables
        //=-----------------=
        private string targetWorldID;


        //=-----------------=
        // Reference Variables
        //=-----------------=
        private Image loadingBar;


        //=-----------------=
        // Mono Functions
        //=-----------------=
        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Make sure the streaming world is loaded, so we can store actors there if needed
            if (!SceneManager.GetSceneByName(streamingWorldID).isLoaded)
            {
                SceneManager.LoadScene(streamingWorldID, LoadSceneMode.Additive);
            }
        }

        //=-----------------=
        // Internal Functions
        //=-----------------=
        /// <summary>
        /// Set up the loading screen level and loading screen UI, then start loading the target level
        /// </summary>
        private IEnumerator StartLoadWithTravelLevel()
        {
            isLoading = true;
            yield return new WaitForSeconds(delayBeforeWorldChange);

            // Load the transition level over top everything else
            SceneManager.LoadScene(loadingWorldID);

            // The following should execute on the loading screen scene
            var loadingBarObject = GameObject.FindWithTag("LoadingBar");
            if (loadingBarObject) loadingBar = loadingBarObject.GetComponent<Image>();

            yield return new WaitForSeconds(minimumRequiredLoadTime);
            StartCoroutine(StartAsyncLoadOperation());
        }

        /// <summary>
        /// Asynchronously load the target level and update the loading screen accordingly (if applicable)
        /// </summary>
        private IEnumerator StartAsyncLoadOperation()
        {
            // Create an async operation (Will automatically switch to target scene once it's finished loading)
            var targetLevel = SceneManager.LoadSceneAsync(targetWorldID);

            // Set loading bar to reflect async progress
            while (targetLevel.progress < 1)
            {
                if (loadingBar) loadingBar.fillAmount = targetLevel.progress;
                yield return new WaitForEndOfFrame();
            }

            // Scene has finished loading, trigger the on world loaded event
            isLoading = false;
            if (OnWorldLoaded is not null) OnWorldLoaded.Invoke();
        }
        
        /// <summary>
        /// A strange blend of StartLoadWithTravelLevel and StartAsyncLoadOperation that seems to handle ejecting streamed actors correctly
        /// </summary>
        private IEnumerator LoadAsync()
        {
            isLoading = true;

            yield return new WaitForSeconds(delayBeforeWorldChange);

            AsyncOperation loadAsync = SceneManager.LoadSceneAsync(targetWorldID);
            loadAsync.allowSceneActivation = false;

            while (loadAsync.progress < 0.9f)
            {
                yield return null;
            }

            loadAsync.allowSceneActivation = true;

            yield return null;

            EjectStreamedActors();

            isLoading = false;
        }

        private IEnumerator ForceLoad(float _loadDelay)
        {
            yield return new WaitForSeconds(_loadDelay);
            SceneManager.LoadScene(targetWorldID);
        }

        private IEnumerator StreamLoad()
        {
            isLoading = true;
            yield return new WaitForSeconds(delayBeforeWorldChange);
            print("Active scene was " + SceneManager.GetActiveScene().name);

            // Load the target scene
            SceneManager.LoadScene(targetWorldID, LoadSceneMode.Additive);
            print("Active scene is " + SceneManager.GetActiveScene().name);

            // Unload the active scene
            var targetLevel = SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());
            while (targetLevel != null && targetLevel.progress < 1)
            {
                yield return new WaitForEndOfFrame();
            }
            DevConsole.Log($"Unloaded previous scene", "WorldLoader");

            isLoading = false;

            GameObject[] streamedObjects = SceneManager.GetSceneByName(streamingWorldID).GetRootGameObjects();

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(targetWorldID)); // Assign the new scene to be the active scene
            DevConsole.Log($"Set active scene to {targetWorldID}", "WorldLoader");

            //EjectStreamedActors(streamedObjects);
        }

        /// <summary>
        /// When the level is loaded, this will remove any objects we wanted to keep during a level change, from the streaming level
        /// </summary>
        private void EjectStreamedActors()
        {
            foreach (var actor in SceneManager.GetSceneByName(streamingWorldID).GetRootGameObjects())
            {
                SceneManager.MoveGameObjectToScene(actor.gameObject, SceneManager.GetActiveScene());
            }
        }
        
        /// <summary>
        /// This seems to update the OnWorldLoaded event to see if it should be fired
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeStaticFields()
        {
            OnWorldLoaded = null;
        }


        //=-----------------=
        // External Functions
        //=-----------------=
        public void LoadWorld(string _worldID)
        {
            targetWorldID = _worldID;
            if (!DoesSceneExist(_worldID))
            {
                Debug.LogWarning(_worldID + " Is not a valid level! Loading fallback scene...");
                targetWorldID = "_Error";
                Destroy(GameInstance.GetWidget("WB_Loading"));
            }

            StartCoroutine(StartLoadWithTravelLevel());
        }

        public void ForceLoadWorld(string _targetSceneID, float _delay)
        {
            targetWorldID = _targetSceneID;
            if (!DoesSceneExist(_targetSceneID))
            {
                Debug.LogWarning(_targetSceneID + " Is not a valid level! Loading fallback scene...");
                targetWorldID = "_Error";
                Destroy(GameInstance.GetWidget("WB_Loading"));
            }

            StartCoroutine(ForceLoad(_delay));
        }

        public void StreamLoadWorld(string _targetSceneID)
        {
            targetWorldID = _targetSceneID;
            if (!DoesSceneExist(_targetSceneID))
            {
                Debug.LogWarning(_targetSceneID + " Is not a valid level! Loading fallback scene...");
                targetWorldID = "_Error";
                Destroy(GameInstance.GetWidget("WB_Loading"));
            }

            if (isLoading) return;
            //DevConsole.Log($"StreamLoadWorld firing StreamLoad coroutine...", "WorldLoader");
            StartCoroutine(StreamLoad());
            // TODO: Identify purpose of Graphics_SliceableObjectManager
            //Graphics_SliceableObjectManager.Instance.ClearList();
            //StartCoroutine(LoadAsync());
            //StartCoroutine(StreamLoadDos());
        }

        public void StreamLoadWorld(int _targetSceneIDnum)
        {
            targetWorldID = SceneManager.GetSceneByBuildIndex(_targetSceneIDnum).name;

            if (isLoading) return;
            StartCoroutine(StreamLoad());
            StartCoroutine(LoadAsync());
        }

        public void LoadByIndex(int _targetSceneID)
        {
            SceneManager.LoadScene(_targetSceneID);
        }

        // This code was expertly copied from @Yagero on github.com
        // https://gist.github.com/yagero/2cd50a12fcc928a6446539119741a343
        // (Seriously though, this function is a lifesaver, so thanks!)
        public static bool DoesSceneExist(string _targetSceneID)
        {
            if (string.IsNullOrEmpty(_targetSceneID)) return false;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var lastSlash = scenePath.LastIndexOf("/");
                var sceneName = scenePath.Substring(lastSlash + 1, scenePath.LastIndexOf(".") - lastSlash - 1);

                if (string.Compare(_targetSceneID, sceneName, true) == 0) return true;
            }

            return false;
        }
    }
}
