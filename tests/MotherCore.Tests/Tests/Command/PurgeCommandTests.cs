using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;
using VRageMath;

namespace MotherCore.Tests.Command
{
    [Category(TestCategories.LayerCommand)]
    public class PurgeCommandTests : TestBase
    {
        [Test]
        public void Execute_Without_Force_Returns_Force_Guidance_And_Does_Not_Purge()
        {
            var script = ScriptFactory().WithMother().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            storage.Set("ship", "Frigate");
            script.RunTerminal("purge storage");

            Assert.That(storage.Get("ship"), Is.EqualTo("Frigate"));
            script.ShouldHavePrinted("Run command with --force to purge");
            script.ShouldHaveExecuted("purge");
        }

        [Test]
        public void Execute_With_Force_And_No_Arguments_Returns_NoArgumentsProvided()
        {
            var script = ScriptFactory().WithMother().Boot();
            script.RunTerminal("purge --force");

            script.ShouldHavePrinted(CommandBus.Messages.NoArgumentsProvided);
            script.ShouldHaveExecuted("purge");
        }

        [Test]
        public void Execute_With_Force_And_Unknown_Module_Returns_No_Modules_Purged()
        {
            var script = ScriptFactory().WithMother().Boot();
            script.RunTerminal("purge unknown --force");

            script.ShouldHavePrinted("No modules purged");
            script.ShouldHaveExecuted("purge");
        }

        [Test]
        public void Execute_With_Force_Purges_Storage_Module()
        {
            var script = ScriptFactory().WithMother().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            storage.Set("ship", "Frigate");

            script.RunTerminal("purge storage --force");

            Assert.That(storage.Get("ship"), Is.EqualTo(string.Empty));
            Assert.That(storage.StorageString, Is.EqualTo(string.Empty));
            script.ShouldHaveExecuted("purge");
        }

        [Test]
        public void Execute_With_Force_Purges_Almanac_Module()
        {
            var script = ScriptFactory().WithMother().Boot();
            var almanac = script.Mother.GetModule<Almanac>();

            almanac.AddRecord(new AlmanacRecord("wp-1", "waypoint", new Vector3D(1, 2, 3)));

            script.RunTerminal("purge almanac --force");

            Assert.That(almanac.Records, Is.Empty);
            script.ShouldHaveExecuted("purge");
        }

        [Test]
        public void Execute_With_Force_And_Wildcard_Purges_Almanac_And_Storage()
        {
            var script = ScriptFactory().WithMother().Boot();
            var almanac = script.Mother.GetModule<Almanac>();
            var storage = script.Mother.GetModule<LocalStorage>();

            almanac.AddRecord(new AlmanacRecord("wp-1", "waypoint", new Vector3D(1, 2, 3)));
            storage.Set("ship", "Frigate");

            script.RunTerminal("purge * --force=true");

            Assert.That(almanac.Records, Is.Empty);
            Assert.That(storage.Get("ship"), Is.EqualTo(string.Empty));
            script.ShouldHaveExecuted("purge");
        }

        [Test]
        public void Execute_Does_Not_Keep_Force_State_Between_Invocations()
        {
            var script = ScriptFactory().WithMother().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            storage.Set("first", "value");
            script.RunTerminal("purge storage --force");

            storage.Set("second", "value");
            script.RunTerminal("purge storage");

            Assert.That(storage.Get("first"), Is.EqualTo(""));
            Assert.That(storage.Get("second"), Is.EqualTo("value"));
            script.ShouldHaveExecuted("purge", count: 2);
            script.ShouldHavePrinted("Run command with --force to purge");
        }
    }
}