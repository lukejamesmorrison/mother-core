using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// The EventBus class is a core module that manages events and their subscriptions 
    /// across the system. Modules may emit events, and other modules may subscribe 
    /// to those events for further action when they occur.
    /// </summary>
    public class EventBus : BaseCoreModule
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public EventBus(Mother mother) : base (mother) { }

        /// <summary>
        /// All event subscriptions. The key is the event name, and the 
        /// value is a list of modules subscribed to that event.
        /// </summary>
        private readonly Dictionary<Type, List<IModule>> EventSubscriptions = new Dictionary<Type, List<IModule>>();

        /// <summary>
        /// Subscribe a Module to a specific event.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="module"></param>
        public void Subscribe<TEvent>(IModule module) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);

            if (!EventSubscriptions.ContainsKey(eventType))
                EventSubscriptions[eventType] = new List<IModule>();

            EventSubscriptions[eventType].Add(module);
        }

        /// <summary>
        /// Check if a Module is subscribed to a specific event.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="module"></param>
        public bool IsSubscribed<TEvent>(IModule module)
        {
            var eventType = typeof(TEvent);
            if (EventSubscriptions.ContainsKey(eventType))
                return EventSubscriptions[eventType].Contains(module);

            return false;
        }

        /// <summary>
        /// Unsubscribe a Module from a specific event.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="module"></param>
        public void Unsubscribe<TEvent>(IModule module) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);

            if (EventSubscriptions.ContainsKey(eventType))
                EventSubscriptions[eventType].Remove(module);
        }

        /// <summary>
        /// Emit an event of a specific type with optional event data.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="eventData"></param>
        public new void Emit<TEvent>(object eventData = null) where TEvent : IEvent, new()
        {
            Emit(new TEvent(), eventData);
        }

        /// <summary>
        /// Emit an event to all subscribed Modules with optional event data.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public new void Emit(IEvent e, object eventData = null)
        {
            var eventType = e.GetType();

            if (EventSubscriptions.ContainsKey(eventType))
                EventSubscriptions[eventType].ForEach(module => module.HandleEvent(e, eventData));
        }
    }
}
