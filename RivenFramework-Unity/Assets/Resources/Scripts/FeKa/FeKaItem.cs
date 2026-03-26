using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "FeKaItem_", menuName = "FeKa/FeKaItem")]
public class FeKaItem : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int usages;
    public bool consumable;
    public float respawnTime;
    public Rarity rarity;
    

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

public enum Rarity
{
    common,
    uncommon,
    rare,
    legendary,
    divine
}