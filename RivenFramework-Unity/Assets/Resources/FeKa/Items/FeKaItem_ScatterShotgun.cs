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
public class FeKaItem_ScatterShotgun : ItemBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public int maxAmmo = 300;
    public GameObject bulletPrefab;
    public float fireRate = 0.05f; 
    public DamageSource damageSource;
    public float bulletSpread = 0.2f;


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
        if (stats.utility != null && stats.utility.itemBehaviour is FeKaItem_ScatterShotgun existingGun)
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
        Debug.Log($"[TommyGun] OnUseHeld called. ammo={ammo}, time={Time.time}, nextFireTime={nextFireTime}");
        // Fire delay
        if (ammo <= 0 || Time.time < nextFireTime) return;
        
        // Top row
        SpawnBullet(pawn,new Vector3(0.25f,0.25f,0.00f),new Vector3(1.00f,1.00f,0.00f));
        SpawnBullet(pawn,new Vector3(0.00f,0.25f,0.00f),new Vector3(0.00f,1.00f,0.00f));
        SpawnBullet(pawn,new Vector3(-0.25f,0.25f,0.00f),new Vector3(-1.00f,1.00f,0.00f));

        // Middle row
        SpawnBullet(pawn,new Vector3(0.25f,0.00f,0.00f),new Vector3(1.00f,0.00f,0.00f));
        SpawnBullet(pawn,new Vector3(0.00f,0.00f,0.00f),new Vector3(0.00f,0.00f,0.00f));
        SpawnBullet(pawn,new Vector3(-0.25f,0.00f,0.00f),new Vector3(-1.00f,0.00f,0.00f));

        // Bottom row
        SpawnBullet(pawn,new Vector3(0.25f,-0.25f,0.00f),new Vector3(1.00f,-1.00f,0.00f));
        SpawnBullet(pawn,new Vector3(0.00f,-0.25f,0.00f),new Vector3(0.00f,-1.00f,0.00f));
        SpawnBullet(pawn,new Vector3(-0.25f,-0.25f,0.00f),new Vector3(-1.00f,-1.00f,0.00f));
        
        ammo--;
        nextFireTime = Time.time + fireRate;
    }

    private void SpawnBullet(FeKaPawn pawn, Vector3 positionOffset, Vector3 spreadDirection)
    {
        var baseForward = pawn.FeKaCurrentStats.projectileSpawnPoint.forward * 1.5f;
        var spawnPos = pawn.FeKaCurrentStats.projectileSpawnPoint.position + baseForward + positionOffset;

        Vector3 finalDirection = spreadDirection * bulletSpread;
        var spawnRot = Quaternion.LookRotation(finalDirection);

        // Fire
        NetSpawner.Spawn(bulletPrefab.name, spawnPos, spawnRot, (bulletObject, networkId) =>
        {
            var homing = bulletObject.GetComponent<HomingRocket>();
            if (homing != null)
            {
                homing.exemptPawns.Add(pawn);
                homing.damageInfo.instigator = pawn;
                homing.damageInfo.type = DamageType.Explosive;
                homing.damageInfo.source = damageSource;
            }
        });
    }

    public override void Reset()
    {
        ammo = maxAmmo;
        nextFireTime = 0f;
    }


    #endregion
}
