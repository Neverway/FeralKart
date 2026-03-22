using System.Collections.Generic;

namespace ErryLib.GameEvents
{
    public static class GameEventSystem
    {
        private static GameEvent originalEvent;

        [Reload] private static List<ListensToGameEvent> eventListeners;

        public static void Register(ListensToGameEvent eventListener)
        {
            if (eventListeners == null) eventListeners = new List<ListensToGameEvent>();

            eventListeners.Add(eventListener);
        }
        public static void UnRegister(ListensToGameEvent eventListener)
        {
            if (eventListeners == null) eventListeners = new List<ListensToGameEvent>();

            eventListeners.Remove(eventListener);
        }

        /// <summary>
        /// This method is called by <see cref="GameEvent"/> both before and after calling <see cref="GameEvent{T}.WhenInvoked"/> so that all 
        /// of the Modifiers in Modifier.activeModifiers that implement ListensToGameEvent can recieve a call for that <see cref="GameEvent"/>.
        /// <para>(This should only be called through <see cref="GameEvent{T}.Invoke"/>)</para>
        /// </summary>
        /// <param name="gameEvent">The GameEvent in the process of being invoked</param>
        /// /// <param name="invokeTiming">Whether this method is being called before or after GameEvent.WhenInvoked()</param>
        public static void ProcessGameEvent<T>(T gameEvent, InvokeTiming invokeTiming) where T : GameEvent<T>
        {
            if (originalEvent == null) originalEvent = gameEvent;

            //Loop through all active modifiers and pass 'gameEvent' to any modifiers implementing ListensToGameEvent
            Modifier[] modifiers = Modifier.ActiveModifiers.ToArray();
            foreach (Modifier mod in modifiers)
                if (mod is ListensToGameEvent)
                    (mod as ListensToGameEvent).TryReactToEvent(gameEvent, invokeTiming);

            if (eventListeners != null)
                foreach(ListensToGameEvent listener in eventListeners)
                    listener.TryReactToEvent(gameEvent, invokeTiming);

            if (originalEvent == gameEvent && invokeTiming == InvokeTiming.After)
            {
                ListensToGameEvent.ClearTimeOuts();
                originalEvent = null;
            }
        }
    }
    /// <summary>
    /// Represents the timing of whether a GameEvent is ocurring before, or after GameEvent.WhenInvoked()
    /// </summary>
    public enum InvokeTiming { Before, After }
}