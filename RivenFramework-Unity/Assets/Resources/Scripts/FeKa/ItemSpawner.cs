using System;
using System.Collections;
using System.Collections.Generic;
using ErryLib.Reflection;
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
        
        icon.sprite = item.details.icon;
        itemAvailable = true;
        ring.color = FeKaItem.GetRarityColor(item.details.rarity);
        glow.color = FeKaItem.GetRarityColor(item.details.rarity);
        beam.color = FeKaItem.GetRarityColor(item.details.rarity);
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
        yield return new WaitForSeconds(item.details.respawnTime);
        itemAvailable = true;
        icon.gameObject.SetActive(true);
        ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f);
        glow.color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f);
        beam.color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f);
    }

    public void PickupItem(FeKaPawn pawn)
    {
        if (!itemAvailable) return;
        var itemRef = item.Clone(); // doing item.Clone allows the scriptable object to remain unmodified
        if (itemRef?.itemBehaviour == null) return;

        bool occupiesSlot = itemRef.itemBehaviour.OnPickup(pawn);
        
        if (occupiesSlot)
        {
            if (pawn.FeKaCurrentStats.utility != null) return;
            pawn.FeKaCurrentStats.utility = itemRef;
        }
        
        StartCoroutine(WaitForRespawn());
    }
}
