//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose: Adjusts parameters for the current world (scene) and handles 
//  spawning the player pawn at player start points
// Notes:
//
//=============================================================================

using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Neverway.Framework.PawnManagement
{
    public class WorldSettings : MonoBehaviour
    {
        //=-----------------=
        // Public Variables
        //=-----------------=
        [Tooltip("Set this to override the default gamemode that's specified in the game instance, doing so will define what kind of gamemode pawn to spawn")]
        public GameMode gameModeOverride;
        [Tooltip("Which pawn should be spawned from the above gamemode")]
        public int startingGameMode;
        public bool debugShowKillZones;
        public bool disableWorldKillVolume;
        public bool disableWorldKillY;
        public int worldKillVolumeDistance = 32767;
        public int worldKillYDistance = -100;


        //=-----------------=
        // Private Variables
        //=-----------------=


        //=-----------------=
        // Reference Variables
        //=-----------------=
        private GameInstance gameInstance;


        //=-----------------=
        // Mono Functions
        //=-----------------=
        private void Start()
        {
            InvokeRepeating(nameof(CheckKillVolume), 0, 2);
        }

        private void Update()
        {
            if (GetGameInstance() is false) return;
            if (gameInstance.localPlayerCharacter is null) SpawnPlayerCharacter();
        }

        private void OnDrawGizmos()
        {
            if (debugShowKillZones is false) return;
            Gizmos.color = Color.red;
            if (disableWorldKillVolume is false) Gizmos.DrawCube(new Vector3(0, 0, 0), new Vector3(worldKillVolumeDistance * 0.5f, worldKillVolumeDistance * 0.5f, worldKillVolumeDistance * 0.5f));
            if (disableWorldKillY is false) Gizmos.DrawCube(new Vector3(0, worldKillYDistance, 0), new Vector3(1000, 1, 1000));
        }

        //=-----------------=
        // Internal Functions
        //=-----------------=
        private bool GetGameInstance()
        {
            if (gameInstance is null)
            {
                if (gameInstance is null)
                {
                    gameInstance = FindObjectOfType<GameInstance>();
                    if (gameInstance is null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        
        private void CheckKillVolume()
        {
            if (disableWorldKillVolume && disableWorldKillY) return;
            foreach (var pawn in FindObjectsOfType<Pawn>())
            {
                if (disableWorldKillVolume is false)
                {
                    var distanceToEntity = Vector3.Distance(pawn.transform.position, new Vector3(0, 0, 0));
                    if (distanceToEntity >= worldKillVolumeDistance || distanceToEntity <= (worldKillVolumeDistance * -1))
                    {
                        pawn.ModifyHealth(-99999);
                    }
                }

                if (disableWorldKillY is false)
                {
                    if (pawn.transform.position.y <= worldKillYDistance)
                    {
                        pawn.ModifyHealth(-99999);
                    }
                }
            }

            foreach (var actor in FindObjectsOfType<ActorData>())
            {
                if (!actor.CompareTag("PhysProp")) continue;

                if (disableWorldKillVolume is false)
                {
                    var distanceToEntity = Vector3.Distance(actor.transform.position, new Vector3(0, 0, 0));
                    if (distanceToEntity >= worldKillVolumeDistance || distanceToEntity <= (worldKillVolumeDistance * -1))
                    {
                        Destroy(actor.gameObject);
                    }
                }

                if (disableWorldKillY is false)
                {
                    if (actor.transform.position.y <= worldKillYDistance)
                    {
                        Destroy(actor.gameObject);
                    }
                }
            }
        }

        public static Transform GetPlayerStartPoint(bool _usePlayerStart = true)
        {
            if (_usePlayerStart is false) return null;
            var allPossibleStartPoints = FindObjectsOfType<PlayerStart>();
            var allValidStartPoints = allPossibleStartPoints.Where(_possibleStartPoint => _possibleStartPoint.playerStartFilter == "").ToList();

            if (allValidStartPoints.Count == 0) return null;
            var random = Random.Range(0, allValidStartPoints.Count);
            return allValidStartPoints[random].transform;
        }


        //=-----------------=
        // External Functions
        //=-----------------=
        public void SpawnPlayerCharacter()
        {
            
            // EDIT: This is not the responsibility of this class, removing ~Liz
            // Perform a check first to see if there is already a local player character in the scene
            //Debug.Log("LocalPlayerCharacter is empty in the gameInstance! Has the player spawned yet? Checking...");
            /*foreach (var pawn in FindObjectsOfType<Pawn>())
            {
                if (pawn.isPossessed)
                {
                    gameInstance.localPlayerCharacter = pawn;
                    return;
                }
            }*/

            // EDIT: The player hasn't even been told to spawn yet, so why is this here? ~Liz
            //Debug.Log("No possessed actors found. Checking actor controllers to see if any of them are player driven...");
            /*foreach (var pawn in FindObjectsOfType<Pawn>())
            {
                if (gameInstance.PlayerControllerClasses.Contains(pawn.currentController))
                {
                    gameInstance.localPlayerCharacter = pawn;
                    //Debug.Log("A valid controller actor was found, assigning them to be the LocalPlayerCharacter.");
                    return;
                }
            }*/
            //Debug.Log("No player found. Let's spawn a new one!");

            var startPoint = GetPlayerStartPoint();

            // Determine the game mode to use - either the override or the default.
            var gameMode = gameInstance.defaultGamemode;
            if (gameModeOverride is not null) gameMode = gameModeOverride;
            // Choose the class type based on whether starting as a spectator.
            var classToInstantiate = gameMode.gamemodePawns[startingGameMode];
            // Determine the spawn position and rotation - use default if startPoint is null.
            Vector3 spawnPosition = startPoint ? startPoint.position : Vector3.zero;
            Quaternion spawnRotation = startPoint ? startPoint.rotation : Quaternion.identity;
            // Instantiate and assign the local player character
            gameInstance.localPlayerCharacter = Instantiate(classToInstantiate, spawnPosition, spawnRotation).GetComponent<Pawn>();
        }
    }
}
