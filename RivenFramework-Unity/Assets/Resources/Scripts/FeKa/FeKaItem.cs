using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FeKaItem_", menuName = "FeKa/FeKaItem")]
[Serializable]
public class FeKaItem : ScriptableObject
{
    public FeKaItem Clone()
    {
        FeKaItem clone = ScriptableObject.CreateInstance<FeKaItem>();
        clone.details = details;
        clone.itemBehaviour = itemBehaviour.GetClone();
        return clone;
    }

    [Box]public FeKaItemDetails details;
    [SerializeReference, Polymorphic] public ItemBehaviour itemBehaviour;
    

    public static Color GetRarityColor(Rarity rarity=Rarity.common)
    {
        switch (rarity)
        {
            case Rarity.common:
                return new Color(0.6f, 0.6f, 0.6f);
                break;
            case Rarity.uncommon:
                return new Color(0.5f, 0.9f, 0.5f);
                break;
            case Rarity.rare:
                return new Color(0.2f, 0.7f, 0.9f);
                break;
            case Rarity.legendary:
                return new Color(0.9f, 0.6f, 0.2f);
                break;
            case Rarity.divine:
                return new Color(0.9f, 0.2f, 0.9f);
                break;
            default:
                return new Color(0.6f, 0.6f, 0.6f);
        }
    }
}

[Serializable]
public struct FeKaItemDetails
{
    [Tooltip("The name of the item")]
    public string itemName;
    [Tooltip("The description of the item")]
    [TextArea]
    public string itemDescription;
    [Tooltip("The icon of the item")]
    public Sprite icon;
    [Tooltip("When this item is in a pickup field, this is how long it takes for the item to respawn")]
    public float respawnTime;
    [Tooltip("If this item is being used as a character ability or final strike, this is the amount of charge required to get the item")]
    public float chargeAmount;
    [Tooltip("If this item is being used as a character ability or final strike, this is the amount that is given to the charge every second")]
    public float passiveRecharge;
    [Tooltip("This is just a fancy color for the item")]
    public Rarity rarity;
}

[Serializable]
public abstract class ItemBehaviour
{    
    public abstract ItemBehaviour GetClone();
    
    public abstract bool OnPickup(FeKaPawn pawn);

    public virtual void OnUpdate(FeKaPawn pawn) { }

    public virtual void OnUseHeld(FeKaPawn pawn) { }

    public virtual void OnUseReleased(FeKaPawn pawn) { }

    public virtual int GetCharge() => -1;

    public virtual bool IsExhausted() => false;
    
    public virtual void Reset() { }
}

public enum Rarity
{
    common,
    uncommon,
    rare,
    legendary,
    divine
}