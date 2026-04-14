using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class TerminalCommandTests : BaseModuleTests
    {
        // --- Basic parsing ---

        [Test]
        public void It_Can_Be_Instantiated_With_A_Command_String()
        {
            string commandString = "rotor/rotate -45 --speed=100 --delay=0.5 --force";

            TerminalCommand command = new TerminalCommand(commandString);

            Assert.That(command.Name, Is.EqualTo("rotor/rotate"));
            Assert.That(command.Arguments, Is.EqualTo(new List<string> { "-45" }));
            Assert.That(command.Options, Is.EqualTo(new Dictionary<string, string>
            {
                { "speed", "100" },
                { "delay", "0.5" },
                { "force", "true" }
            }));
            Assert.That(command.CommandString, Is.EqualTo(commandString));
        }

        [Test]
        public void It_Can_Get_Options_By_Key()
        {
            string commandString = "rotor/rotate -45 --speed=100 --delay=0.5 --force";
            TerminalCommand command = new TerminalCommand(commandString);

            Assert.That(command.GetOption("speed"), Is.EqualTo("100"));
            Assert.That(command.GetOption("delay"), Is.EqualTo("0.5"));
            Assert.That(command.GetOption("force"), Is.EqualTo("true"));
            Assert.That(command.GetOption("nonexistent"), Is.EqualTo(""));
        }

        // --- Quoted argument handling ---

        [Test]
        public void It_Preserves_Quoted_Arguments_As_Single_Terms()
        {
            TerminalCommand command = new TerminalCommand("print \"Hello World\"");

            Assert.That(command.Name, Is.EqualTo("print"));
            Assert.That(command.Arguments.Count, Is.EqualTo(1));
            Assert.That(command.Arguments[0], Is.EqualTo("Hello World"));
        }

        [Test]
        public void It_Handles_Multiple_Quoted_Arguments()
        {
            TerminalCommand command = new TerminalCommand("msg \"Player One\" \"Welcome aboard\"");

            Assert.That(command.Name, Is.EqualTo("msg"));
            Assert.That(command.Arguments.Count, Is.EqualTo(2));
            Assert.That(command.Arguments[0], Is.EqualTo("Player One"));
            Assert.That(command.Arguments[1], Is.EqualTo("Welcome aboard"));
        }

        [Test]
        public void It_Handles_Quoted_Arguments_Mixed_With_Options()
        {
            TerminalCommand command = new TerminalCommand("light/color \"Warning Light\" red --brightness=100");

            Assert.That(command.Name, Is.EqualTo("light/color"));
            Assert.That(command.Arguments.Count, Is.EqualTo(2));
            Assert.That(command.Arguments[0], Is.EqualTo("Warning Light"));
            Assert.That(command.Arguments[1], Is.EqualTo("red"));
            Assert.That(command.GetOption("brightness"), Is.EqualTo("100"));
        }

        // --- Whitespace and formatting ---

        [Test]
        public void It_Trims_Leading_And_Trailing_Whitespace()
        {
            TerminalCommand command = new TerminalCommand("  help  ");

            Assert.That(command.Name, Is.EqualTo("help"));
            Assert.That(command.Arguments.Count, Is.EqualTo(0));
        }

        [Test]
        public void It_Strips_Carriage_Returns()
        {
            TerminalCommand command = new TerminalCommand("help\r");

            Assert.That(command.Name, Is.EqualTo("help"));
        }

        [Test]
        public void It_Handles_Multiple_Spaces_Between_Terms()
        {
            TerminalCommand command = new TerminalCommand("rotor/rotate   -45   --speed=100");

            Assert.That(command.Name, Is.EqualTo("rotor/rotate"));
            Assert.That(command.Arguments.Count, Is.EqualTo(1));
            Assert.That(command.Arguments[0], Is.EqualTo("-45"));
            Assert.That(command.GetOption("speed"), Is.EqualTo("100"));
        }

        // --- Command-only (no arguments, no options) ---

        [Test]
        public void It_Handles_Command_With_No_Arguments_Or_Options()
        {
            TerminalCommand command = new TerminalCommand("help");

            Assert.That(command.Name, Is.EqualTo("help"));
            Assert.That(command.Arguments.Count, Is.EqualTo(0));
            Assert.That(command.Options.Count, Is.EqualTo(0));
        }

        // --- GetBoolFromString ---

        [Test]
        public void GetBoolFromString_Returns_True_For_True_String()
        {
            Assert.That(TerminalCommand.GetBoolFromString("true"), Is.True);
            Assert.That(TerminalCommand.GetBoolFromString("True"), Is.True);
            Assert.That(TerminalCommand.GetBoolFromString("TRUE"), Is.True);
        }

        [Test]
        public void GetBoolFromString_Returns_True_For_One_String()
        {
            Assert.That(TerminalCommand.GetBoolFromString("1"), Is.True);
        }

        [Test]
        public void GetBoolFromString_Returns_False_For_Other_Strings()
        {
            Assert.That(TerminalCommand.GetBoolFromString("false"), Is.False);
            Assert.That(TerminalCommand.GetBoolFromString("0"), Is.False);
            Assert.That(TerminalCommand.GetBoolFromString(""), Is.False);
            Assert.That(TerminalCommand.GetBoolFromString("yes"), Is.False);
        }

        [Test]
        public void GetBoolFromString_Handles_Null_Input()
        {
            Assert.That(TerminalCommand.GetBoolFromString(null), Is.False);
        }

        [Test]
        public void GetBoolFromString_Trims_Whitespace()
        {
            Assert.That(TerminalCommand.GetBoolFromString("  true  "), Is.True);
            Assert.That(TerminalCommand.GetBoolFromString("  1  "), Is.True);
        }

        // --- Multiple arguments ---

        [Test]
        public void It_Parses_Multiple_Positional_Arguments()
        {
            TerminalCommand command = new TerminalCommand("piston/distance Piston1 1.5 3.0");

            Assert.That(command.Name, Is.EqualTo("piston/distance"));
            Assert.That(command.Arguments.Count, Is.EqualTo(3));
            Assert.That(command.Arguments[0], Is.EqualTo("Piston1"));
            Assert.That(command.Arguments[1], Is.EqualTo("1.5"));
            Assert.That(command.Arguments[2], Is.EqualTo("3.0"));
        }

        // --- Option-only commands ---

        [Test]
        public void It_Parses_Command_With_Only_Options()
        {
            TerminalCommand command = new TerminalCommand("debug --verbose --level=3");

            Assert.That(command.Name, Is.EqualTo("debug"));
            Assert.That(command.Arguments.Count, Is.EqualTo(0));
            Assert.That(command.Options.Count, Is.EqualTo(2));
            Assert.That(command.GetOption("verbose"), Is.EqualTo("true"));
            Assert.That(command.GetOption("level"), Is.EqualTo("3"));
        }

        // --- Flight plan as quoted argument ---

        [Test]
        public void It_Preserves_Flight_Plan_With_Routines_As_Single_Quoted_Argument()
        {
            string flightPlan = "GPS:WP1:1:2:3:#FF75C9F1: { cmd1; cmd2; } GPS:WP2:4:5:6:#FF75C9F1: R";

            TerminalCommand command = new TerminalCommand("nav/set-flight-plan \"" + flightPlan + "\"");

            Assert.That(command.Name, Is.EqualTo("nav/set-flight-plan"));
            Assert.That(command.Arguments.Count, Is.EqualTo(1));
            Assert.That(command.Arguments[0], Is.EqualTo(flightPlan));
        }

        [Test]
        public void It_Preserves_Flight_Plan_With_Multiple_Routines_As_Single_Quoted_Argument()
        {
            string flightPlan = "{ wait 2; ArmsIn; } GPS:Exit:1:2:3:#FF75C9F1: { 40p; fcs/start; } GPS:Outpost:4:5:6:#FF75C9F1: { 80p; } R";

            TerminalCommand command = new TerminalCommand("nav/set-flight-plan \"" + flightPlan + "\"");

            Assert.That(command.Name, Is.EqualTo("nav/set-flight-plan"));
            Assert.That(command.Arguments.Count, Is.EqualTo(1));
            Assert.That(command.Arguments[0], Is.EqualTo(flightPlan));
        }

        [Test]
        public void It_Preserves_Flight_Plan_With_Options_And_Quoted_Argument()
        {
            string flightPlan = "GPS:WP1:1:2:3:#FF75C9F1: { fcs/start --speed=99; } R";

            TerminalCommand command = new TerminalCommand("nav/set-flight-plan \"" + flightPlan + "\"");

            Assert.That(command.Name, Is.EqualTo("nav/set-flight-plan"));
            Assert.That(command.Arguments.Count, Is.EqualTo(1));
            Assert.That(command.Arguments[0], Is.EqualTo(flightPlan));
        }

        // --- Force local prefix (!!) ---

        [Test]
        public void It_Parses_Force_Local_Prefix()
        {
            TerminalCommand command = new TerminalCommand("!!help");

            Assert.That(command.Name, Is.EqualTo("help"));
            Assert.That(command.IsForceLocal, Is.True);
        }

        [Test]
        public void It_Parses_Force_Local_Prefix_With_Arguments()
        {
            TerminalCommand command = new TerminalCommand("!!light/color Light1 red");

            Assert.That(command.Name, Is.EqualTo("light/color"));
            Assert.That(command.IsForceLocal, Is.True);
            Assert.That(command.Arguments.Count, Is.EqualTo(2));
            Assert.That(command.Arguments[0], Is.EqualTo("Light1"));
            Assert.That(command.Arguments[1], Is.EqualTo("red"));
        }

        [Test]
        public void It_Parses_Force_Local_Prefix_With_Options()
        {
            TerminalCommand command = new TerminalCommand("!!rotor/rotate -45 --speed=100");

            Assert.That(command.Name, Is.EqualTo("rotor/rotate"));
            Assert.That(command.IsForceLocal, Is.True);
            Assert.That(command.Arguments[0], Is.EqualTo("-45"));
            Assert.That(command.GetOption("speed"), Is.EqualTo("100"));
        }

        [Test]
        public void Command_Without_Force_Local_Prefix_Has_IsForceLocal_False()
        {
            TerminalCommand command = new TerminalCommand("help");

            Assert.That(command.Name, Is.EqualTo("help"));
            Assert.That(command.IsForceLocal, Is.False);
        }
    }
}
