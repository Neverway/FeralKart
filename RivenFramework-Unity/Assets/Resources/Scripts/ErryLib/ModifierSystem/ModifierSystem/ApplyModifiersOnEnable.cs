using System;
using UnityEngine;

public class ApplyModifiersOnEnable : MonoBehaviour
{
    [Polymorphic, SerializeReference] protected Modifier[] allMods = new Modifier[0];

    public void OnEnable()
    {
        foreach (Modifier mod in allMods)
            mod.RegisterModifier();
    }
    public void OnDisable()
    {
        foreach (Modifier mod in allMods)
            mod.UnregisterModifier();
    }
}