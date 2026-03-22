using System;
using UnityEngine;

public enum NumberModifierType { Add, Multiply }
[Serializable]
public abstract class NumberModifier<TTarget> : Modifier where TTarget : INumberModifiable
{
    public NumberModifierType modifierType;
    public float value;

    public override void ModifyValue(Modifiable modifiableValue)
    {
        if (modifiableValue is not TTarget number)
            return;

        if (modifierType == NumberModifierType.Add)
            number.OnModify_AddNumber(value);

        if (modifierType == NumberModifierType.Multiply)
            number.OnModify_MultiplyNumber(value);
    }
}
public interface INumberModifiable : Modifiable
{
    public void OnModify_AddNumber(float toAdd);
    public void OnModify_MultiplyNumber(float toMultiply);
}
[Serializable]
public abstract class ModifiableInt : Modifiable<int>, INumberModifiable
{
    [SerializeField] protected int startValue;
    [HideInInspector] public float multiplier;
    [HideInInspector] public float addedValue;

    public ModifiableInt(int startValue) { this.startValue = startValue; }

    protected override void SetInitialValue()
    {
        addedValue = 0f;
        multiplier = 1f;
    }
    protected override int GetFinalizedValue() =>
        Mathf.RoundToInt((startValue + addedValue) * multiplier);
    public void OnModify_AddNumber(float toAdd) => addedValue += toAdd;
    public void OnModify_MultiplyNumber(float toMultiply) => multiplier *= toMultiply;
}

[Serializable]
public abstract class ModifiableFloat : Modifiable<float>, INumberModifiable
{
    [SerializeField] protected float startValue;
    [HideInInspector] public float multiplier;
    [HideInInspector] public float addedValue;

    public ModifiableFloat(float startValue) { this.startValue = startValue; }
    protected override void SetInitialValue()
    {
        addedValue = 0f;
        multiplier = 1f;
    }

    protected override float GetFinalizedValue() => (startValue + addedValue) * multiplier;
    public void OnModify_AddNumber(float toAdd) => addedValue += toAdd;
    public void OnModify_MultiplyNumber(float toMultiply) => multiplier *= toMultiply;
}
