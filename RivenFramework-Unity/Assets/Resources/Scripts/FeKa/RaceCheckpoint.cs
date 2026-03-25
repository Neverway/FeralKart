using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceCheckpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        FeKaPawn feKaPawn =  other.GetComponentInParent<FeKaPawn>();
        if (feKaPawn)
        {
            if (feKaPawn.FeKaCurrentStats.controlMode == ControlMode.LocalPlayer)
            {
                FindObjectOfType<CheckpointTracker>().NextCheckpoint(feKaPawn);
            }
        }
    }
}
