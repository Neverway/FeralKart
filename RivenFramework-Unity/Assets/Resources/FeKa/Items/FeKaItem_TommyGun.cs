//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FeKaItem_TommyGun : ItemBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public int maxAmmo = 300;
    public GameObject bulletPrefab;
    public float fireRate = 0.05f; 


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public override int GetCharge() => ammo;
    public override bool IsExhausted() => ammo <= 0;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private int ammo;
    private float nextFireTime;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/


    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public override ItemBehaviour GetClone()
    {
        return (ItemBehaviour)this.MemberwiseClone();
    }
    
    public override bool OnPickup(FeKaPawn pawn)
    {
        var stats = pawn.FeKaCurrentStats;
        if (stats == null) return false;

        // Picking up ammo
        if (stats.utility != null && stats.utility.itemBehaviour is FeKaItem_TommyGun existingGun)
        {
            existingGun.ammo = maxAmmo;
            return false;
        }

        // Already holding an item
        if (stats.utility != null) return false;

        ammo = maxAmmo;
        return true;
    }

    public override void OnUseHeld(FeKaPawn pawn)
    {
        // Fire delay
        if (ammo <= 0 || Time.time < nextFireTime) return;

        if (bulletPrefab != null) Object.Instantiate(bulletPrefab, pawn.transform.position + pawn.transform.forward * 1.5f, pawn.transform.rotation);
        ammo--;
        nextFireTime = Time.time + fireRate;
    }


    #endregion
}
