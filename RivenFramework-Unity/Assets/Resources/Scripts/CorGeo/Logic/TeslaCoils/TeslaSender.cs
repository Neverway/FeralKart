//===================== (Neverway 2024) Written by Connorses =====================
//
// Purpose:
// Notes:
//
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Neverway.Framework.LogicSystem;

//todo: make a manager that Lists senders and receivers and calls them accordionly
[RequireComponent(typeof(TeslaConductor))]
public class TeslaSender : MonoBehaviour, TeslaPowerSource
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public LogicInput<bool> inputSignal;

    //=-----------------=
    // Reference Variables
    //=-----------------=
    [SerializeField] private GameObject lightObject;
    private TeslaConductor conductor;

    public bool IsTeslaPowered() => inputSignal == null || inputSignal.Get();

    //=-----------------=
    // Internal Functions
    //=-----------------=
    private void Awake()
    {
        conductor = GetComponent<TeslaConductor>();
    }

    public void Update()
    {
        conductor.SetPowerSource(this);
        lightObject.SetActive(conductor.IsTeslaPowered());
    }

    public Transform GetZapTargetTransform() => transform;
}