using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Integration
{
    public class EventBusTests : ScriptTestBase<CoreTestProgram>
    {

        [Test]
        public void A_Module_Can_Be_Subscribed_To_An_Event()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();
            IModule module1 = new FakeModule<ConnectorLockedEvent>(Mother);
            IModule module2 = new FakeModule<ConnectorLockedEvent>(Mother);


            eventBus.Subscribe<ConnectorLockedEvent>(module1);

            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module1), Is.True);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module2), Is.False);
        }

        [Test]
        public void A_Module_Can_Be_Unsubscribed_From_An_Event()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();
            IModule module = new FakeModule<ConnectorLockedEvent>(Mother);

            eventBus.Subscribe<ConnectorLockedEvent>(module);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module), Is.True);

            eventBus.Unsubscribe<ConnectorLockedEvent>(module);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module), Is.False);
        }

        [Test]
        public void It_Can_Emit_An_Event()
        {
            EventBus eventBus = Mother.GetModule<EventBus>();

            IModule observer = new FakeModule<ConnectorLockedEvent>(Mother);

            eventBus.Subscribe<ConnectorLockedEvent>(observer);

            eventBus.Emit<ConnectorLockedEvent>();

            Script.AssertEventEmitted<ConnectorLockedEvent>();
            Script.AssertEventEmitted<ConnectorLockedEvent>(observer);
        }

        [Test]
        public void FakeModule_Remains_Aligned_With_BaseModule_Defaults_And_IModule_Contract()
        {
            IModule module = new FakeModule<ConnectorLockedEvent>(Mother);

            Assert.That(module, Is.InstanceOf<BaseModule>());
            Assert.That(module.GetModuleName(), Is.EqualTo("FakeModule<ConnectorLockedEvent>"));
            Assert.That(module.GetCommands(), Is.Empty);
        }
    }
}

