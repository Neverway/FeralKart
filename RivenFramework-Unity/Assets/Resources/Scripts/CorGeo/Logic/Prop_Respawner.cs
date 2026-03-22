//==========================================( Neverway 2025 )=========================================================//
// Author
//  Connorses
//
// Contributors
//  Liz M.
//
//====================================================================================================================//

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a prefab object on a timer, and spawns it again if it is destroyed.
/// </summary>
public class Prop_Respawner : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Header("Parameters")]
    [Tooltip("How long to wait before spawning the prop when activated")]
    [SerializeField] protected float spawnDelay;
    [Tooltip("Wait for a DestroySpawnedObject call before spawning the first object")]
    [SerializeField] protected bool waitForRespawn = false;
    [Tooltip("If false, the spawner will always set waitForRespawn when the spawned object is destroyed")]
    [SerializeField] protected bool autoRespawn = true;
    [Tooltip("If false, the spawner will not spawn the prop if one with the same unique ID already exists")]
    [SerializeField] protected bool allowDuplicates;
    [Tooltip("When powered, the spawned prop will be destroyed and a new one will be created")]
    public LogicInput<bool> respawnProp = new(false);

    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/

    
    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    protected Coroutine spawnWorker;
    protected GameObject spawnedObject { get; set; }

    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Header("References")]
    [Tooltip("This is the actor that will be created by the spawner")]
    [SerializeField] public GameObject propPrefab;
    [Tooltip("This is the unique identifier that will be assigned to the actor when spawned")]
    [SerializeField] public string propUniqueID;
    
    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        // TODO: CallOnSourceChange needs HasLogicOutputSource to fix possible null refs for unlinked logic I/Os
        // Using this 'if' statement as a quick fix for now ~Liz
        if (respawnProp.HasLogicOutputSource is false) return;
        respawnProp.CallOnSourceChanged(RespawnProp);
    }

    private void FixedUpdate()
    {
        if (waitForRespawn) return;
        
        if (spawnedObject == null)
        {
            UpdateRespawn();
        }
    }

    private void OnDisable ()
    {
        if (spawnWorker != null)
        {
            StopCoroutine(spawnWorker);
        }
        spawnWorker = null;
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    protected virtual IEnumerator SpawnWorker()
    {
        yield return new WaitForSeconds(spawnDelay);
        spawnedObject = Instantiate(propPrefab, transform.position, transform.rotation);
        var actor = spawnedObject.GetComponent<Actor>();
        if (actor) actor.uniqueId = propUniqueID;
        spawnWorker = null;
        if (autoRespawn == false)
        {
            waitForRespawn = true;
        }
    }
    
    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Destroy the spawned object so that it will respawn.
    /// </summary>
    private void DestroySpawnedObject()
    {
        StartCoroutine(DestroyWorker());
    }

    /// <summary>
    /// Destroy the object after moving it to trigger OnTriggerExit for any objects it's inside of.
    /// </summary>
    /// <returns></returns>
    private IEnumerator DestroyWorker()
    {
        waitForRespawn = false;
        if (spawnedObject == null)
        {
            yield break;
        }

        //Move the spawne dobject far away, triggering OnTriggerExit for anything it may have been inside of
        spawnedObject.transform.position = transform.position + Vector3.one * 10000f;
        //wait one frame, then destroy object
        yield return null;
        Destroy(spawnedObject);
    }

    private void UpdateRespawn()
    {
        if (!allowDuplicates)
        {
            if (DoesActorExist(propUniqueID))
            {
                spawnedObject = DoesActorExist(propUniqueID).gameObject;
                return ;
            }
        }
        
        DestroySpawnedObject();
        if (spawnWorker is null) spawnWorker = StartCoroutine(SpawnWorker());
    }
    
    
    private void RespawnProp()
    {
        if (!respawnProp.Get())
        {
            return;
        }
        
        DestroySpawnedObject();
        if (spawnWorker is null) spawnWorker = StartCoroutine(SpawnWorker());
    }
    
    

    // Used to see if we have any duplicate actors
    private Actor DoesActorExist(string _uuid)
    {
        var allActors = FindObjectsOfType<Actor>();
        
        foreach (Actor actor in allActors)
        {
            if (actor.uniqueId == "") continue;
            
            print($"Found matching object {actor.name} with {actor.uniqueId} to {_uuid}");
            if (actor.uniqueId == _uuid) return actor;
        }

        return null;
    }

    #endregion
}
