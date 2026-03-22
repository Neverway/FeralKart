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

[RequireComponent(typeof(TeslaConductor))]
public class TeslaReceiver : MonoBehaviour
{
    //=-----------------=
    // Reference Variables
    //=-----------------=
    public LogicOutput<bool> isPowered = new(false);
    [SerializeField] private GameObject lightObject;
    private TeslaConductor conductor;

    //=-----------------=
    // Internal Functions
    //=-----------------=
    private void Awake()
    {
        conductor = GetComponent<TeslaConductor>();
    }

    private void OnDisable()
    {
        isPowered.Set(false);
    }

    private void Update()
    {
        lightObject.SetActive(conductor.IsTeslaPowered());
        isPowered.Set(conductor.IsTeslaPowered());
    }

    //=-----------------=
    // External Functions
    //=-----------------=
}