//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
// Information
//      If the client owns the object (hasAuthority == true) this will send a TSYNC packet to the server at a
//      specified Hz rating (20Hz by default)
//      On remote clients (hasAuthority == false) incoming TYSNC packets are interpolated and applied to the attached
//      object's transform
//      
//      networkObjectID needs to be set to something unique (this is done automatically for objects created by NetSpawner)
//
//====================================================================================================================//

using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

/// <summary>
/// Attach to any game object that needs its transform synchronized over the network
/// </summary>
public class NetTransform : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Header("Object Identity")]
    [Tooltip("Unique ID assigned by the server when spawned, must match across all clients")]
    public string networkObjectUId;
    [Tooltip("If true, the local client controls this object, false it is controlled by a network client")]
    public bool hasAuthority = false;

    [Header("Sync Settings")]
    [Tooltip("How many times per second the owner sends a transform update packet, 40 by default")]
    public float sendRate = 40f;
    [Tooltip("If enabled, sync this object's position")]
    public bool syncPosition = true;
    [Tooltip("If enabled, sync this object's rotation")]
    public bool syncRotation = true;
    [Tooltip("If enabled, sync this object's scale")]
    public bool syncScale = false;

    [Header("Interpolation")] 
    [Tooltip("How aggressively the remote object catches up to the latest received state, the higher the value, the quicker it catches up, but can also lead to jerkier movement")]
    public float interpolationSpeed = 15f;
    [Tooltip("The maximum number of buffered states to be kept for interpolation, older ones are discarded")]
    public int interpolationBufferSize = 6;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private float sendTimer = 0f;
    private float sendInterval = 0f;
    private GI_NetworkManager networkManager;

    private readonly Queue<TransformSnapshot> snapshots = new Queue<TransformSnapshot>();
    private TransformSnapshot targetSnapshot;
    private bool hasTarget = false;

    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private Vector3 lastSentScale;
    
    // Movements below these thresholds won't send a packet
    private const float positionThreshold = 0.001f;
    private const float rotationThreshold = 0.1f;
    private const float scaleThreshold = 0.001f;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private struct TransformSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }


    #endregion


    #region=======================================( Functions )======================================================= //

    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Awake()
    {
        sendInterval = 1f / Mathf.Max(1f, sendRate); 
        lastSentPosition = transform.position;
        lastSentRotation = transform.rotation;
        lastSentScale = transform.localScale;
    }

    private void Start()
    {
        networkManager = GameInstance.Get<GI_NetworkManager>();
        
        if (networkManager != null) networkManager.RegisterNetTransform(networkObjectUId, this);
    }

    private void OnDestroy()
    {
        if (networkManager != null) networkManager.UnregisterNetTransform(networkObjectUId);
    }

    private void Update()
    {
        if (hasAuthority) OwnerUpdate();
        else RemoteUpdate();
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void OwnerUpdate()
    {
        sendTimer += Time.deltaTime;
        if (sendTimer < sendInterval) return;
        sendTimer -= sendInterval;
        
        // Dead reckoning sync (don't bother sending packet if nothing changed)
        bool posChanged = syncPosition && Vector3.Distance(transform.position, lastSentPosition) > positionThreshold;
        bool rotChanged = syncRotation && Quaternion.Angle(transform.rotation, lastSentRotation) > rotationThreshold;
        bool scaleChanged = syncScale && Vector3.Distance(transform.localScale, lastSentScale) > scaleThreshold;
        
        if (!posChanged && !rotChanged && !scaleChanged) return;
        
        lastSentPosition = transform.position;
        lastSentRotation = transform.rotation;
        lastSentScale = transform.localScale;

        networkManager.SendTransformSync(this);
    }

    private void RemoteUpdate()
    {
        if (!hasTarget) return;
        
        if (syncPosition) transform.position = Vector3.Lerp(transform.position, targetSnapshot.position, interpolationSpeed * Time.deltaTime);
        if (syncRotation) transform.rotation = Quaternion.Lerp(transform.rotation, targetSnapshot.rotation, interpolationSpeed * Time.deltaTime);
        if (syncScale) transform.localScale = Vector3.Lerp(transform.localScale, targetSnapshot.scale, interpolationSpeed * Time.deltaTime);

        // If close to target snapshot values, continue to the next snapshot
        if (snapshots.Count > 0)
        {
            float positionDequeueThreshold = 0.05f;
            float positionError = syncPosition ? Vector3.Distance(transform.position, targetSnapshot.position) : 0;
            if (positionError < positionDequeueThreshold) targetSnapshot = snapshots.Dequeue();
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void ReceiveTransformPacket(TransformSyncPacket packet)
    {
        var snapshot = new TransformSnapshot
        {
            position = packet.syncPosition ? new Vector3(packet.px, packet.py, packet.pz) : transform.position,
            rotation = packet.syncRotation ? Quaternion.Euler(packet.rx, packet.ry, packet.rz) : transform.rotation,
            scale = packet.syncScale ? new Vector3(packet.sx, packet.sy, packet.sz) : transform.localScale
        };

        while (snapshots.Count >= interpolationBufferSize) snapshots.Dequeue();

        if (!hasTarget)
        {
            transform.position = snapshot.position;
            transform.rotation = snapshot.rotation;
            transform.localScale = snapshot.scale;
            targetSnapshot = snapshot;
            hasTarget = true;
        }
        else
        {
            snapshots.Enqueue(snapshot);
        }
    }

    #endregion
}


[Serializable]
public class TransformSyncPacket
{
    // Object being synced
    public string objectUId;
    
    // Info to include in packet
    public bool syncPosition;
    public bool syncRotation;
    public bool syncScale;

    // Positon, Rotation, and Scale
    public float px, py, pz;
    public float rx, ry, rz;
    public float sx, sy, sz;
}