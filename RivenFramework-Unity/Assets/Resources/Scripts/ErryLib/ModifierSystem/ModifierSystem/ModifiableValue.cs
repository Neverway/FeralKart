public interface Modifiable 
{
    public abstract bool CanBeModifiedBy(Modifier mod);
}
public abstract class Modifiable<TValue> : Modifiable
{
    public TValue Get()
    {
        SetInitialValue();
        Modifier.RunModifiableThroughActiveModifiers(this);
        return GetFinalizedValue();
    }

    protected abstract void SetInitialValue();
    protected abstract TValue GetFinalizedValue();

    public virtual bool CanBeModifiedBy(Modifier mod) => true;

    public static implicit operator TValue(Modifiable<TValue> thisObject) => thisObject.Get();
    public override string ToString() => ((TValue)this).ToString();
}

//public abstract class ActorSpecificModifiable<TValue> : Modifiable<TValue>
//{
//    public Actor RelevantActor { get; private set; }
//
//    protected ActorSpecificModifiable(Actor actor)
//    {
//        RelevantActor = actor;
//    }
//
//    public override bool CanBeModifiedBy(Modifier mod)
//    {
//        if (mod is ActorSpecificModifier)
//            return (mod as ActorSpecificModifier).actorReference.IsActorPartOfReference(RelevantActor);
//        return false;
//    }
//}