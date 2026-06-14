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
            var mother = script.Mother;


            var command = new PurgeCommand(mother);
            var storage = mother.GetModule<LocalStorage>();

            storage.Set("ship", "Frigate");

            var result = command.Execute(new TerminalCommand("purge storage"));

            Assert.That(result, Is.EqualTo("Run command with --force to purge"));
            Assert.That(storage.Get("ship"), Is.EqualTo("Frigate"));
        }

        [Test]
        public void Execute_With_Force_And_No_Arguments_Returns_NoArgumentsProvided()
        {
            var script = ScriptFactory().WithMother().Boot();
            var mother = script.Mother;

            var command = new PurgeCommand(mother);

            var result = command.Execute(new TerminalCommand("purge --force"));

            Assert.That(result, Is.EqualTo(CommandBus.Messages.NoArgumentsProvided));
        }

        [Test]
        public void Execute_With_Force_And_Unknown_Module_Returns_No_Modules_Purged()
        {
            var script = ScriptFactory().WithMother().Boot();
            var mother = script.Mother;
            var command = new PurgeCommand(mother);

            var result = command.Execute(new TerminalCommand("purge unknown --force"));

            Assert.That(result, Is.EqualTo("No modules purged"));
        }

        [Test]
        public void Execute_With_Force_Purges_Storage_Module()
        {
            var script = ScriptFactory().WithMother().Boot();
            var mother = script.Mother;
            var command = new PurgeCommand(mother);
            var storage = mother.GetModule<LocalStorage>();

            storage.Set("ship", "Frigate");

            var result = command.Execute(new TerminalCommand("purge storage --force"));

            Assert.That(storage.Get("ship"), Is.EqualTo(string.Empty));
            Assert.That(storage.StorageString, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Execute_With_Force_Purges_Almanac_Module()
        {
            var script = ScriptFactory().WithMother().Boot();
            var mother = script.Mother;
            var command = new PurgeCommand(mother);
            var almanac = mother.GetModule<Almanac>();

            almanac.AddRecord(new AlmanacRecord("wp-1", "waypoint", new Vector3D(1, 2, 3)));

            var result = command.Execute(new TerminalCommand("purge almanac --force"));

            Assert.That(almanac.Records, Is.Empty);
        }

        [Test]
        public void Execute_With_Force_And_Wildcard_Purges_Almanac_And_Storage()
        {
            var script = ScriptFactory().WithMother().Boot();
            var mother = script.Mother;
            var command = new PurgeCommand(mother);
            var almanac = mother.GetModule<Almanac>();
            var storage = mother.GetModule<LocalStorage>();

            almanac.AddRecord(new AlmanacRecord("wp-1", "waypoint", new Vector3D(1, 2, 3)));
            storage.Set("ship", "Frigate");

            var result = command.Execute(new TerminalCommand("purge * --force=true"));

            Assert.That(almanac.Records, Is.Empty);
            Assert.That(storage.Get("ship"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Execute_Does_Not_Keep_Force_State_Between_Invocations()
        {
            var script = ScriptFactory().WithMother().Boot();
            var mother = script.Mother;
            var command = new PurgeCommand(mother);
            var storage = mother.GetModule<LocalStorage>();

            storage.Set("first", "value");
            var forced = command.Execute(new TerminalCommand("purge storage --force"));

            storage.Set("second", "value");
            var notForced = command.Execute(new TerminalCommand("purge storage"));

            Assert.That(storage.Get("first"), Is.EqualTo(""));
            Assert.That(storage.Get("second"), Is.EqualTo("value"));
        }
    }
}