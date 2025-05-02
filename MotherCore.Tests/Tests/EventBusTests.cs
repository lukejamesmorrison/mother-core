using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;

namespace MotherCore.Tests.Tests
{
    public class EventBusTests : BaseModuleTests
    {
        [Test]
        public void It_Can_Be_Instantiated_With_An_Instance_Of_Mother()
        {
            EventBus eventBus = new EventBus(_mother);

            Assert.That(eventBus.Mother, Is.SameAs(_mother));
        }

        [Test]
        public void It_Can_Be_Booted()
        {
            EventBus module = new EventBus(_mother);

            module.Boot();

            Assert.Pass();
        }

        [Test]
        public void It_Can_Be_Subscribed_To_An_Event()
        {
            EventBus eventBus = new EventBus(_mother);
            var module1 = A.Fake<IModule>();
            var module2 = A.Fake<IModule>();


            eventBus.Subscribe<ConnectorLockedEvent>(module1);

            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module1), Is.True);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module2), Is.False);
        }

        [Test]
        public void It_Can_Be_Unsubscribed_From_An_Event()
        {
            EventBus eventBus = new EventBus(_mother);
            var module = A.Fake<IModule>();

            //var someEvent = A.Fake<ConnectorLockedEvent>();
            eventBus.Subscribe<ConnectorLockedEvent>(module);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module), Is.True);

            eventBus.Unsubscribe<ConnectorLockedEvent>(module);
            Assert.That(eventBus.IsSubscribed<ConnectorLockedEvent>(module), Is.False);
        }

        [Test]
        public void It_Can_Emit_An_Event()
        {
            EventBus eventBus = new EventBus(_mother);

            Almanac almanac = A.Fake<Almanac>(options =>
                options.WithArgumentsForConstructor(() => new Almanac(_mother)));

            Security security = A.Fake<Security>(options =>
                options.WithArgumentsForConstructor(() => new Security(_mother)));

            eventBus.Subscribe<ConnectorLockedEvent>(almanac);
            eventBus.Subscribe<ConnectorLockedEvent>(security);

            eventBus.Emit<ConnectorLockedEvent>();

            A.CallTo(() => almanac.HandleEvent(A<ConnectorLockedEvent>._, null))
                .MustHaveHappened();

            A.CallTo(() => security.HandleEvent(A<ConnectorLockedEvent>._, null))
                .MustHaveHappened();
        }
    }
}
