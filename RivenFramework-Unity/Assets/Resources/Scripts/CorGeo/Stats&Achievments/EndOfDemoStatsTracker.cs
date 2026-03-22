using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndOfDemoStatsTracker : MonoBehaviour
{
    public bool clearStatsOnAwakeInstead = false;

    public int buddies = 0;
    public int jumps = 0;
    public int bulbs = 0;
    public bool doTracking = true;

    public static EndOfDemoStatsTracker instance;

    public void Awake()
    {
        if (clearStatsOnAwakeInstead)
        {
            buddies = 0;
            jumps = 0;
            bulbs = 0;
            doTracking = true;
            Destroy(this);
            return;
        }

        if (instance == null)
            instance = this;
        else
            Destroy(this);
    }

    public void AddJumpCount()
    {
        if (doTracking)
            jumps++;
    }
    public void AddBulbCount()
    {
        if (doTracking)
            bulbs++;
    }
    public void UpdateBuddies(int newBuddiesCount)
    {
        buddies = newBuddiesCount;
    }
    public void StopTracking()
    {
        doTracking = false;
    }
}
