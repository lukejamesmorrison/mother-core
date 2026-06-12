using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;

namespace MotherCore.Tests.Integration
{
    public class TerminalTests : ScriptTestBase<CoreTestProgram>
    {
        [Test]
        public void It_Can_Have_Highlights()
        {
            Terminal terminal = new Terminal(Mother);

            terminal.Highlight("Test Highlight 1");
            terminal.Highlight("Test Highlight 2");

            string terminalHighlights = terminal.GetHighlights();

            Assert.That(terminalHighlights, Is.EqualTo("Test Highlight 1\nTest Highlight 2\n"));
        }

        [Test]
        public void It_Can_Be_Cleared()
        {
            Terminal terminal = new Terminal(Mother);
            var capture = new PrintCapture(Script);

            terminal.Print("Test Print 1");

            bool cleared = terminal.ClearConsole();
            terminal.UpdateTerminal();

            Assert.That(cleared, Is.True);
        }
    }
}

