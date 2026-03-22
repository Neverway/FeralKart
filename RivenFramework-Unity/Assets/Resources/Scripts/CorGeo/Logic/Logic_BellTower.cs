using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Serialization;

public class Logic_BellTower : MonoBehaviour
{
    public LogicInput<bool> shortTower;
    
    public string towerName="01";
    public TowerType towerType;
    public TowerTypeObjects[] towerTypeObjectsArray;
    public Light towerLight;
    public TMP_Text[] towerLabels;
    private TowerType lastTowerType;
    public enum TowerType
    {
        unstable,
        stable,
        mixed,
    }

    public Animator animator;
    
    // Start is called before the first frame update
    void Start()
    {
        if (towerType == TowerType.stable)
        {
            animator.Play("IdleOn");
        }
        else if (towerType == TowerType.unstable)
        {
            animator.Play("IdleOff");
        }
        
        if (shortTower.HasLogicOutputSource) shortTower.CallOnSourceChanged(SetTowerShorted);
    }

    private void SetTowerShorted()
    {
        if (shortTower.Get() is false) return;
        if (towerType == TowerType.stable)
        {
            animator.Play("Shorted");
        }
        else if (towerType == TowerType.unstable)
        {
            animator.Play("PowerUp");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (towerType != lastTowerType) UpdateTowerType();

        lastTowerType = towerType;
    }
    
    public void OnValidate()
    {
        UpdateTowerType();

        // YOU are valid
    }

    public void UpdateTowerType()
    {
        foreach (var tower in towerTypeObjectsArray)
        {
            foreach (GameObject towerObject in tower.toggleObjects)
            {
                // Enable only the objects for this tower type
                towerObject.SetActive(towerType==tower.towerType);
            }

            foreach (var label in towerLabels)
            {
                label.text = towerName;
            }

            if (towerType == tower.towerType)
            {
                // Swap the substance and electricity materials
                foreach (MaterialIndex substanceMaterialToSet in tower.toggleSubstances)
                {
                    Material[] mats = substanceMaterialToSet.mesh.sharedMaterials;
                    mats[substanceMaterialToSet.index] = tower.riftSubstanceMaterial;
                    substanceMaterialToSet.mesh.sharedMaterials = mats;
                }

                foreach (MaterialIndex electricityMaterialToSet in tower.toggleElectricity)
                {
                    Material[] mats = electricityMaterialToSet.mesh.sharedMaterials;
                    mats[electricityMaterialToSet.index] = tower.electricitySubstanceMaterial;
                    electricityMaterialToSet.mesh.sharedMaterials = mats;
                }
                
                towerLight.color = tower.lightColor;
            }
        }
    }

    [Serializable]
    public struct TowerTypeObjects
    {
        [Tooltip("What kind of tower these parameters are for")]
        public TowerType towerType;
        [Tooltip("The color of the rift substance for this type of tower")]
        public Material riftSubstanceMaterial;
        [Tooltip("The color of the electricity for this type of tower")]
        public Material electricitySubstanceMaterial;
        [Tooltip("The color of the light emitted by this type of tower")]
        public Color lightColor;
        [Tooltip("The objects to be enabled for this type of tower")]
        public List<GameObject> toggleObjects;
        [Tooltip("The renderer's and the material index to override with the rift substance material")]
        public List<MaterialIndex> toggleSubstances;
        [Tooltip("The renderer's and the material index to override with the electricity substance material")]
        public List<MaterialIndex> toggleElectricity;
    }

    [Serializable]
    public struct MaterialIndex
    {
        public Renderer mesh;
        public int index;
    }
}
