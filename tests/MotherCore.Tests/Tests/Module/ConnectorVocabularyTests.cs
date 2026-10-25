using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Module
{
    /// <summary>
    /// Core owns connector state, so a script with no extension modules (ie. 
    /// Mother GUI) can still test a connector in a condition.
    /// </summary>
    [Category(TestCategories.LayerModule)]
    public class ConnectorVocabularyTests : TestBase
    {
        static bool If(Script<CoreTestProgram> script, string condition, out string error)
        {
            return script.Mother.GetModule<ConditionModule>().Evaluate(new TerminalCommand("if " + condition), out error);
        }

        static Script<CoreTestProgram> Boot(FakeShipConnector connector)
        {
            return ScriptFactory().WithMother().WithBlock(connector).Boot();
        }

        [TestCase("connected", MyShipConnectorStatus.Connected, true)]
        [TestCase("connected", MyShipConnectorStatus.Unconnected, false)]
        [TestCase("connectable", MyShipConnectorStatus.Connectable, true)]
        [TestCase("connectable", MyShipConnectorStatus.Connected, false)]
        [TestCase("disconnected", MyShipConnectorStatus.Unconnected, true)]
        [TestCase("disconnected", MyShipConnectorStatus.Connected, false)]
        public void A_Connector_Predicate_Reads_The_Connector_Status(string predicate, MyShipConnectorStatus status, bool expected)
        {
            var connector = new FakeShipConnector(customName: "Dock");
            connector.Configure(() => status, null, () => { }, () => { }, () => { });

            var script = Boot(connector);
            string error;

            bool result = If(script, "\"Dock\" " + predicate, out error);

            Assert.That(error, Is.Null);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Status_Compares_A_Connector_By_State_Name()
        {
            var connector = new FakeShipConnector(customName: "Dock");
            connector.Configure(() => MyShipConnectorStatus.Connected, null, () => { }, () => { }, () => { });

            var script = Boot(connector);
            string error;

            Assert.That(If(script, "\"Dock\" status == connected", out error), Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void A_Reboot_Does_Not_Double_The_Connector_Vocabulary()
        {
            var connector = new FakeShipConnector(customName: "Dock");
            connector.Configure(() => MyShipConnectorStatus.Connected, null, () => { }, () => { }, () => { });

            var script = Boot(connector);
            script.Mother.PendingBoot = true;
            script.RunToIdle();

            string error;

            Assert.That(If(script, "\"Dock\" connected", out error), Is.True);
            Assert.That(error, Is.Null);
        }
    }
}
