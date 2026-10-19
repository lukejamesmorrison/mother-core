using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;

namespace MotherCore.Tests.Tests.Module
{
    [Category(TestCategories.LayerModule)]
    public class TerminalTests : TestBase
    {
        [Test]
        public void It_Can_Have_Highlights()
        {
            var script = ScriptFactory().WithMother().Boot();
            var terminal = script.Mother.GetModule<Terminal>();

            terminal.Highlight("Test Highlight 1");
            terminal.Highlight("Test Highlight 2");

            string terminalHighlights = terminal.GetHighlights();

            Assert.That(terminalHighlights, Is.EqualTo("Test Highlight 1\nTest Highlight 2\n"));
        }

        [Test]
        public void It_Can_Be_Cleared()
        {
            var script = ScriptFactory().WithMother().Boot();
            var terminal = script.Mother.GetModule<Terminal>();

            terminal.Print("Test Print 1");

            bool cleared = terminal.ClearConsole();
            terminal.UpdateTerminal();

            Assert.That(cleared, Is.True);
        }
    }
}

