//===================== (Neverway 2024) Written by Liz M. =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
    
public class FPPawn : Pawn
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    [HideInInspector] public List<Pawn> visiblePawns = new List<Pawn>();
    [HideInInspector] public List<Pawn> visibleHostiles = new List<Pawn>();
    [HideInInspector] public List<Pawn> visibleAllies = new List<Pawn>();


    //=-----------------=
    // Private Variables
    //=-----------------=


    //=-----------------=
    // Reference Variables
    //=-----------------=
    // These values are cast back to the base pawn classes currentStats and defaultStats
    public FPPawnStats FPDefaultStats;
    public FPPawnStats FPCurrentStats => (FPPawnStats)currentStats;
    public FPPawnActions FPaction => (FPPawnActions)action;
    
    [HideInInspector] public Rigidbody physicsbody;
    [SerializeField] public GameObject interactionPrefab;


    //=-----------------=
    // Mono Functions
    //=-----------------=
    public virtual void Awake()
    {
        // Get references
        physicsbody = GetComponent<Rigidbody>();
        viewPoint = transform.Find("ViewPoint");

        defaultStats = FPDefaultStats;
        currentStats = (FPPawnStats)FPDefaultStats.Clone(); // Don't forget to clone so that you don't overwrite the pawns default values! ~Liz
        action = FPaction;
    }
    

    //=-----------------=
    // Internal Functions
    //=-----------------=


    //=-----------------=
    // External Functions
    //=-----------------=
    public bool IsGrounded()
    {
        return FPaction.IsOnGround(this);
    }
}