using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using System.Linq;

namespace MotherCore.Tests.Integration
{
    public class EventBusTests : ScriptTestBase<CoreTestProgram>
    {
        class EventPayloadModule : BaseModule
        {
            public EventPayloadModule(Mother mother) : base(mother) { }

            public IEvent LastEvent;

            public object LastEventData;

            public override void HandleEvent(IEvent e, object eventData)
            {
                LastEvent = e;
                LastEventData = eventData;
            }
        }

        [Test]
        public void A_Module_Can_Be_Subscribed_To_An_Event()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();

            IModule module1 = ModuleFactory.Create(Mother);
            IModule module2 = ModuleFactory.Create(Mother);

            eventBus.Subscribe<ConnectorLockedEvent>(module1);

            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module1), Is.True);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module2), Is.False);
        }

        [Test]
        public void A_Module_Can_Be_Unsubscribed_From_An_Event()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();
            IModule module = ModuleFactory.Create(Mother);

            eventBus.Subscribe<ConnectorLockedEvent>(module);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module), Is.True);

            eventBus.Unsubscribe<ConnectorLockedEvent>(module);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module), Is.False);
        }

        [Test]
        public void It_Can_Emit_An_Event()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();

            IModule module = ModuleFactory.Create(Mother);

            eventBus.Subscribe<ConnectorLockedEvent>(module);

            eventBus.Emit<ConnectorLockedEvent>();

            Script.AssertEventEmitted<ConnectorLockedEvent>();
            Assert.That(eventBus.Emissions.Count(emission => emission.Event is ConnectorLockedEvent), Is.EqualTo(1));
            Assert.That(
                eventBus.Emissions.Count(emission =>
                    emission.Event is ConnectorLockedEvent
                    && emission.Recipients.Contains(module)),
                Is.EqualTo(1));
            Script.AssertEventEmitted<ConnectorLockedEvent>(module);
        }

        [Test]
        public void It_Can_Emit_An_Event_With_A_Data_Payload()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();
            var module = new EventPayloadModule(Mother);
            var payload = "airlock-1";

            eventBus.Subscribe<ConnectorLockedEvent>(module);
            eventBus.Emit<ConnectorLockedEvent>(payload);

            Assert.That(module.LastEvent, Is.TypeOf<ConnectorLockedEvent>());
            Assert.That(module.LastEventData, Is.EqualTo(payload));
            Script.AssertEventEmitted<ConnectorLockedEvent>(module);
        }

        [Test]
        public void It_Can_Emit_An_Event_With_A_Block_Data_Payload()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();
            var module = new EventPayloadModule(Mother);
            var payload = TerminalBlockFactory.Create<IMyDoor>(customName: "Hangar Door");

            eventBus.Subscribe<ConnectorLockedEvent>(module);
            eventBus.Emit<ConnectorLockedEvent>(payload);

            Assert.That(module.LastEvent, Is.TypeOf<ConnectorLockedEvent>());
            Assert.That(module.LastEventData, Is.SameAs(payload));
            Assert.That(module.LastEventData, Is.InstanceOf<IMyTerminalBlock>());
            Script.AssertEventEmitted<ConnectorLockedEvent>(module);
        }

        [Test]
        public void FakeModule_Remains_Aligned_With_BaseModule_Defaults_And_IModule_Contract()
        {
            IModule module = ModuleFactory.Create(Mother);

            Assert.That(module, Is.InstanceOf<BaseModule>());
            Assert.That(module.GetModuleName(), Is.EqualTo(module.GetType().ToString()));
            Assert.That(module.GetCommands(), Is.Empty);
        }
    }
}

