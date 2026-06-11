using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Mocks;
using System.Linq;

namespace MotherCore.Tests.Harness
{
    /// <summary>
    /// Verifies <see cref="FakeIgcNetwork"/>: endpoint allocation, Almanac
    /// cross-registration, <see cref="FakeIgcNetwork.SentMessages"/> capture,
    /// message delivery via <see cref="FakeIgcNetwork.Deliver"/> and
    /// <see cref="FakeIgcNetwork.DispatchIgc"/>, and the
    /// <see cref="FakeIgcNetwork.Scripts"/> roster.
    /// </summary>
    public class FakeIgcNetworkTests
    {
        // =====================================================================
        // Endpoint allocation
        // =====================================================================

        [Test]
        public void Two_Scripts_On_Same_Network_Have_Different_IGC_Ids()
        {
            var network = new FakeIgcNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            Assert.That(shipA.IGC.Me, Is.Not.EqualTo(shipB.IGC.Me));
        }

        [Test]
        public void NetworkIGC_Is_Non_Null_After_Boot_On_Network()
        {
            var network = new FakeIgcNetwork();

            var script = new Script("ShipA").OnNetwork(network).Boot();

            Assert.That(script.NetworkIGC, Is.Not.Null);
        }

        [Test]
        public void Script_Not_On_Network_Has_Null_NetworkIGC()
        {
            var script = new Script().Boot();

            Assert.That(script.NetworkIGC, Is.Null);
        }

        // =====================================================================
        // Almanac cross-registration
        // =====================================================================

        [Test]
        public void Almanac_Record_Contains_Correct_UnicastId_For_Remote_Script()
        {
            var network = new FakeIgcNetwork();

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
            var network = new FakeIgcNetwork();

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

        // =====================================================================
        // SentMessages capture
        // =====================================================================

        [Test]
        public void Remote_Command_SentMessage_Targets_Correct_Recipient()
        {
            var network = new FakeIgcNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            Assert.That(network.SentMessages.Any(m => m.TargetId == shipB.IGC.Me), Is.True,
                "The outbound message should be addressed to ShipB's IGC.Me.");
        }

        [Test]
        public void ClearSentMessages_Empties_The_Capture_List()
        {
            var network = new FakeIgcNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            new Script("ShipB").OnNetwork(network).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            network.ClearSentMessages();

            Assert.That(network.SentMessages.Count, Is.EqualTo(0));
        }

        // =====================================================================
        // Deliver
        // =====================================================================

        [Test]
        public void Deliver_Executes_Remote_Command_On_Recipient()
        {
            var network = new FakeIgcNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            Assert.That(shipB.Bus.GetExecutionCount("help"), Is.EqualTo(0),
                "Command should not execute on ShipB before Deliver() is called.");

            network.Deliver();
            shipB.Clock.RunToIdle();

            Assert.That(shipB.Bus.GetExecutionCount("help"), Is.EqualTo(1),
                "Command should execute on ShipB after Deliver() and the clock runs.");
        }

        [Test]
        public void Deliver_Does_Not_Execute_Command_On_Sender()
        {
            var network = new FakeIgcNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            network.Deliver();

            shipA.Clock.RunToIdle();
            shipB.Clock.RunToIdle();

            Assert.That(shipA.Bus.GetExecutionCount("help"), Is.EqualTo(0),
                "The remote command should not execute locally on the sender.");
            Assert.That(shipB.Bus.GetExecutionCount("help"), Is.EqualTo(1),
                "The remote command should execute once on the recipient.");
        }

        [Test]
        public void Deliver_On_Empty_Pending_Queue_Does_Not_Throw()
        {
            var network = new FakeIgcNetwork();
            new Script("ShipA").OnNetwork(network).Boot();

            Assert.DoesNotThrow(() => network.Deliver());
        }

        // =====================================================================
        // DispatchIgc
        // =====================================================================

        [Test]
        public void DispatchIgc_Delivers_Messages_Like_Deliver()
        {
            var network = new FakeIgcNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();

            shipA.Bus.RunTerminalCommand("@ShipB help");
            shipA.Clock.RunToIdle();

            network.DispatchIgc();
            shipB.Clock.RunToIdle();

            Assert.That(shipB.Bus.GetExecutionCount("help"), Is.EqualTo(1));
        }

        [Test]
        public void DispatchIgc_Returns_Network_For_Chaining()
        {
            var network = new FakeIgcNetwork();
            new Script("ShipA").OnNetwork(network).Boot();

            Assert.That(network.DispatchIgc(), Is.SameAs(network));
        }

        // =====================================================================
        // Scripts
        // =====================================================================

        [Test]
        public void Sessions_Is_Empty_Before_Any_Script_Boots()
        {
            var network = new FakeIgcNetwork();

            Assert.That(network.Scripts, Is.Empty);
        }

        [Test]
        public void Sessions_Contains_Script_After_Boot()
        {
            var network = new FakeIgcNetwork();

            new Script("ShipA").OnNetwork(network).Boot();

            Assert.That(network.Scripts.Count, Is.EqualTo(1));
        }

        [Test]
        public void Sessions_Contains_All_Booted_Scripts_In_Registration_Order()
        {
            var network = new FakeIgcNetwork();

            var shipA = new Script("ShipA").OnNetwork(network).Boot();
            var shipB = new Script("ShipB").OnNetwork(network).Boot();
            var shipC = new Script("ShipC").OnNetwork(network).Boot();

            Assert.That(network.Scripts.Count, Is.EqualTo(3));
            Assert.That(network.Scripts[0].Mother.Name, Is.EqualTo("ShipA"));
            Assert.That(network.Scripts[1].Mother.Name, Is.EqualTo("ShipB"));
            Assert.That(network.Scripts[2].Mother.Name, Is.EqualTo("ShipC"));
        }

        [Test]
        public void Scripts_Not_On_Network_Do_Not_Appear_In_Sessions()
        {
            var network = new FakeIgcNetwork();

            new Script("ShipA").OnNetwork(network).Boot();
            new Script("Standalone").Boot(); // not on the network

            Assert.That(network.Scripts.Count, Is.EqualTo(1));
        }
    }
}
