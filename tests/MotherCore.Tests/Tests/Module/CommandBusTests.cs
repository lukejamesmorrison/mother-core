using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Debugging;

namespace MotherCore.Tests.Tests.Module
{
    [Category(TestCategories.LayerModule)]
    public class CommandBusTests : TestBase
    {
        // --- Command registration ---

        [Test]
        public void A_Module_Command_Can_Be_Registered()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            Assert.That(commandBus.ModuleCommands.Any(command => command is HelpCommand), Is.True);
        }

        [Test]
        public void A_Module_Command_Can_Be_Run_From_A_Terminal_Command()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void Multiple_Module_Commands_Can_Be_Registered()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();
            var clock = commandBus.Mother.GetModule<Clock>();


            int helpCountBefore = commandBus.ModuleCommands.Count(command => command is HelpCommand);
            int haltCountBefore = commandBus.ModuleCommands.Count(command => command is HaltCommand);

            commandBus.RegisterCommand(new HelpCommand(commandBus));
            commandBus.RegisterCommand(new HaltCommand(clock));

            Assert.That(commandBus.ModuleCommands.Count(command => command is HelpCommand), Is.EqualTo(helpCountBefore + 1));
            Assert.That(commandBus.ModuleCommands.Count(command => command is HaltCommand), Is.EqualTo(haltCountBefore + 1));
        }

        // --- Variable substitution ---

        [Test]
        public void It_Substitutes_Variables_In_Terminal_Input()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("BLOCK", "Light1")
                    .Build())
                .Boot();

            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            bool commandRun = commandBus.RunTerminalCommand("light/on $BLOCK");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Substitutes_Multiple_Variables_In_Terminal_Input()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithVariable("BLOCK", "Light1")
                    .WithVariable("COLOR", "red")
                    .Build())
                .Boot();

            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            bool commandRun = commandBus.RunTerminalCommand("light/color $BLOCK $COLOR");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Runs_Terminal_Command_Without_Variables_When_None_Defined()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }

        // --- RunTerminalCommand edge cases ---

        [Test]
        public void RunTerminalCommand_Returns_False_For_Empty_String()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();

            bool result = commandBus.RunTerminalCommand("");

            Assert.That(result, Is.False);
        }

        [Test]
        public void RunTerminalCommand_Handles_Semicolon_Separated_Commands()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();
            
            bool result = commandBus.RunTerminalCommand("help;help");

            Assert.That(result, Is.True);
        }

        // --- Boot behavior ---

        [Test]
        // The help command prints all available commands and
        // is an essential UX element.
        public void Boot_Registers_The_Help_Command()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();

            var helpCommand = commandBus.ModuleCommands
                .FirstOrDefault(c => c.GetCommandName() == "help");

            Assert.That(helpCommand, Is.Not.Null);
        }

        [Test]
        public void Boot_Clears_Construct_Commands()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();

            commandBus.ConstructCommands[999] = new HashSet<string> { "stale" };

            commandBus.Boot();

            Assert.That(commandBus.ConstructCommands.Count, Is.EqualTo(0));
        }

        // --- GetSelfCommandNames ---

        [Test]
        public void GetSelfCommandNames_Includes_Module_Commands()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Config_Commands()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("myCommand", "help")
                    .Build())
                .Boot();

            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("myCommand"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Both_Module_And_Config_Commands()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("myCommand", "help")
                    .Build())
                .Boot();

            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
            Assert.That(names, Contains.Item("myCommand"));
            Assert.That(names.Count(name => name == "myCommand"), Is.EqualTo(1));
            Assert.That(names.Count(name => name == "help"), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void GetSelfCommandNames_Returns_Empty_When_No_Commands()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = new CommandBus(script.Mother);

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names.Count, Is.EqualTo(0));
        }

        // --- RegisterRemoteCommands ---

        [Test]
        // Remote scripts are shared between Mother Core scripts to
        // allow construct-wide command execution. It is important
        // to remember which commands are defined on
        // which Programmable Blocks.
        public void RegisterRemoteCommands_Stores_Commands_For_Remote_Script()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId = script.Mother.Id + 1;
            var commands = new List<string> { "dock", "undock" };

            commandBus.RegisterRemoteCommands(remoteId, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(remoteId), Is.True);
            Assert.That(commandBus.ConstructCommands[remoteId], Is.EqualTo(commands));
        }

        [Test]  // C10
        public void RegisterRemoteCommands_With_Empty_List_Stores_Empty_Set()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId = script.Mother.Id + 1;

            // Should not throw and should store empty sets � not skip the entry entirely.
            Assert.DoesNotThrow(() => commandBus.RegisterRemoteCommands(remoteId, new List<string>()),
                "RegisterRemoteCommands with an empty list must not throw.");

            Assert.That(commandBus.ConstructCommands.ContainsKey(remoteId), Is.True,
                "ConstructCommands should contain an entry for the remote id even when the command list is empty.");
            Assert.That(commandBus.ConstructCommands[remoteId], Is.Empty,
                "The stored normal command set should be empty.");

            Assert.That(commandBus.ImportantConstructCommands.ContainsKey(remoteId), Is.True,
                "ImportantConstructCommands should contain an entry for the remote id even when the command list is empty.");
            Assert.That(commandBus.ImportantConstructCommands[remoteId], Is.Empty,
                "The stored important command set should be empty.");
        }

        [Test]
        public void RegisterRemoteCommands_Ignores_Self_Id()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            var commands = new List<string> { "dock" };

            commandBus.RegisterRemoteCommands(script.Mother.Id, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(script.Mother.Id), Is.False);
        }

        [Test]
        public void RegisterRemoteCommands_Overwrites_Existing_Entry()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId = script.Mother.Id + 1;

            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "old" });
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "new" });

            Assert.That(commandBus.ConstructCommands[remoteId].Count, Is.EqualTo(1));
            Assert.That(commandBus.ConstructCommands[remoteId].Contains("new"), Is.True);
        }

        // --- FindInstanceWithCommand ---

        [Test]
        public void FindInstanceWithCommand_Returns_Script_Id_When_Found()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId = script.Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "dock", "undock" });

            long result = commandBus.FindInstanceWithCommand("dock");

            Assert.That(result, Is.EqualTo(remoteId));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_Zero_When_Not_Found()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();

            long result = commandBus.FindInstanceWithCommand("nonexistent");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_First_Match_From_Multiple_Scripts()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId1 = script.Mother.Id + 1;
            long remoteId2 = script.Mother.Id + 2;

            commandBus.RegisterRemoteCommands(remoteId1, new List<string> { "dock" });
            commandBus.RegisterRemoteCommands(remoteId2, new List<string> { "undock" });

            Assert.That(commandBus.FindInstanceWithCommand("dock"), Is.EqualTo(remoteId1));
            Assert.That(commandBus.FindInstanceWithCommand("undock"), Is.EqualTo(remoteId2));
        }

        // --- Important commands (!prefix) ---

        [Test]
        public void RegisterRemoteCommands_Separates_Important_Commands()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId = script.Mother.Id + 1;
            var commands = new List<string> { "dock", "!undock", "status" };

            commandBus.RegisterRemoteCommands(remoteId, commands);

            Assert.That(commandBus.ConstructCommands[remoteId], Contains.Item("dock"));
            Assert.That(commandBus.ConstructCommands[remoteId], Contains.Item("status"));
            Assert.That(commandBus.ConstructCommands[remoteId], Does.Not.Contain("!undock"));
            Assert.That(commandBus.ConstructCommands[remoteId], Does.Not.Contain("undock"));

            Assert.That(commandBus.ImportantConstructCommands[remoteId], Contains.Item("undock"));
            Assert.That(commandBus.ImportantConstructCommands[remoteId], Does.Not.Contain("!undock"));
        }

        [Test]
        public void FindInstanceWithImportantCommand_Returns_Script_Id_When_Found()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId = script.Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "!dock", "!undock" });

            long result = commandBus.FindInstanceWithImportantCommand("dock");

            Assert.That(result, Is.EqualTo(remoteId));
        }

        [Test]
        public void FindInstanceWithImportantCommand_Returns_Zero_When_Not_Found()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();

            long result = commandBus.FindInstanceWithImportantCommand("nonexistent");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void FindInstanceWithImportantCommand_Does_Not_Find_Normal_Commands()
        {
            var script = ScriptFactory().WithMother().Boot();
            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            long remoteId = script.Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "dock" });

            long result = commandBus.FindInstanceWithImportantCommand("dock");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Boot_Clears_Important_Construct_Commands()
        {
            CommandBus commandBus = ModuleFactory<CommandBus>().Boot();

            commandBus.ImportantConstructCommands[999] = new HashSet<string> { "stale" };

            commandBus.Boot();

            Assert.That(commandBus.ImportantConstructCommands.Count, Is.EqualTo(0));
        }

        // --- Config command expansion ---

        [Test]
        public void RunTerminalCommand_Expands_Config_Command()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("myAction", "help")
                    .Build())
                .Boot();

            CommandBus commandBus = script.Mother.GetModule<CommandBus>();


            bool result = commandBus.RunTerminalCommand("myAction");

            Assert.That(result, Is.True);
        }

        [Test]
        public void RunTerminalCommand_Expands_Config_Command_With_Multiple_Steps()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("sequence", "help; help")
                    .Build())
                .Boot();

           CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            bool result = commandBus.RunTerminalCommand("sequence");

            Assert.That(result, Is.True);
        }

        // =====================================================================
        // Coroutine execution: structure and scheduling
        // =====================================================================

        /// <summary>
        /// A single command should produce exactly one coroutine.
        /// </summary>
        [Test]
        public void Single_Command_Creates_Exactly_One_Coroutine()
        {
            var script = ScriptFactory().WithMother().Boot();
            var clock = script.Mother.GetModule<Clock>();
            int bootCount = clock.CoroutineCount;

            script.RunTerminal("help");

            Assert.That(clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "A single command should add exactly one coroutine.");

            script.RunToIdle();
            script.AssertCommandExecuted("help");
        }

        /// <summary>
        /// Semicolon-separated commands (e.g. "a; b; c") should run
        /// sequentially within a single coroutine. Each individual command
        /// may itself expand to parallel groups, but the top-level sequence
        /// is strictly ordered.
        /// </summary>
        [Test]
        public void Semicolon_Commands_Run_Sequentially_In_One_Coroutine()
        {
            var script = ScriptFactory().WithMother().Boot();
            var commandBus = script.Mother.GetModule<CommandBus>();
            var clock = script.Mother.GetModule<Clock>();

            int bootCount = script.Clock.CoroutineCount;

            commandBus.RunTerminalCommand("help; help; help");

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "Semicolon-separated commands should share a single coroutine.");

            script.Tick();

            script.AssertCommandExecuted("help", 1);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(1),
                "First tick: only the first command should have executed.");

            script.Tick();

            script.AssertCommandExecuted("help", 2);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(2),
                "Second tick: the second command should have executed.");

            script.Tick();

            script.AssertCommandExecuted("help", 3);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(3),
                "Third tick: the third command should have executed.");
        }

        /// <summary>
        /// Parallel groups should each get their own coroutine.
        /// </summary>
        [Test]
        public void Parallel_Groups_Launch_One_Coroutine_Per_Group()
        {
            var script = ScriptFactory().WithMother().Boot();
            var commandBus = script.Mother.GetModule<CommandBus>();

            int bootCount = script.Clock.CoroutineCount;

            script.RunTerminal("{ help; } { help; } { help; }");

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 3),
                "Three parallel groups should launch three coroutines.");

            script.Clock.Tick();

            script.AssertCommandExecuted("help", 3);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(3),
                "All three parallel groups should execute on the same tick.");
        }

        // =====================================================================
        // C1 / C2: Force-local (!!) and underscore-prefix (_) local resolution
        // =====================================================================

        /// <summary>
        /// When a remote script owns a command as an important (! prefix) command,
        /// a plain invocation should delegate to the remote instance and NOT execute
        /// the local module command. Calling the same command with the !! force-local
        /// prefix should bypass the important construct command and execute locally.
        /// </summary>
        [Test]  // C1
        public void Force_Local_Bypasses_Important_Construct_Command()
        {
            var script = ScriptFactory().WithMother().Boot();
            var commandBus = script.Mother.GetModule<CommandBus>();

            // Register "help" as an important command on a remote construct instance.
            long remoteId = script.Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "!help" });

            // Without force-local the important construct command takes priority.
            commandBus.RunTerminalCommand("help");
            script.Tick();

            script.AssertCommandExecuted("help", 1, CommandExecutionOutcome.DelegatedToImportantConstruct);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(0),
                "Plain 'help' should be delegated to the remote important construct command, leaving the local command un-executed.");

            // With !! force-local prefix the important construct check is skipped and
            // the locally registered module command runs instead.
            commandBus.RunTerminalCommand("!!help");
            script.Tick();

            script.AssertCommandExecuted("help", 1);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(1),
                "Force-local '!!help' should execute the local command regardless of the " +
                "important construct command registered on the remote script.");
        }

        /// <summary>
        /// A command prefixed with _ (e.g. "_myAction") should always resolve the
        /// local config command unconditionally � before the important-construct check
        /// is even reached. This guarantees local execution even when an important
        /// construct command with the same base name is registered on a remote script.
        /// </summary>
        [Test]  // C2
        public void Underscore_Prefix_Resolves_Local_Config_Command()
        {
            var script = ScriptFactory<CoreTestProgram>()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("myAction", "help")
                    .Build()
                )
                .Boot();

            var commandBus = script.Mother.GetModule<CommandBus>();

            // Register "myAction" as an important command on a remote construct instance.
            long remoteId = script.Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "!myAction" });

            // Without the underscore the important construct command takes priority:
            // "myAction" is delegated to the remote script and the local tracker never runs.
            commandBus.RunTerminalCommand("myAction");
            script.Tick();

            script.AssertCommandExecuted("myAction", 1, CommandExecutionOutcome.DelegatedToImportantConstruct);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(0),
                "Plain 'myAction' should be delegated to the remote important construct command.");

            // With the _ prefix the local config command is resolved regardless of
            // what is registered on the construct.
            commandBus.RunTerminalCommand("_myAction");
            script.RunToIdle();

            script.AssertCommandExecuted("help", 1);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(1),
                "Underscore-prefixed '_myAction' should resolve and execute the local config command.");
        }

        // =====================================================================
        // C7 / C8: Unknown command message and halt command
        // =====================================================================

        /// <summary>
        /// Running an unrecognised command should print the CommandNotFound message
        /// (containing the command name) and must not throw an exception.
        /// </summary>
        [Test]  // C7
        public void Unknown_Command_Prints_CommandNotFound_And_Does_Not_Throw()
        {
            var script = ScriptFactory().WithMother().Boot();
            var capture = new PrintCapture(script);
            var terminal = script.Mother.GetModule<Terminal>();

            // Does not throw.
            Assert.DoesNotThrow(() =>
            {
                script.RunTerminal("nonexistent_cmd");
                script.Tick();
            }, "RunTerminalCommand should never throw for an unknown command.");

            // Terminal buffers messages; flush them through Echo so PrintCapture can see them.
            terminal.UpdateTerminal();

            // CommandNotFound message is printed, containing both the static prefix
            // and the offending command name.
            Assert.That(capture.Contains("Command not found"), Is.True,
                "CommandNotFound prefix should appear in the output.");

            Assert.That(capture.Contains("nonexistent_cmd"), Is.True,
                "The unrecognised command name should appear in the output.");
        }

        /// <summary>
        /// The halt command is registered on boot. Executing it should call
        /// Clock.Halt(), which clears all active coroutines and all queued tasks.
        /// HaltCommand.Execute() is called directly here to avoid the re-entrant
        /// dispose issue that occurs when Clock.Halt() is invoked from inside a
        /// running coroutine's MoveNext() call.
        /// </summary>
        /// <summary>
        /// Running the built-in "help" command should print a list of every command name
        /// that is registered on the CommandBus. "help" and "halt" are both registered
        /// on boot, so both names must appear in the captured output.
        /// </summary>
        [Test]  // C9
        public void Help_Command_Output_Lists_All_Registered_Commands()
        {
            var script = ScriptFactory().WithMother().Boot();
            var capture = new PrintCapture(script);
            var terminal = script.Mother.GetModule<Terminal>();

            script.RunTerminal("help");
            script.Run();

            // Terminal buffers messages; flush them through Echo so PrintCapture can see them.
            terminal.UpdateTerminal();

            // Both commands registered on boot must appear in the help output.
            Assert.That(capture.Contains("help"), Is.True,
                "Help output should include the 'help' command name.");

            Assert.That(capture.Contains("halt"), Is.True,
                "Help output should include the 'halt' command name.");
        }

        [Test]  // C8
        public void Halt_Command_Clears_All_Coroutines()
        {
            var script = ScriptFactory().WithMother().Boot();
            var clock = script.Mother.GetModule<Clock>();
            var commandBus = script.Mother.GetModule<CommandBus>();


            // Start long-running coroutines so there is something to clear.
            script.RunTerminal("help; wait 100; rename HaltedAlpha");
            script.RunTerminal("help; wait 100; rename HaltedBeta");

            // advance past first command into wait state
            script.Run();

            Assert.That(script.Clock.CoroutineCount, Is.GreaterThan(0),
                "Coroutines should be active before halt is called.");

            // Queue a deferred task so we can assert halt clears it too.
            int queuedBefore = clock.QueuedTaskCount;
            clock.QueueForLater(() => { }, 5.0);
            Assert.That(clock.QueuedTaskCount, Is.EqualTo(queuedBefore + 1));

            // Verify halt is registered on boot.
            var haltCommand = commandBus.ModuleCommands
                .First(c => c.GetCommandName() == "halt") as HaltCommand;

            Assert.That(haltCommand, Is.Not.Null,
                "HaltCommand should be registered on boot.");

            // Call Execute() directly � tests Clock.Halt() without triggering
            // the re-entrant dispose that happens when Halt() runs inside a coroutine.
            haltCommand.Execute(new TerminalCommand("halt"));

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(0),
                "Halt command should clear all active coroutines.");

            Assert.That(clock.QueuedTaskCount, Is.EqualTo(0),
                "Halt command should also clear all queued tasks.");
        }

        /// <summary>
        /// A command string consisting entirely of whitespace passes the naive
        /// <c>commandString.Length &gt; 0</c> check but produces an empty token after
        /// trimming, which previously caused an <c>ArgumentOutOfRangeException</c> deep
        /// inside <c>TerminalCommand</c>. <c>RunTerminalCommand</c> must guard against
        /// whitespace-only input and return <c>false</c> cleanly.
        /// </summary>
        [Test]  // C11
        public void RunTerminalCommand_With_Only_Whitespace_Returns_False()
        {
            var script = ScriptFactory().WithMother().Boot();
            var commandBus = script.Mother.GetModule<CommandBus>();
            int bootCount = script.Clock.CoroutineCount;

            bool result = false;
            Assert.DoesNotThrow(() => result = commandBus.RunTerminalCommand("   "),
                "RunTerminalCommand must not throw for a whitespace-only string.");

            Assert.That(result, Is.False,
                "Whitespace-only input should return false, the same as an empty string.");

            // No coroutine should have been queued by the command.
            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount),
                "No coroutine should be launched for a whitespace-only command string.");
        }

        // =====================================================================
        // C5 / C6: Wait timing and config command expansion to parallel groups
        // =====================================================================

        /// <summary>
        /// A wait command inside a sequential routine should block all commands that
        /// follow it within the same coroutine for the specified duration.  Commands
        /// before the wait run on their normal tick; commands after the wait must not
        /// run until sufficient simulated time has elapsed.
        /// </summary>
        [Test]  // C5
        public void Wait_Blocks_Subsequent_Commands_In_Same_Coroutine()
        {
            var script = ScriptFactory().WithMother().Boot();
            var commandBus = script.Mother.GetModule<CommandBus>();

            var fakeRuntime = script.Mother.Program.Runtime as FakeGridProgramRuntimeInfo;

            script.RunTerminal("help; wait 2; rename CarrierRenamed");

            // Tick 1: first "help" executes.
            script.Run();
            script.AssertCommandExecuted("help", 1);
            Assert.That(script.Mother.Name, Is.Not.EqualTo("CarrierRenamed"),
                "Tick 1: rename should not have executed yet.");

            // Tick 2: wait 2 starts � coroutine yields 2.0s, no new execution.
            script.Run();
            script.AssertCommandExecuted("wait", 1, CommandExecutionOutcome.WaitScheduled);
            Assert.That(script.Mother.Name, Is.Not.EqualTo("CarrierRenamed"),
                "Tick 2: wait started, rename must not execute yet.");

            // Tick 3 (delta=0): wait still active � still blocked.
            script.Run();
            Assert.That(script.Mother.Name, Is.Not.EqualTo("CarrierRenamed"),
                "Tick 3: wait still active (deltaTime=0), rename must not execute.");

            // Advance simulated time past the 2-second wait threshold.
            Assert.That(fakeRuntime, Is.Not.Null);
            fakeRuntime.TimeSinceLastRun = TimeSpan.FromSeconds(2.1);
            script.RunToIdle();

            script.AssertCommandExecuted("rename", 1);
            Assert.That(script.Mother.Name, Is.EqualTo("CarrierRenamed"),
                "After the 2s wait expires, the rename command should execute.");
        }

        /// <summary>
        /// When a config command expands to a routine that contains parallel groups,
        /// the expansion should launch one coroutine per group � not collapse them
        /// into a single sequential coroutine.
        /// </summary>
        [Test]  // C6
        public void Config_Command_Expanding_To_Parallel_Groups_Launches_Multiple_Coroutines()
        {
            var script = ScriptFactory()
                .WithMother()
                // Config command whose value is a parallel-group routine.
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("par", "{ help; } { help; }")
                    .Build())
                .Boot();

            var commandBus = script.Mother.GetModule<CommandBus>();

            int bootCount = script.Clock.CoroutineCount;

            script.Bus.RunTerminalCommand("par");

            // Expansion may be eager or lazy depending on harness execution path,
            // but at least one coroutine must be queued for the routine.
            Assert.That(script.Clock.CoroutineCount, Is.GreaterThanOrEqualTo(bootCount + 1),
                "Config command expansion should enqueue coroutine work.");

            script.RunToIdle();

            script.AssertCommandExecuted("help", 2);
            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(2),
                "Both parallel groups should have each executed their 'help' command.");
        }

        /// <summary>
        /// A <c>wait</c> inside one parallel group must only block that group's
        /// coroutine.  The sibling group runs its commands immediately and must
        /// not stall while the first group is waiting.
        /// </summary>
        [Test]  // C12
        public void Wait_In_Parallel_Group_Does_Not_Block_Other_Parallel_Group()
        {
            var script = ScriptFactory().WithMother().Boot();
            var commandBus = script.Mother.GetModule<CommandBus>();

            int bootCount = script.Clock.CoroutineCount;
            var fakeRuntime = script.Mother.Program.Runtime as FakeGridProgramRuntimeInfo;

            // Group 1: wait 2 seconds, then rename.
            // Group 2: help immediately.
            script.RunTerminal("{ wait 2; rename ParallelLate; } { help; }");

            // Both coroutines are launched before any tick.
            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 2),
                "Two coroutines should be active � one per parallel group.");

            // Tick 1: group 1 hits 'wait 2' and yields; group 2 runs 'track' and yields 0.
            script.Run();

            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(1),
                "Tick 1: only group 2's 'help' should have run; group 1 is blocked by its wait.");
            Assert.That(script.Mother.Name, Is.Not.EqualTo("ParallelLate"));

            // Fast branch may be collected in the same cycle once it completes.
            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "Tick 1: completed sibling branch should be collected; waiting branch remains.");

            // Tick 2 (delta=0): group 2 drains and is removed; group 1 is still waiting.
            script.Run();

            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(1),
                "Tick 2: group 2 is being collected; group 1's wait is still active.");

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "Tick 2: group 2 should have been collected; only group 1 remains.");

            // Advance simulated time past the 2-second wait threshold.
            Assert.That(fakeRuntime, Is.Not.Null);
            fakeRuntime.TimeSinceLastRun = TimeSpan.FromSeconds(2.1);
            script.RunToIdle();

            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(1),
                "After the 2s wait expires, the immediate parallel branch should still only have run once.");
            Assert.That(script.Mother.Name, Is.EqualTo("ParallelLate"),
                "After the 2s wait expires, group 1's rename should execute.");
        }

        // =====================================================================
        // C3 / C4: Important config command resolution and name listing
        // =====================================================================

        /// <summary>
        /// A config command stored with a ! prefix (e.g. ConfigCommands["!dock"]) should
        /// be resolved when the player runs the base name ("dock") and no construct
        /// instance owns an important command with that name. This exercises the
        /// final "!" + command.Name branch in ResolveConfigCommand.
        /// </summary>
        [Test]  // C3
        public void Important_Config_Command_Is_Resolved_When_No_Construct_Owner()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("!dock", "help")
                    .Build())
                .Boot();

            var commandBus = script.Mother.GetModule<CommandBus>();


            script.RunTerminal("dock");
            script.RunToIdle();

            Assert.That(commandBus.GetExecutionCount("help"), Is.EqualTo(1),
                "'dock' should resolve via the '!dock' config command entry when no construct instance owns it.");
        }

        /// <summary>
        /// GetSelfCommandNames iterates ConfigCommands keys as-is, so a key stored
        /// with the ! prefix (e.g. "!dock") must appear in the returned list. This
        /// matters because the list is broadcast to other Mother Core instances via
        /// RegisterRemoteCommands, which uses the ! prefix to classify commands as
        /// important on the receiving end.
        /// </summary>
        [Test]  // C4
        public void GetSelfCommandNames_Includes_Important_Config_Command_With_Bang_Prefix()
        {
            var script = ScriptFactory()
                .WithMother()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("!dock", "help")
                    .Build())
                .Boot();

            CommandBus commandBus = script.Mother.GetModule<CommandBus>();

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("!dock"),
                "GetSelfCommandNames should include the '!dock' key exactly as stored, " +
                "so that receiving scripts can classify it as an important command.");
        }

    }
}





