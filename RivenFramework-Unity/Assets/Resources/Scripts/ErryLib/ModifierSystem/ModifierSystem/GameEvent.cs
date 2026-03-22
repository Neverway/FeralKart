using System;

namespace ErryLib.GameEvents
{
    /// <summary>
    /// An event that happens in the game that can be listened to by <see cref="Modifier"/>s or other systems handled by <see cref="GameEventSystem"/>.
    /// <para><b>This is the base class of <see cref="GameEvent{T}"/>, If you are making a custom <see cref="GameEvent"/>, make sure
    /// that <see cref="GameEvent{T}"/> is the class you extend from instead</b></para>
    /// </summary>
    [Serializable]
    public abstract class GameEvent 
    {
        /// <summary>
        /// Flag for handling whether the <see cref="Modifier"/> has been interrupted.
        /// </summary>
        public bool isEventInterrupted { internal set; get; }

        /// <summary>
        /// This property determines whether or not your <see cref="GameEvent"/> can be interrupted via <see cref="GameEvent{}.InterruptEvent"/>
        /// </summary>
        protected abstract bool IsInterruptable { get; }

        /// <summary>
        /// <b>This methods behaviour is defined in <seealso cref="GameEvent{T}.Invoke"/>.</b>
        /// <para>Call this to trigger the <see cref="GameEvent"/>. This will call <see cref="WhenInvoked"/> as well as calling 
        /// <see cref="ListensToGameEvent{T}.ReactToEvent(T, InvokeTiming)"/>. on all <see cref="Modifier.ActiveModifiers"/> where T is 
        /// this GameEvent.</para>
        /// </summary>
        public abstract void Invoke();


        /// <summary>
        /// This method is called during <see cref="Invoke"/> and is meant to define the behaviour for the event itself.
        /// <para><b>Note: <see cref="Modifier"/>s listening to this <see cref="GameEvent"/> will have a chance to react directly before and 
        /// after this method is called, so its important to make sure the behaviour for the event happens inside of this method</b></para>
        /// </summary>
        protected abstract void WhenInvoked();
    }

    /// <summary>
    /// An event that happens in the game that can be listened to by <see cref="Modifier"/>s or other systems handled by <see cref="GameEventSystem"/>
    /// </summary>
    /// <typeparam name="TSelf"> This is a type reference to the type that derived from this class. Any class that extends this class should
    /// pass itself as this generic type</typeparam>
    [Serializable]
    public abstract class GameEvent<TSelf> : GameEvent where TSelf : GameEvent<TSelf>
    {
    

        /// <summary>
        /// Interrupts this <see cref="GameEvent"/> from invoking. This will only have effect after <see cref="GameEvent.Invoke"/> gets 
        /// called and before it calls <see cref="GameEvent.WhenInvoked"/>. The main place this will be used is during 
        /// <see cref="ListensToGameEvent{T}.ReactToEvent(T, InvokeTiming)"/> where the <see cref="InvokeTiming"/> is set to <see cref="InvokeTiming.Before"/>
        /// <para><b>If you wish to make your <see cref="GameEvent"/> uninterruptable, or, implement specific behaviour during interrupt, you can
        /// override this method and implement that behaviour</b></para>
        /// </summary>
        public virtual void InterruptEvent() => isEventInterrupted = IsInterruptable;


        /// <summary>
        /// Call this to trigger the <see cref="GameEvent"/>. This will call <see cref="GameEvent.WhenInvoked"/> as well as calling 
        /// <see cref="ListensToGameEvent{T}.ReactToEvent(T, InvokeTiming)"/>. on all <see cref="Modifier.ActiveModifiers"/> where T is 
        /// this GameEvent
        /// </summary>
        public override void Invoke()
        {
            //Process GameEvent through GameEventSystem BEFORE calling WhenInvoked(). InterruptEvent() could be called during this so check for that
            isEventInterrupted = false;
            GameEventSystem.ProcessGameEvent(this as TSelf, InvokeTiming.Before);
            if (isEventInterrupted) return;

            //Actually DO the implemented GameEvent behaviour
            WhenInvoked();

            //Process GameEvent through GameEventSystem AFTER calling WhenInvoked()
            GameEventSystem.ProcessGameEvent(this as TSelf, InvokeTiming.After);
        }


        /// <summary>
        /// This will call <see cref="GameEvent.WhenInvoked"/> bypassing the <see cref="GameEventSystem"/>. The <see cref="GameEvent"/> will 
        /// happen, but nothing listening to the <see cref="GameEvent"/> will know that this event triggered. 
        /// <para>(Should probably only be used for testing purposes)</para>
        /// </summary>
        public void InvokeWithoutGameEventSystem() => WhenInvoked();
    }
}