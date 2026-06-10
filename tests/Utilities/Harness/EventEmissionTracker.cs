using IngameScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Records emitted events and the modules subscribed to them at emission time.
    /// This lets tests assert on event traffic without explicit observer setup.
    /// </summary>
    public class EventEmissionTracker : BaseModule
    {
        readonly EventBus _eventBus;
        readonly FieldInfo _subscriptionsField;

        /// <summary>
        /// Initializes a tracker bound to the supplied <see cref="EventBus"/>.
        /// </summary>
        /// <param name="mother">The booted Mother instance under test.</param>
        /// <param name="eventBus">The event bus whose emissions should be recorded.</param>
        public EventEmissionTracker(Mother mother, EventBus eventBus) : base(mother)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _subscriptionsField = typeof(EventBus).GetField("EventSubscriptions",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        /// <summary>
        /// Every event observed by this tracker in emission order.
        /// </summary>
        public List<EventEmissionRecord> Emissions { get; } = new List<EventEmissionRecord>();

        /// <summary>
        /// Subscribes this tracker to every concrete <see cref="IEvent"/> type that is
        /// currently loadable in the AppDomain.
        /// </summary>
        public void SubscribeToKnownEvents()
        {
            var subscribeMethod = typeof(EventBus)
                .GetMethods()
                .Single(method => method.Name == "Subscribe"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(IModule));

            foreach (var eventType in GetKnownEventTypes())
            {
                subscribeMethod.MakeGenericMethod(eventType)
                    .Invoke(_eventBus, new object[] { this });
            }
        }

        public override string GetModuleName()
        {
            return nameof(EventEmissionTracker);
        }

        /// <summary>
        /// Records an emitted event together with the subscriber snapshot that would
        /// receive it through the event bus.
        /// </summary>
        /// <param name="e">The emitted event instance.</param>
        /// <param name="eventData">Optional event payload.</param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            Emissions.Add(new EventEmissionRecord(
                e,
                eventData,
                GetRecipientsFor(e.GetType())));
        }

        /// <summary>
        /// Finds every concrete event type currently available to the test run.
        /// </summary>
        static IEnumerable<Type> GetKnownEventTypes()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .SelectMany(GetLoadableTypes)
                .Where(type => typeof(IEvent).IsAssignableFrom(type)
                    && type.IsClass
                    && !type.IsAbstract)
                .Distinct();
        }

        /// <summary>
        /// Returns the loadable types from an assembly, tolerating partial type-load failures.
        /// </summary>
        static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }

        /// <summary>
        /// Captures the currently subscribed recipients for the supplied event type,
        /// excluding this tracker itself.
        /// </summary>
        HashSet<IModule> GetRecipientsFor(Type eventType)
        {
            var subscriptions = (Dictionary<Type, HashSet<IModule>>)_subscriptionsField.GetValue(_eventBus);

            if (!subscriptions.TryGetValue(eventType, out var recipients))
                return new HashSet<IModule>();

            return new HashSet<IModule>(recipients.Where(module => !ReferenceEquals(module, this)));
        }
    }

    /// <summary>
    /// Immutable snapshot of one emitted event, its payload, and the modules that were
    /// subscribed to receive it at the time of emission.
    /// </summary>
    public class EventEmissionRecord
    {
        /// <summary>
        /// Initializes a new recorded event emission.
        /// </summary>
        /// <param name="event">The emitted event instance.</param>
        /// <param name="eventData">The associated payload, if any.</param>
        /// <param name="recipients">The subscribed modules that would receive the event.</param>
        public EventEmissionRecord(IEvent @event, object eventData, HashSet<IModule> recipients)
        {
            Event = @event;
            EventData = eventData;
            Recipients = recipients;
        }

        /// <summary>
        /// The emitted event instance.
        /// </summary>
        public IEvent Event { get; }

        /// <summary>
        /// Optional payload passed alongside the event.
        /// </summary>
        public object EventData { get; }

        /// <summary>
        /// The subscribed recipients captured at emission time.
        /// </summary>
        public HashSet<IModule> Recipients { get; }
    }
}