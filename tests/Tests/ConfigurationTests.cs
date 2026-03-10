using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class ConfigurationTests : BaseModuleTests
    {
        [Test]
        public void It_Can_Load_Variables_From_Custom_Data()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=Luke",
                "",
                "[commands]"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigVariables.ContainsKey("PLAYER"), Is.True);
            Assert.That(_mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke"));
        }

        [Test]
        public void It_Can_Substitute_Variables_Into_Commands()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=Luke",
                "",
                "[commands]",
                "greeting=Hello, $PLAYER"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigCommands.ContainsKey("greeting"), Is.True);

            // ConfigCommands now stores the raw template
            Assert.That(_mother.ConfigCommands["greeting"], Is.EqualTo("Hello, $PLAYER"));

            // Variables are resolved at runtime
            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Can_Substitute_Multiple_Variables_Into_A_Command()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=Luke",
                "SHIP=Falcon",
                "",
                "[commands]",
                "greeting=Hello, $PLAYER aboard $SHIP"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke aboard Falcon"));
        }

        [Test]
        public void It_Can_Substitute_The_Same_Variable_Multiple_Times()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "NAME=Luke",
                "",
                "[commands]",
                "echo=Hello $NAME, goodbye $NAME"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["echo"], null);
            Assert.That(resolved, Is.EqualTo("Hello Luke, goodbye Luke"));
        }

        [Test]
        public void Commands_Without_Variables_Are_Not_Affected()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=Luke",
                "",
                "[commands]",
                "stop=light/off Light1"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigCommands["stop"], Is.EqualTo("light/off Light1"));
        }

        [Test]
        public void Variables_Section_Can_Be_Empty()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "",
                "[commands]",
                "stop=light/off Light1"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigVariables.Count, Is.EqualTo(0));
            Assert.That(_mother.ConfigCommands["stop"], Is.EqualTo("light/off Light1"));
        }

        [Test]
        public void Longer_Variable_Names_Are_Substituted_Before_Shorter_Ones()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "START=Begin",
                "START_TIME=12:00",
                "",
                "[commands]",
                "echo=Launch at $START_TIME; $START sequence"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["echo"], null);
            Assert.That(resolved, Is.EqualTo("Launch at 12:00; Begin sequence"));
        }

        [Test]
        public void It_Strips_Double_Quotes_From_Variable_Values()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=\"Luke\"",
                "",
                "[commands]",
                "greeting=Hello, $PLAYER"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke"));

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Strips_Double_Quotes_From_Variable_Values_With_Spaces()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=\"Luke Morrison\"",
                "",
                "[commands]",
                "greeting=Hello, $PLAYER"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke Morrison"));

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke Morrison"));
        }

        [Test]
        public void It_Strips_Leading_Dollar_Sign_From_Variable_Names()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "$PLAYER=Luke",
                "",
                "[commands]",
                "greeting=Hello, $PLAYER"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigVariables.ContainsKey("PLAYER"), Is.True);

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Strips_Quotes_From_Command_Values()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=Luke",
                "",
                "[commands]",
                "greeting=\"Hello, $PLAYER\""
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Handles_Dollar_Prefix_And_Quoted_Value_Together()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "$PLAYER=\"Luke Morrison\"",
                "",
                "[commands]",
                "greeting=\"Hello, $PLAYER\""
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            Assert.That(_mother.ConfigVariables.ContainsKey("PLAYER"), Is.True);
            Assert.That(_mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke Morrison"));

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke Morrison"));
        }

        // ---- Command Parameter Tests ----

        [Test]
        public void It_Substitutes_Command_Parameters_With_Provided_Options()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "",
                "[commands]",
                "greeting=Hello, {{player}}"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            var options = new Dictionary<string, string> { { "player", "Alex" } };
            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], options);
            Assert.That(resolved, Is.EqualTo("Hello, Alex"));
        }

        [Test]
        public void It_Uses_Default_Value_When_No_Option_Provided()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "",
                "[commands]",
                "greeting=Hello, {{player:World}}"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, World"));
        }

        [Test]
        public void It_Uses_Variable_As_Default_For_Command_Parameter()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=Luke",
                "",
                "[commands]",
                "greeting=Hello, {{player:$PLAYER}}"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            // No options provided — uses $PLAYER default which resolves to "Luke"
            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Overrides_Variable_Default_With_Provided_Option()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "PLAYER=Luke",
                "",
                "[commands]",
                "greeting=Hello, {{player:$PLAYER}}"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            // Option provided — overrides the $PLAYER default
            var options = new Dictionary<string, string> { { "player", "Alex" } };
            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], options);
            Assert.That(resolved, Is.EqualTo("Hello, Alex"));
        }

        [Test]
        public void It_Substitutes_Multiple_Command_Parameters()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "",
                "[commands]",
                "activateLights=block/on {{block:Lights}}; light/color {{block:Lights}} {{color:red}}"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            var options = new Dictionary<string, string> { { "block", "Cockpit Lights" }, { "color", "blue" } };
            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["activateLights"], options);
            Assert.That(resolved, Is.EqualTo("block/on Cockpit Lights; light/color Cockpit Lights blue"));
        }

        [Test]
        public void It_Returns_Empty_String_For_Parameter_Without_Default_Or_Option()
        {
            _mother.ProgrammableBlock.CustomData = string.Join("\n",
                "[variables]",
                "",
                "[commands]",
                "greeting=Hello, {{player}}"
            );

            Configuration config = new Configuration(_mother);
            config.Boot();

            string resolved = _mother.SubstituteCommandParameters(_mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, "));
        }
    }
}
