using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RivenFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WB_HUD : MonoBehaviour
{
    public FeKaPawn targetFeKaPawn;
    public bool findPossessedPawn;

    public TMP_Text timer;
    public TMP_Text lap;
    public TMP_Text speed;
    public TMP_Text placement;
    
    public Image characterPortrait;
    public Image shieldBar;
    public Image healthBar;
    public TMP_Text shieldText;
    public TMP_Text healthText;
    public GameObject stockShelf;
    public GameObject stockReference;
    public UI_Ability finalStrike;
    public UI_Ability primary;
    public UI_Ability utility;

    private float previousHealth;
    private float previousShield;
    private List<Image> stockImages = new List<Image>(0);
    private GI_RaceManager raceManager;

    private void Start()
    {
        raceManager = GameInstance.Get<GI_RaceManager>();
    }

    void Update()
    {
        // Pawn reference check
        if (findPossessedPawn)
        {
            targetFeKaPawn = GameInstance.Get<GI_PawnManager>().localPlayerCharacter.GetComponent<FeKaPawn>();
        }
        if (!targetFeKaPawn) return;
        
        // Update all the indicators
        UpdateTimer();
        UpdateShield();
        UpdateHealth();
        UpdateStocks();
        UpdateAbilities();
    }

    private void UpdateTimer()
    {
        timer.text = ($"{raceManager.timeRemaining:f2}");
        lap.text = ($"Lap {targetFeKaPawn.FeKaCurrentStats.currentLap}/{raceManager.totalLaps}");
        speed.text = ($"{targetFeKaPawn.physicsbody.velocity.magnitude:f2} m/s");

        int placementNum = raceManager.GetRacerPlace(targetFeKaPawn);
        if (placementNum == 1)
        {
            placement.text = ($"1st");
            placement.color = new Color(1f, 0.8f, 0.3f, 1);
        }
        else if (placementNum == 2)
        {
            placement.text = ($"2nd");
            placement.color = new Color(0.6f, 0.8f, 0.9f, 1);
        }

        else if (placementNum == 3)
        {
            placement.text = ($"3rd");
            placement.color = new Color(0.6f, 0.3f, 0.2f, 1);
        }
        else
        {
            placement.text = ($"{placementNum}th");
            placement.color = new Color(0.8f, 0.8f, 0.8f, 1);
        }
    }
    private void UpdateShield()
    {
        // Update shield text
        shieldText.text = $"{(targetFeKaPawn.FeKaCurrentStats.shield / targetFeKaPawn.FeKaCurrentStats.MaxShield) * 100}";
        
        // Animate shield bar on change
        if (targetFeKaPawn.FeKaCurrentStats.shield != previousShield)
        {
            shieldBar.DOKill();
            shieldBar.fillAmount=((targetFeKaPawn.FeKaCurrentStats.shield / targetFeKaPawn.FeKaCurrentStats.MaxShield) * 100 * 0.01f);
        }
        previousShield = targetFeKaPawn.FeKaCurrentStats.shield;
    }
    private void UpdateHealth()
    {
        // Update health text
        healthText.text = $"{(targetFeKaPawn.FeKaCurrentStats.health / targetFeKaPawn.FeKaDefaultStats.health) * 100}";
        
        // Animate health bar on change
        if (targetFeKaPawn.FeKaCurrentStats.health != previousHealth)
        {
            healthBar.DOKill();
            healthBar.fillAmount=((targetFeKaPawn.FeKaCurrentStats.health / targetFeKaPawn.FeKaDefaultStats.health) * 100 * 0.01f);
        }
        previousHealth = targetFeKaPawn.FeKaCurrentStats.health;
    }
    private void UpdateStocks()
    {
        if (stockImages.Count != targetFeKaPawn.FeKaCurrentStats.stocks)
        {
            // Clear stock images
            for (int i = 0; i < stockImages.Count; i++)
            {
                Destroy(stockImages[i]);
            }
            stockImages.Clear();
            
            // Add images for each stock
            for (int i = 0; i < targetFeKaPawn.FeKaCurrentStats.stocks; i++)
            {
                var newStock = Instantiate(stockReference, stockShelf.transform).GetComponent<Image>();
                stockImages.Add(newStock);
                newStock.gameObject.SetActive(true);
            }
        }
    }
    private void UpdateAbilities()
    {
        if (targetFeKaPawn.FeKaCurrentStats.utility)
        {
            utility.icon.enabled = true;
            utility.icon.sprite = targetFeKaPawn.FeKaCurrentStats.utility.details.icon;
            utility.bar.color = FeKaItem.GetRarityColor(targetFeKaPawn.FeKaCurrentStats.utility.details.rarity);
            utility.bar.fillAmount = targetFeKaPawn.FeKaCurrentStats.utilityCharge;
            utility.text.text = targetFeKaPawn.FeKaCurrentStats.utility.details.itemName;
            utility.quantity.text = ($"{targetFeKaPawn.FeKaCurrentStats.utility.itemBehaviour.GetCharge()}");
        }
        else
        {
            utility.icon.enabled = false;
            utility.bar.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            utility.text.text = "";
            utility.quantity.text = "";
        }
    }
}

[Serializable]
public struct UI_Ability
{
    public Image icon;
    public Image bar;
    public Image keyhint;
    public TMP_Text text;
    public TMP_Text quantity;
}