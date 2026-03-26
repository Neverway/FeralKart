using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSpawnerTrigger : MonoBehaviour
{
    public ItemSpawner itemSpawner;
    
    private void OnTriggerEnter(Collider other)
    {
        FeKaPawn feKaPawn =  other.GetComponentInParent<FeKaPawn>();
        if (feKaPawn)
        {
            //if (feKaPawn.FeKaCurrentStats.controlMode == ControlMode.LocalPlayer)
            //{
                itemSpawner.PickupItem(feKaPawn);
            //}
        }
    }
}
