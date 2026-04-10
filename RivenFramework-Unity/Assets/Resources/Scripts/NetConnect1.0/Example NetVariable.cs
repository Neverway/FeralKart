using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleNetVariable : MonoBehaviour
{
    private NetVariable<float> currentHealth;
    
    // Start is called before the first frame update
    void Start()
    {
        var owner = GetComponent<NetVariableOwner>();
        currentHealth = owner.Register<float>("currentHealth", 100f, OnHealthChanged);
    }

    private void OnHealthChanged(float newValue)
    {
        // All of this code fires on all clients when the health is changed by the owner
        // UpdateHealthBar(newValue);
    }

    private void TakeDamage(float amount)
    {
        // This should only be fired by the owner
        currentHealth.Value -= amount;
    }
}
