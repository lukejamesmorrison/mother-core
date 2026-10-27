using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;

namespace MotherCore.Tests.Command
{
    [Category(TestCategories.LayerCommand)]
    public class RenameCommandTests : TestBase
    {
        [Test]
        public void Execute_Without_Arguments_Returns_NoArgumentsProvided()
        {
            var script = ScriptFactory().WithMother().Boot();
            var name = script.Mother.Name;

            script.RunTerminal("rename");

            script.ShouldHavePrinted(CommandBus.Messages.NoArgumentsProvided);
            Assert.That(script.Mother.Name, Is.EqualTo(name));
        }

        [Test]
        public void Execute_Sets_Name_And_Stores_It_In_Configuration()
        {
            var script = ScriptFactory().WithMother().Boot();

            script.RunTerminal("rename Frigate");

            script.ShouldHavePrinted("Grid name set to: Frigate");
            script.ShouldHaveName("Frigate");
            Assert.That(script.Mother.GetModule<Configuration>().Raw.Get("general", "name").ToString(), Is.EqualTo("Frigate"));
        }

        [Test]
        public void Execute_With_Unique_Appends_A_Five_Digit_Suffix()
        {
            var script = ScriptFactory().WithMother().Boot();

            script.RunTerminal("rename Frigate --unique");

            Assert.That(script.Mother.Name, Does.Match(@"^Frigate-\d{5}$"));
        }
    }
}
