using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ErryLib.Reflection;
using UnityEditor;
using UnityEngine;

public class GI_ReplayEventTimeline : MonoBehaviour
{
    public bool isRecording;
    public bool isPlaying;
    public float recordStartTime;
    public List<ReplayEvent> replayEvents = new List<ReplayEvent>();


    #region Main Controls

    public void StartRecording()
    {
        replayEvents.Clear();
        isRecording = true;
        recordStartTime = Time.time;
    }

    public void StopRecording()
    {
        isRecording = false;
    }

    public void PlayRecording()
    {
        ReplayEvent[] events = replayEvents.ToArray();

        StartCoroutine(PlayEvents(events));
    }

    #endregion
    
    /*public bool RecordThisEvent<T>(T eventCaller, [CallerMemberName]string methodName = "")
    {
        return RecordThisEvent(eventCaller, methodParameters:null);
    }*/
    
    /*public bool RecordThisEvent<T>(T eventCaller, object[] methodParameters, [CallerMemberName]string methodName = "")
    {
        if (isPlaying) return true;
        if (!isRecording) return false;
        replayEvents.Add(new ReplayEvent
        {
            eventCallerType = eventCaller.GetType(),
            eventCaller = eventCaller,
            methodParameters = methodParameters,
            methodName = methodName,
            time = Time.time-recordStartTime
        });
        return false;
    }*/

    public void PlayThisEvent(ReplayEvent replayEvent)
    {
        bool wasPlaying = isPlaying;
        isPlaying = false;
        replayEvent.eventCallerType.GetMethod(replayEvent.methodName).Invoke(replayEvent.eventCaller, replayEvent.methodParameters);
        isPlaying = wasPlaying;
        
        //Debug.Log($"Event {replayEvent.methodName} occurred at {Time.time} with parameters {string.Join(",", replayEvent.methodParameters)}");
    }

    private IEnumerator PlayEvents(ReplayEvent[] events)
    {
        isPlaying = true;
        var replayTime = Time.time;
        
        for (int i = 0; i < events.Length; i++)
        {
            while ((events[i].time + replayTime) <= Time.time)
            {
                PlayThisEvent(events[i]);
            }
            yield return null;
        }

        isPlaying = false;
    }
    
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            StartRecording();
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            StopRecording();
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            PlayRecording();
        }
    }

}

[Serializable]
public struct ReplayEvent
{
    public Type eventCallerType; // The type of the object that called this
    public object eventCaller; // What object called this
    public object[] methodParameters; // What parameters were passed
    public string methodName; // What method was called
    public float time; // When this event had happened
}
