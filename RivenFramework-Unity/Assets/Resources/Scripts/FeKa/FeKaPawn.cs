using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FeKaPawn : Pawn
{
    // These values are cast back to the base pawn classes currentStats and defaultStats
    public FeKaPawnStats FeKaDefaultStats;
    public FeKaPawnStats FeKaCurrentStats => (FeKaPawnStats)currentStats;
    public FeKaPawnActions FeKaaction => (FeKaPawnActions)action;
    
    [HideInInspector] public Rigidbody physicsbody;
    [SerializeField] public GameObject interactionPrefab;
    
    public void Awake()
    {
        // Get references
        physicsbody = GetComponent<Rigidbody>();
        viewPoint = transform.Find("ViewPoint");

        defaultStats = FeKaDefaultStats;
        currentStats = (FeKaPawnStats)FeKaDefaultStats.Clone(); // Don't forget to clone so that you don't overwrite the pawns default values! ~Liz
        action = FeKaaction;
    }
    
    public bool IsGrounded()
    {
        return FeKaaction.IsOnGround(this);
    }
}
