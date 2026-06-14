using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Module
{
    [Category(TestCategories.LayerModule)]
    public class ConfigurationTests : TestBase
    {
        [Test]
        public void It_Can_Load_Variables_From_Custom_Data()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigVariables.ContainsKey("PLAYER"), Is.True);
            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke"));
        }

        [Test]
        public void It_Can_Substitute_Variables_Into_Commands()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .WithCommand("greeting", "Hello, $PLAYER")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigCommands.ContainsKey("greeting"), Is.True);

            // ConfigCommands now stores the raw template
            Assert.That(script.Mother.ConfigCommands["greeting"], Is.EqualTo("Hello, $PLAYER"));

            // Variables are resolved at runtime
            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Can_Substitute_Multiple_Variables_Into_A_Command()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .WithVariable("SHIP", "Millennium Falcon")
                    .WithCommand("greeting", "Hello, $PLAYER aboard the $SHIP")
                    .Build())
                .Boot();

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke aboard the Millennium Falcon"));
        }

        [Test]
        public void It_Can_Substitute_The_Same_Variable_Multiple_Times()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("NAME", "Luke")
                    .WithCommand("echo", "Hello $NAME, goodbye $NAME")
                    .Build())
                .Boot();

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["echo"], null);
            Assert.That(resolved, Is.EqualTo("Hello Luke, goodbye Luke"));
        }

        [Test]
        public void Commands_Without_Variables_Are_Not_Affected()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .WithCommand("stop", "light/off Light1")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigCommands["stop"], Is.EqualTo("light/off Light1"));
        }

        [Test]
        public void Variables_Section_Can_Be_Empty()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("stop", "light/off Light1")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigVariables.Count, Is.EqualTo(0));
            Assert.That(script.Mother.ConfigCommands["stop"], Is.EqualTo("light/off Light1"));
        }

        [Test]
        public void Longer_Variable_Names_Are_Substituted_Before_Shorter_Ones()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("START", "Begin")
                    .WithVariable("START_TIME", "12:00")
                    .WithCommand("echo", "Launch at $START_TIME; $START sequence")
                    .Build())
                .Boot();

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["echo"], null);
            Assert.That(resolved, Is.EqualTo("Launch at 12:00; Begin sequence"));
        }

        [Test]
        public void It_Strips_Double_Quotes_From_Variable_Values()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "\"Luke\"")
                    .WithCommand("greeting", "Hello, $PLAYER")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke"));

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Strips_Double_Quotes_From_Variable_Values_With_Spaces()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "\"Luke Morrison\"")
                    .WithCommand("greeting", "Hello, $PLAYER")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke Morrison"));

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke Morrison"));
        }

        [Test]
        public void It_Strips_Leading_Dollar_Sign_From_Variable_Names()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("$PLAYER", "Luke")
                    .WithCommand("greeting", "Hello, $PLAYER")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigVariables.ContainsKey("PLAYER"), Is.True);

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Strips_Quotes_From_Command_Values()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .WithCommand("greeting", "\"Hello, $PLAYER\"")
                    .Build())
                .Boot();

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Handles_Dollar_Prefix_And_Quoted_Value_Together()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("$PLAYER", "\"Luke Morrison\"")
                    .WithCommand("greeting", "\"Hello, $PLAYER\"")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigVariables.ContainsKey("PLAYER"), Is.True);
            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Luke Morrison"));

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke Morrison"));
        }

        // ---- Command Parameter Tests ----

        [Test]
        public void It_Substitutes_Command_Parameters_With_Provided_Options()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("greeting", "Hello, {{player}}")
                    .Build())
                .Boot();

            var options = new Dictionary<string, string> { { "player", "Alex" } };
            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], options);
            Assert.That(resolved, Is.EqualTo("Hello, Alex"));
        }

        [Test]
        public void It_Uses_Default_Value_When_No_Option_Provided()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("greeting", "Hello, {{player:World}}")
                    .Build())
                .Boot();

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, World"));
        }

        [Test]
        public void It_Uses_Variable_As_Default_For_Command_Parameter()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .WithCommand("greeting", "Hello, {{player:$PLAYER}}")
                    .Build())
                .Boot();

            // No options provided � uses $PLAYER default which resolves to "Luke"
            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Luke"));
        }

        [Test]
        public void It_Overrides_Variable_Default_With_Provided_Option()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .WithCommand("greeting", "Hello, {{player:$PLAYER}}")
                    .Build())
                .Boot();

            // Option provided � overrides the $PLAYER default
            var options = new Dictionary<string, string> { { "player", "Alex" } };
            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], options);
            Assert.That(resolved, Is.EqualTo("Hello, Alex"));
        }

        [Test]
        public void It_Substitutes_Multiple_Command_Parameters()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("activateLights", "block/on {{block:Lights}}; light/color {{block:Lights}} {{color:red}}")
                    .Build())
                .Boot();

            var options = new Dictionary<string, string> { { "block", "Cockpit Lights" }, { "color", "blue" } };
            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["activateLights"], options);
            Assert.That(resolved, Is.EqualTo("block/on Cockpit Lights; light/color Cockpit Lights blue"));
        }

        [Test]
        public void It_Returns_Empty_String_For_Parameter_Without_Default_Or_Option()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("greeting", "Hello, {{player}}")
                    .Build())
                .Boot();

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, "));
        }

        // ---- var/set Command Tests ----

        [Test]
        public void VarSet_Updates_Variable_In_Memory()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .WithCommand("greeting", "Hello, $PLAYER")
                    .Build())
                .Boot();

            var config = script.Mother.GetModule<Configuration>();

            var command = new TerminalCommand("var/set PLAYER Alex");
            var setCmd = new SetVariableCommand(config);
            setCmd.Execute(command);

            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Alex"));

            string resolved = script.Mother.SubstituteCommandParameters(script.Mother.ConfigCommands["greeting"], null);
            Assert.That(resolved, Is.EqualTo("Hello, Alex"));
        }

        [Test]
        public void VarSet_Does_Not_Persist_To_CustomData_Without_Save_Flag()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .Build())
                .Boot();

            var config = script.Mother.GetModule<Configuration>();

            string originalCustomData = script.Mother.ProgrammableBlock.CustomData;

            var command = new TerminalCommand("var/set PLAYER Alex");
            new SetVariableCommand(config).Execute(command);

            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Alex"));
            Assert.That(script.Mother.ProgrammableBlock.CustomData, Is.EqualTo(originalCustomData));
        }

        [Test]
        public void VarSet_Persists_To_CustomData_With_Save_Flag()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("PLAYER", "Luke")
                    .Build())
                .Boot();

            var config = script.Mother.GetModule<Configuration>();

            var command = new TerminalCommand("var/set PLAYER Alex --save");
            new SetVariableCommand(config).Execute(command);

            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Alex"));

            // Reload config from CustomData to verify persistence
            Configuration reloaded = new Configuration(script.Mother);
            reloaded.Boot();

            Assert.That(script.Mother.ConfigVariables["PLAYER"], Is.EqualTo("Alex"));
        }

        [Test]
        public void VarSet_Returns_Error_When_No_Arguments_Provided()
        {
            var script = ScriptFactory()
                .WithMother()
                .Boot();
            var config = script.Mother.GetModule<Configuration>();

            var command = new TerminalCommand("var/set");
            string result = new SetVariableCommand(config).Execute(command);

            Assert.That(result, Is.EqualTo(CommandBus.Messages.NoArgumentsProvided));
        }

        [Test]
        public void VarSet_Returns_Error_When_Only_Name_Provided()
        {
            var script = ScriptFactory()
                .WithMother()
                .Boot();
            var config = script.Mother.GetModule<Configuration>();

            var command = new TerminalCommand("var/set PLAYER");
            string result = new SetVariableCommand(config).Execute(command);

            Assert.That(result, Is.EqualTo(CommandBus.Messages.NoArgumentsProvided));
        }

        [Test]
        public void VarSet_Is_Registered_As_Module_Command()
        {
            var script = ScriptFactory()
                .WithMother()
                .Boot();
            var config = script.Mother.GetModule<Configuration>();

            var commands = config.GetCommands();
            Assert.That(commands.Exists(c => c.GetCommandName() == "var/set"), Is.True);
        }
    }
}

