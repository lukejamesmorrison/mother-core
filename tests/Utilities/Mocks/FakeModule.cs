using IngameScript;
using System.Collections.Generic;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Generic fake module for tests. It derives from <see cref="BaseModule"/>
    /// so it stays aligned with the same module lifecycle and defaults used by
    /// production modules, while recording deliveries of a specific event type.
    /// </summary>
    public class FakeModule<TEvent> : BaseModule, IInvocationObserver
        where TEvent : IEvent
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public FakeModule(Mother mother) : base(mother) { }

        /// <summary>
        /// Every matching event observed in delivery order.
        /// </summary>
        public List<TEvent> Events { get; } = new List<TEvent>();

        /// <summary>
        /// Every matching eventData payload observed in delivery order.
        /// </summary>
        public List<object> EventData { get; } = new List<object>();

        /// <summary>
        /// The number of matching event deliveries captured by this fake module.
        /// </summary>
        public int InvocationCount => Events.Count;

        /// <summary>
        /// The most recently observed event instance, or <see langword="null"/> when none were seen.
        /// </summary>
        public TEvent LastEvent => Events.Count == 0 ? default(TEvent) : Events[Events.Count - 1];

        /// <summary>
        /// The most recently observed eventData payload, or <see langword="null"/> when none were seen.
        /// </summary>
        public object LastEventData => EventData.Count == 0 ? null : EventData[EventData.Count - 1];

        /// <summary>
        /// The name of this module, used for logging and diagnostics. By default, the base module 
        /// implementation returns the class name, but here we override it to include 
        /// the event type for easier identification in tests.
        /// </summary>
        /// <returns></returns>
        public override string GetModuleName()
        {
            return $"FakeModule<{typeof(TEvent).Name}>";
        }

        /// <summary>
        /// Handles an event delivery by checking if the event is of the expected type, and if so, recording 
        /// it along with its eventData payload. This allows tests to verify that the module received 
        /// the correct events with the correct data in the expected order. 
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            if (!(e is TEvent typedEvent))
                return;

            Events.Add(typedEvent);
            EventData.Add(eventData);
        }
    }
}