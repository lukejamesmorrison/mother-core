using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class TerminalTests : BaseModuleTests
    {
        [Test]
        public void It_Can_Be_Instantiated_With_An_Instance_Of_Mother()
        {
            Terminal terminal = new Terminal(_mother);

            Assert.That(terminal.Mother, Is.SameAs(_mother));
        }

        [Test]
        public void It_Can_Be_Booted()
        {
            Terminal terminal = new Terminal(_mother);

            terminal.Boot();

            Assert.Pass();
        }

        [Test]
        public void It_Can_Have_Highlights()
        {
            Terminal terminal = new Terminal(_mother);

            terminal.Highlight("Test Highlight 1");
            terminal.Highlight("Test Highlight 2");

            string terminalHighlights = terminal.GetHighlights();

            Assert.That(terminalHighlights, Is.EqualTo("Test Highlight 1\nTest Highlight 2\n"));
        }

        [Test]
        public void It_Can_Be_Cleared()
        {
            Terminal terminal = new Terminal(_mother);

            terminal.Print("Test Print 1");

            bool cleared = terminal.ClearConsole();

            Assert.That(cleared, Is.True);
        }

        [Test]
        public void The_Terminal_Window_Can_Be_Updated()
        {
            Terminal terminal = A.Fake<Terminal>(options => options
                .WithArgumentsForConstructor(() => new Terminal(_mother))
            );

            A.CallTo(() => terminal.GetConsoleHeader()).Returns("System OK");

            terminal.UpdateTerminal();

            // expect that Echo is called with calling UpdateTerminal
            A.CallTo(() => terminal.Echo(A<string>._)).MustHaveHappened();
        }
    }
}
