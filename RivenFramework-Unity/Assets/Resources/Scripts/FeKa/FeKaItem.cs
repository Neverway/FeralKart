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

    public FeKaItemDetails details;
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
    public string itemName;
    public Sprite icon;
    public float respawnTime;
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
}

public enum Rarity
{
    common,
    uncommon,
    rare,
    legendary,
    divine
}