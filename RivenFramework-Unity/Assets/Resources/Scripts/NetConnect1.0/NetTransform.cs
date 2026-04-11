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
    public float interpolationSpeed = 60f;
    [Tooltip("How many degrees per second the remote object rotates toward the target snapshot rotation")]
    public float interpolationRotationSpeed = 720f;
    [Tooltip("How far (in units) the remote object must be from a snapshot before it snaps directly to it rather than interpolating. Prevents rubberbanding after a large gap")]
    public float snapThreshold = 8f;
    [Tooltip("How much past the latest received snapshot to extrapolate when a packet is late. 1 = extrapolate one full packet interval ahead, 0 = no extrapolation.")]
    [Range(0f, 1f)]
    public float extrapolationFactor = 0.5f;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private float sendTimer = 0f;
    private float sendInterval = 0f;
    private GI_NetworkManager networkManager;
    
    private TransformSnapshot fromSnapshot;
    private TransformSnapshot toSnapshot;
    private bool hasSnapshots = false;
    private float timeSinceLastPacket = 0f;
    private float estimatedPacketInterval = 0.025f; // matches default sendRate=40

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

    public NetTransform copyObjectIdentityFrom;


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
        
        // We really need to find a better way to update this
        if (copyObjectIdentityFrom != null)
        {
            InvokeRepeating(nameof(UpdateCopiedObjectIdentity), 0.25f, 5f);
        }
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
        if (!hasSnapshots) return;
 
        timeSinceLastPacket += Time.deltaTime;
 
        float t = timeSinceLastPacket / estimatedPacketInterval;
 
        Vector3 targetPos = toSnapshot.position;
        Quaternion targetRot = toSnapshot.rotation;
        Vector3 targetScale = toSnapshot.scale;
 
        if (t > 1f && extrapolationFactor > 0f)
        {
            float extra = Mathf.Min(t - 1f, extrapolationFactor);
            targetPos   = toSnapshot.position + (toSnapshot.position - fromSnapshot.position) * extra;
            targetRot   = toSnapshot.rotation;
        }
 
        float blend = Mathf.Clamp01(t);
 
        Vector3 blendedPos   = Vector3.Lerp(fromSnapshot.position, targetPos, blend);
        Quaternion blendedRot = Quaternion.Slerp(fromSnapshot.rotation, targetRot, blend);
        Vector3 blendedScale  = Vector3.Lerp(fromSnapshot.scale, targetScale, blend);
 
        float posError = syncPosition ? Vector3.Distance(transform.position, blendedPos) : 0f;
        if (posError > snapThreshold)
        {
            if (syncPosition)  transform.position   = blendedPos;
            if (syncRotation)  transform.rotation   = blendedRot;
            if (syncScale)     transform.localScale  = blendedScale;
        }
        else
        {
            float catchUp = Mathf.Clamp01(interpolationSpeed * Time.deltaTime);
            if (syncPosition)  transform.position   = Vector3.Lerp(transform.position,   blendedPos,   catchUp);
            if (syncRotation)  transform.rotation   = Quaternion.Slerp(transform.rotation, blendedRot, catchUp);
            if (syncScale)     transform.localScale  = Vector3.Lerp(transform.localScale,  blendedScale, catchUp);
        }
    }

    private void UpdateCopiedObjectIdentity()
    {
        networkObjectUId = copyObjectIdentityFrom.networkObjectUId;
        hasAuthority = copyObjectIdentityFrom.hasAuthority;
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void ReceiveTransformPacket(TransformSyncPacket packet)
    {
        var incoming = new TransformSnapshot
        {
            position = packet.syncPosition ? new Vector3(packet.px, packet.py, packet.pz) : transform.position,
            rotation = packet.syncRotation ? Quaternion.Euler(packet.rx, packet.ry, packet.rz) : transform.rotation,
            scale    = packet.syncScale    ? new Vector3(packet.sx, packet.sy, packet.sz)    : transform.localScale
        };
 
        if (!hasSnapshots)
        {
            transform.position   = incoming.position;
            transform.rotation   = incoming.rotation;
            transform.localScale = incoming.scale;
            fromSnapshot = incoming;
            toSnapshot   = incoming;
            hasSnapshots = true;
            timeSinceLastPacket = 0f;
            return;
        }
 
        float measuredInterval = timeSinceLastPacket;
        if (measuredInterval > 0f && measuredInterval < 1f)
            estimatedPacketInterval = Mathf.Lerp(estimatedPacketInterval, measuredInterval, 0.2f);
 
        fromSnapshot = toSnapshot;
        toSnapshot   = incoming;
        timeSinceLastPacket = 0f;
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