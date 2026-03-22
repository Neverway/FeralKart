//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M., Connorses, Errynei, Soulex
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;

/// <summary>
/// ?????
/// </summary>
[Serializable]
public class RiftManager_ActorHandler : ILoggable
{
    /// <summary>
    /// Class constructor
    /// </summary>
    public RiftManager_ActorHandler(RiftManager riftManager)
    {
        this.riftManager = riftManager;
        EnableRuntimeLogging = riftManager.EnableRuntimeLogging;
    }
    
    
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public bool EnableRuntimeLogging { get; set; }


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("Link to parent class for logging")]
    private RiftManager riftManager;
    [Tooltip("A list of all CorGeo actors in the current level, every object with CorGeo_Actor adds itself here in their start method")]
    public static List<CorGeo_Actor> CorGeo_Actors = new List<CorGeo_Actor> { };


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public void RestoreActors()
    {
        foreach (CorGeo_Actor actor in CorGeo_Actors)
        {
            actor.GoHome();
        }
    }
    
    /// <summary>
    /// Calculate where an object in Null-Space should move to if the rift scales to the given percent.
    /// </summary>
    /// <param name="_actorPosition">The current position of the actor we are moving</param>
    /// <param name="_newRiftPercent">The current percentage of how distorted the rift is</param>
    /// <returns>Returns the new position the actor should be at to avoid rift offsets</returns>
    public Vector3 MoveActorPositionWithNullSpace (Vector3 _actorPosition, float _newRiftPercent)
    {
        // Calculate how far across null-space the transform is.
        float distanceFromPlaneA = RiftManager.cutPlaneA.GetDistanceToPoint(_actorPosition);
        
        // Exit if the rift is near collapse
        if (distanceFromPlaneA == 0 || RiftManager.currentRiftWidth == 0)
        {
            DebugConsole.Log(this, "EARLY EXIT - Null-space is near collapse");    
            return _actorPosition;
        }

        // Calculate the actor position percentage across null-space (0 = actor on A-Plane, 1 = actor on B-Plane)
        float actorDistancePercentAcrossNullSpace = 0;
        if (RiftManager.currentRiftWidth != 0)
        {
            //I'm avoiding doing this calculation if currentRiftWidth = 0.
            //This avoids errors where the value was Infinity or something.
            actorDistancePercentAcrossNullSpace = distanceFromPlaneA / RiftManager.currentRiftWidth;
        }
        

        if (_newRiftPercent == 0)
        {
            Debug.LogError("HEYA WE ARE DIVIDING BY ZERO!! Expect player vaporization!!");
            Debug.LogError($"ADPANS {distanceFromPlaneA} / {RiftManager.currentRiftWidth} = ??");
            Debug.LogError($"NRW {RiftManager.riftStartingWidth} * {_newRiftPercent} = ??");
        }
        
        // Calculate the new distance from A-Plane based on current rift scale
        float newRiftWidth = RiftManager.riftStartingWidth * _newRiftPercent;
        float newActorDistancePercentFromPlaneA = actorDistancePercentAcrossNullSpace * newRiftWidth;
        
        // Calculate the change in distance of the actor
        // (this kinda change is usually referred to as delta apparently)
        // (Think like deltaTime, it's the change of time between frames) ~Liz
        float deltaActorDistance = newActorDistancePercentFromPlaneA - distanceFromPlaneA;
        
        // Calculate the new actor position relative to the rift's normal direction (Don't forget to flip the rift normal!)
        Vector3 newActorPosition = _actorPosition + (-RiftManager.riftNormal * deltaActorDistance);
        
        DebugConsole.Log(this, $"Object: {_actorPosition} | PercentAcross: {actorDistancePercentAcrossNullSpace:F3} | " +
                               $"OldDist: {distanceFromPlaneA:F3} | NewDist: {newActorDistancePercentFromPlaneA:F3} | " +
                               $"Delta: {deltaActorDistance:F3} | NewPos: {newActorPosition}");
        
        return newActorPosition;
    }

    /// <summary>
    /// Calculate where an object in B-Space should move to if the rift scales to the given percent.
    /// </summary>
    /// <param name="_actorPosition">The current position of the actor we are moving</param>
    /// <param name="_newRiftPercent">The current percentage of how distorted the rift is</param>
    /// <returns>Returns the new position the actor should be at to avoid rift offsets</returns>
    public Vector3 MoveActorPositionWithBSpace (Vector3 _actorPosition, float _newRiftPercent)
    {
        // Calculate the new distance of the rift
        float newRiftWidth = RiftManager.riftStartingWidth * _newRiftPercent;

        // Calculate the change in distance of the actor
        // (If you are confused on what delta means here, read the similar comment in MoveActorPositionWithNullSpace)
        float deltaWidth = newRiftWidth - RiftManager.currentRiftWidth;
        
        // Calculate the new actor position relative to the rift's normal direction (Don't forget to NOT flip the rift normal!)
        Vector3 newActorPosition = _actorPosition + (RiftManager.riftNormal * deltaWidth);
        
        DebugConsole.Log(this, $"Object: {_actorPosition} | " +
                               $"CurrentWidth: {RiftManager.currentRiftWidth:F3} | NewWidth: {newRiftWidth:F3} | " +
                               $"Delta: {deltaWidth:F3} | NewPos: {newActorPosition}");

        return newActorPosition;
        
        float offset = Mathf.Abs(RiftManager.riftStartingWidth*RiftManager.currentRiftPercent)-Mathf.Abs(RiftManager.riftStartingWidth * _newRiftPercent);

        Debug.Log($"Pos {_actorPosition}, NPer {_newRiftPercent}, CRW {RiftManager.currentRiftWidth}");
        return _actorPosition - (RiftManager.riftNormal * offset);
    }

    #endregion
}
