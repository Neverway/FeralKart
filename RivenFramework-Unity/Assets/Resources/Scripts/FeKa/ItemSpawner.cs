using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public SpriteRenderer icon, ring, glow, beam;
    public FeKaItem item;
    public bool itemAvailable = true;

    private void Start()
    {
        if (item == null)
        {
            SetSpawnerDisabled();
            return;
        }
        
        icon.sprite = item.icon;
        itemAvailable = true;
        ring.color = FeKaItem.GetRarityColor(item.rarity);
        glow.color = FeKaItem.GetRarityColor(item.rarity);
        beam.color = FeKaItem.GetRarityColor(item.rarity);
    }

    private void SetSpawnerDisabled()
    {
        ring.color = FeKaItem.GetRarityColor();
        glow.color = FeKaItem.GetRarityColor();
        beam.color = FeKaItem.GetRarityColor();
        itemAvailable = false;
        icon.gameObject.SetActive(false);
        ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0.5f);
        glow.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0.25f);
        beam.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0.5f);
    }

    private IEnumerator WaitForRespawn()
    {
        itemAvailable = false;
        icon.gameObject.SetActive(false);
        ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0.5f);
        glow.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0.25f);
        beam.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0.5f);
        yield return new WaitForSeconds(item.respawnTime);
        itemAvailable = true;
        icon.gameObject.SetActive(true);
        ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f);
        glow.color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f);
        beam.color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f);
    }

    public void PickupItem(FeKaPawn pawn)
    {
        if (itemAvailable is false) return;

        if (item.consumable)
        {
            StartCoroutine(WaitForRespawn());
            // TODO add code here to auto use the item
            return;
        }
        
        if (pawn.FeKaCurrentStats.utility != null) return;

        pawn.FeKaCurrentStats.utility = item;
        pawn.FeKaCurrentStats.utilityUsages = item.usages;
        
        StartCoroutine(WaitForRespawn());
    }
}
