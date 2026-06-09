using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.TestUtilities;
using System.Linq;

namespace MotherCore.Tests.Integration
{
    /// <summary>
    /// Verifies that <see cref="Script"/> and <see cref="MockIGCNetwork"/> are
    /// working correctly. These tests protect the test harness itself — a regression
    /// here means every test that relies on <c>Script</c> or the mock network
    /// is unreliable.
    /// </summary>
    public class ScriptTests
    {
        [Test]
        public void Boot_Exposes_A_Non_Null_CommandBus()
        {
            var session = new Script().Boot();

            Assert.That(session.Bus, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_ClockDriver()
        {
            var session = new Script().Boot();

            Assert.That(session.Clock, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_Configuration()
        {
            var session = new Script().Boot();

            Assert.That(session.Config, Is.Not.Null);
        }

        [Test]
        public void WithCustomData_Makes_Config_Command_Available_After_Boot()
        {
            var session = new Script()
                .WithCustomData(new CustomDataBuilder()
                    .WithCommand("openDoor", "track")
                    .Build())
                .Boot();

            var names = session.Mother.ConfigCommands.Keys;

            Assert.That(names, Contains.Item("openDoor"));
        }

        [Test]
        public void WithCommands_Registers_Command_With_Bus()
        {
            var tracker = new TrackingCommand("myCmd");
            var session = new Script().WithCommands(tracker).Boot();

            session.Bus.RunTerminalCommand("myCmd");
            session.Clock.Tick(2);

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void WithCommands_Accepts_Multiple_Commands()
        {
            var trackerA = new TrackingCommand("cmdA");
            var trackerB = new TrackingCommand("cmdB");
            var session = new Script().WithCommands(trackerA, trackerB).Boot();

            session.Bus.RunTerminalCommand("cmdA");
            session.Bus.RunTerminalCommand("cmdB");
            session.Clock.RunToIdle();

            Assert.That(trackerA.ExecutionCount, Is.EqualTo(1));
            Assert.That(trackerB.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void Two_Scripts_On_Same_Network_Have_Different_IGC_Ids()
        {
            var network = new MockIGCNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            Assert.That(shipA.IGC.Me, Is.Not.EqualTo(shipB.IGC.Me));
        }

        [Test]
        public void NetworkIGC_Is_Non_Null_After_Boot_On_Network()
        {
            var network = new MockIGCNetwork();

            var session = new Script("ShipA").OnNetwork(network).Boot();

            Assert.That(session.NetworkIGC, Is.Not.Null);
        }

        [Test]
        public void Almanac_Record_Contains_Correct_UnicastId_For_Remote_Script()
        {
            var network = new MockIGCNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            var almanac = shipA.Mother.GetModule<Almanac>();
            var record = almanac.GetRecord("ShipB");

            Assert.That(record.UnicastId, Is.EqualTo(shipB.IGC.Me),
                "The Almanac record for ShipB should carry ShipB's IGC.Me as the unicast target.");
        }

        [Test]
        public void Three_Scripts_Are_All_Cross_Registered_With_Each_Other()
        {
            var network = new MockIGCNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();
            var shipC = new Script("ShipC").OnNetwork(network).Boot();

            var almanacA = shipA.Mother.GetModule<Almanac>();
            var almanacB = shipB.Mother.GetModule<Almanac>();
            var almanacC = shipC.Mother.GetModule<Almanac>();

            Assert.That(almanacA.GetRecord("ShipB"), Is.Not.Null, "A should know B");
            Assert.That(almanacA.GetRecord("ShipC"), Is.Not.Null, "A should know C");
            Assert.That(almanacB.GetRecord("ShipA"), Is.Not.Null, "B should know A");
            Assert.That(almanacB.GetRecord("ShipC"), Is.Not.Null, "B should know C");
            Assert.That(almanacC.GetRecord("ShipA"), Is.Not.Null, "C should know A");
            Assert.That(almanacC.GetRecord("ShipB"), Is.Not.Null, "C should know B");
        }

        [Test]
        public void Remote_Command_SentMessage_Targets_Correct_Recipient()
        {
            var network = new MockIGCNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            bool hasMessageForShipB = network.SentMessages.Any(m => m.TargetId == shipB.IGC.Me);

            Assert.That(hasMessageForShipB, Is.True,
                "The outbound message should be addressed to ShipB's IGC.Me.");
        }

        [Test]
        public void ClearSentMessages_Empties_The_Capture_List()
        {
            var network = new MockIGCNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            network.ClearSentMessages();

            Assert.That(network.SentMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public void Deliver_Executes_Remote_Command_On_Recipient()
        {
            var network = new MockIGCNetwork();

            var tracker = new TrackingCommand("probe");
            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).WithCommands(tracker).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB probe");
            shipA.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(0),
                "Command should not execute on ShipB before Deliver() is called.");

            network.Deliver();
            shipB.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Command should execute on ShipB after Deliver() and the clock runs.");
        }

        [Test]
        public void Deliver_Does_Not_Execute_Command_On_Sender()
        {
            var network = new MockIGCNetwork();

            var trackerA = new TrackingCommand("probe");
            var trackerB = new TrackingCommand("probe");
            var shipA = new Script("ShipA").OnNetwork(network).WithCommands(trackerA).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).WithCommands(trackerB).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB probe");
            shipA.Clock.RunToIdle();
            network.Deliver();
            shipA.Clock.RunToIdle();
            shipB.Clock.RunToIdle();

            Assert.That(trackerA.ExecutionCount, Is.EqualTo(0),
                "The remote command should not execute locally on the sender.");
            Assert.That(trackerB.ExecutionCount, Is.EqualTo(1),
                "The remote command should execute once on the recipient.");
        }

        [Test]
        public void Deliver_On_Empty_Pending_Queue_Does_Not_Throw()
        {
            var network = new MockIGCNetwork();

            new Script("ShipA").OnNetwork(network).Boot();

            Assert.DoesNotThrow(() => network.Deliver());
        }
    }
}