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
    
    public virtual void Awake()
    {
        // Get references
        physicsbody = GetComponent<Rigidbody>();
        if (viewPoint == null) viewPoint = transform.Find("ViewPoint");

        defaultStats = FeKaDefaultStats;
        currentStats = (FeKaPawnStats)FeKaDefaultStats.Clone(); // Don't forget to clone so that you don't overwrite the pawns default values! ~Liz
    }
    
    public bool IsGrounded()
    {
        return FeKaaction.IsOnGround(this);
    }
}
