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
using RivenFramework;
using UnityEngine;

[System.Serializable]
public class FeKaItem_RocketLauncher : ItemBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public int maxAmmo = 3;
    public GameObject rocketPrefab;
    public GameObject targetingUIPrefab;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/
    public override int GetCharge() => ammo;
    public override bool IsExhausted() => ammo <= 0;
    public bool HasLock() => lockedTarget != null;


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private int ammo;
    private UI_RocketTargeting activeTargeting;
    private GI_RaceManager raceManager;
    private FeKaPawn lockedTarget = null;
    private float lockTimer = 0f;
    public float lockDelay = 0.5f;
    private bool isHolding = false;



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
        raceManager = GameInstance.Get<GI_RaceManager>();

        // Picking up ammo
        if (stats.utility != null && stats.utility.itemBehaviour is FeKaItem_RocketLauncher existingGun)
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
        if (ammo <= 0) return;

        raceManager ??= GameInstance.Get<GI_RaceManager>();
        if (raceManager == null || raceManager.racers == null) return;

        var allRacers = new List<FeKaPawn>();
        foreach (var r in raceManager.racers)
            if (r != pawn) allRacers.Add(r);

        var bpawn = (FeKaPawn_Base)pawn;
        if (bpawn.controlMode == ControlMode.LocalPlayer && activeTargeting == null)
        {
            var go = Object.Instantiate(targetingUIPrefab);
            activeTargeting = go.GetComponent<UI_RocketTargeting>();
            if (activeTargeting == null) { Object.Destroy(go); return; }
            activeTargeting.candidates = allRacers;
        }

        isHolding = true;
    }
    
    public override void OnUpdate(FeKaPawn pawn)
    {
        if (!isHolding) return;

        var bpawn = (FeKaPawn_Base)pawn;
        if (bpawn.controlMode == ControlMode.LocalPlayer)
        {
            if (activeTargeting != null) lockedTarget = activeTargeting.lockedTarget;
            return;
        }

        if (bpawn.controlMode == ControlMode.CPU)
        {
            raceManager ??= GameInstance.Get<GI_RaceManager>();
            if (raceManager == null) return;

            FeKaPawn best = null;
            var bestDot = -1f;

            foreach (var r in raceManager.racers)
            {
                if (r == pawn) continue;

                var toRacer = (r.transform.position - pawn.transform.position).normalized;
                var dot = Vector3.Dot(pawn.transform.forward, toRacer);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = r;
                }
            }

            if (best != null && bestDot > 0.6f)
            {
                lockTimer += Time.deltaTime;
                if (lockTimer >= lockDelay)
                    lockedTarget = best;
            }
            else
            {
                lockTimer = 0f;
                lockedTarget = null;
            }
        }
    }

    public override void OnUseReleased(FeKaPawn pawn)
    {
        if (ammo <= 0) return;

        if (activeTargeting != null)
        {
            Object.Destroy(activeTargeting.gameObject);
            activeTargeting = null;
        }
        
        var spawnPos = pawn.FeKaCurrentStats.projectileSpawnPoint.position + pawn.transform.forward * 1.5f;
        var spawnRot = pawn.transform.rotation;

        // Fire
        NetSpawner.Spawn("Rocket", spawnPos, spawnRot, (rocketObject, networkId) =>
        {

            var homing = rocketObject.GetComponent<HomingRocket>();
            if (homing != null)
            {
                homing.exemptPawns.Add(pawn);
                homing.SetTarget(lockedTarget);
            }
        });

        lockedTarget = null;
        lockTimer = 0f;
        isHolding = false;
        ammo--;
    }


    #endregion
}
