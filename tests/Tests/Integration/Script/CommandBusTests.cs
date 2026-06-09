using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.Integration.Script
{
    public class CommandBusTests : ScriptTestBase<TestProgram>
    {
        // --- Construction ---

        [Test]
        public void It_Can_Be_Instantiated_With_An_Instance_Of_Mother()
        {
            CommandBus commandBus = new CommandBus(Mother);

            Assert.That(commandBus.Mother, Is.SameAs(Mother));
        }

        [Test]
        public void It_Can_Be_Booted()
        {
            CommandBus commandBus = new CommandBus(Mother);

            commandBus.Boot();

            Assert.Pass();
        }

        // --- Command registration ---

        [Test]
        public void A_Module_Command_Can_Be_Registered()
        {
            CommandBus commandBus = new CommandBus(Mother);

            commandBus.RegisterCommand(new HelpCommand(commandBus));

            Assert.That(commandBus.ModuleCommands.Count, Is.EqualTo(1));
        }

        [Test]
        public void A_Module_Command_Can_Be_Run_From_A_Terminal_Command()
        {
            CommandBus commandBus = new CommandBus(Mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void Multiple_Module_Commands_Can_Be_Registered()
        {
            CommandBus commandBus = new CommandBus(Mother);

            commandBus.RegisterCommand(new HelpCommand(commandBus));
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            Assert.That(commandBus.ModuleCommands.Count, Is.EqualTo(2));
        }

        // --- Variable substitution ---

        [Test]
        public void It_Substitutes_Variables_In_Terminal_Input()
        {
            Mother.ConfigVariables["BLOCK"] = "Light1";

            CommandBus commandBus = new CommandBus(Mother);
            commandBus.Boot();

            bool commandRun = commandBus.RunTerminalCommand("light/on $BLOCK");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Substitutes_Multiple_Variables_In_Terminal_Input()
        {
            Mother.ConfigVariables["BLOCK"] = "Light1";
            Mother.ConfigVariables["COLOR"] = "red";

            CommandBus commandBus = new CommandBus(Mother);
            commandBus.Boot();

            bool commandRun = commandBus.RunTerminalCommand("light/color $BLOCK $COLOR");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Runs_Terminal_Command_Without_Variables_When_None_Defined()
        {
            CommandBus commandBus = new CommandBus(Mother);
            commandBus.Boot();
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }

        // --- RunTerminalCommand edge cases ---

        [Test]
        public void RunTerminalCommand_Returns_False_For_Empty_String()
        {
            CommandBus commandBus = new CommandBus(Mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("");

            Assert.That(result, Is.False);
        }

        [Test]
        public void RunTerminalCommand_Handles_Semicolon_Separated_Commands()
        {
            CommandBus commandBus = new CommandBus(Mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("help;help");

            Assert.That(result, Is.True);
        }

        // --- Boot behavior ---

        [Test]
        // The help command prints all available commands and
        // is an essential UX element.
        public void Boot_Registers_The_Help_Command()
        {
            CommandBus commandBus = new CommandBus(Mother);

            commandBus.Boot();

            var helpCommand = commandBus.ModuleCommands
                .FirstOrDefault(c => c.GetCommandName() == "help");

            Assert.That(helpCommand, Is.Not.Null);
        }

        [Test]
        public void Boot_Clears_Construct_Commands()
        {
            CommandBus commandBus = new CommandBus(Mother);

            commandBus.ConstructCommands[999] = new HashSet<string> { "stale" };

            commandBus.Boot();

            Assert.That(commandBus.ConstructCommands.Count, Is.EqualTo(0));
        }

        // --- GetSelfCommandNames ---

        [Test]
        public void GetSelfCommandNames_Includes_Module_Commands()
        {
            CommandBus commandBus = new CommandBus(Mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Config_Commands()
        {
            Mother.ConfigCommands["myCommand"] = "help";

            CommandBus commandBus = new CommandBus(Mother);

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("myCommand"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Both_Module_And_Config_Commands()
        {
            Mother.ConfigCommands["myCommand"] = "help";

            CommandBus commandBus = new CommandBus(Mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
            Assert.That(names, Contains.Item("myCommand"));
            Assert.That(names.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetSelfCommandNames_Returns_Empty_When_No_Commands()
        {
            CommandBus commandBus = new CommandBus(Mother);

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
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId = Mother.Id + 1;
            var commands = new List<string> { "dock", "undock" };

            commandBus.RegisterRemoteCommands(remoteId, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(remoteId), Is.True);
            Assert.That(commandBus.ConstructCommands[remoteId], Is.EqualTo(commands));
        }

        [Test]  // C10
        public void RegisterRemoteCommands_With_Empty_List_Stores_Empty_Set()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId = Mother.Id + 1;

            // Should not throw and should store empty sets — not skip the entry entirely.
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
            CommandBus commandBus = new CommandBus(Mother);

            var commands = new List<string> { "dock" };

            commandBus.RegisterRemoteCommands(Mother.Id, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(Mother.Id), Is.False);
        }

        [Test]
        public void RegisterRemoteCommands_Overwrites_Existing_Entry()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId = Mother.Id + 1;

            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "old" });
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "new" });

            Assert.That(commandBus.ConstructCommands[remoteId].Count, Is.EqualTo(1));
            Assert.That(commandBus.ConstructCommands[remoteId].Contains("new"), Is.True);
        }

        // --- FindInstanceWithCommand ---

        [Test]
        public void FindInstanceWithCommand_Returns_Script_Id_When_Found()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId = Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "dock", "undock" });

            long result = commandBus.FindInstanceWithCommand("dock");

            Assert.That(result, Is.EqualTo(remoteId));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_Zero_When_Not_Found()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long result = commandBus.FindInstanceWithCommand("nonexistent");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_First_Match_From_Multiple_Scripts()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId1 = Mother.Id + 1;
            long remoteId2 = Mother.Id + 2;

            commandBus.RegisterRemoteCommands(remoteId1, new List<string> { "dock" });
            commandBus.RegisterRemoteCommands(remoteId2, new List<string> { "undock" });

            Assert.That(commandBus.FindInstanceWithCommand("dock"), Is.EqualTo(remoteId1));
            Assert.That(commandBus.FindInstanceWithCommand("undock"), Is.EqualTo(remoteId2));
        }

        // --- Important commands (!prefix) ---

        [Test]
        public void RegisterRemoteCommands_Separates_Important_Commands()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId = Mother.Id + 1;
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
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId = Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "!dock", "!undock" });

            long result = commandBus.FindInstanceWithImportantCommand("dock");

            Assert.That(result, Is.EqualTo(remoteId));
        }

        [Test]
        public void FindInstanceWithImportantCommand_Returns_Zero_When_Not_Found()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long result = commandBus.FindInstanceWithImportantCommand("nonexistent");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void FindInstanceWithImportantCommand_Does_Not_Find_Normal_Commands()
        {
            CommandBus commandBus = new CommandBus(Mother);

            long remoteId = Mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "dock" });

            long result = commandBus.FindInstanceWithImportantCommand("dock");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Boot_Clears_Important_Construct_Commands()
        {
            CommandBus commandBus = new CommandBus(Mother);

            commandBus.ImportantConstructCommands[999] = new HashSet<string> { "stale" };

            commandBus.Boot();

            Assert.That(commandBus.ImportantConstructCommands.Count, Is.EqualTo(0));
        }

        // --- Config command expansion ---

        [Test]
        public void RunTerminalCommand_Expands_Config_Command()
        {
            Mother.ConfigCommands["myAction"] = "help";

            CommandBus commandBus = new CommandBus(Mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("myAction");

            Assert.That(result, Is.True);
        }

        [Test]
        public void RunTerminalCommand_Expands_Config_Command_With_Multiple_Steps()
        {
            Mother.ConfigCommands["sequence"] = "help;help";

            CommandBus commandBus = new CommandBus(Mother);
            commandBus.Boot();

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
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();
            int bootCount = script.Clock.CoroutineCount;

            script.Bus.RunTerminalCommand("track");

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "A single command should add exactly one coroutine.");

            script.Clock.Tick(2);

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1));
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
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();
            int bootCount = script.Clock.CoroutineCount;

            script.Bus.RunTerminalCommand("track; track; track");

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "Semicolon-separated commands should share a single coroutine.");

            script.Clock.Tick();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "First tick: only the first command should have executed.");

            script.Clock.Tick();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "Second tick: the second command should have executed.");

            script.Clock.Tick();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "Third tick: the third command should have executed.");
        }

        /// <summary>
        /// Parallel groups should each get their own coroutine.
        /// </summary>
        [Test]
        public void Parallel_Groups_Launch_One_Coroutine_Per_Group()
        {
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();
            int bootCount = script.Clock.CoroutineCount;

            script.Bus.RunTerminalCommand("{ track; } { track; } { track; }");

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 3),
                "Three parallel groups should launch three coroutines.");

            script.Clock.Tick();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
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
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();

            // Register "track" as an important command on a remote construct instance.
            long remoteId = script.Mother.Id + 1;
            script.Bus.RegisterRemoteCommands(remoteId, new List<string> { "!track" });

            // Without force-local the important construct command takes priority:
            // ExecutePrimitiveCommand delegates to the remote script and the local
            // tracker is never invoked.
            script.Bus.RunTerminalCommand("track");
            script.Clock.Tick();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(0),
                "Plain 'track' should be delegated to the remote important construct command, " +
                "leaving the local tracker un-executed.");

            // With !! force-local prefix the important construct check is skipped and
            // the locally registered module command runs instead.
            script.Bus.RunTerminalCommand("!!track");
            script.Clock.Tick();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Force-local '!!track' should execute the local command regardless of the " +
                "important construct command registered on the remote script.");
        }

        /// <summary>
        /// A command prefixed with _ (e.g. "_myAction") should always resolve the
        /// local config command unconditionally — before the important-construct check
        /// is even reached. This guarantees local execution even when an important
        /// construct command with the same base name is registered on a remote script.
        /// </summary>
        [Test]  // C2
        public void Underscore_Prefix_Resolves_Local_Config_Command()
        {
            var tracker = new TrackingCommand();

            var script = new Script<TestProgram>()
                .WithCustomData(new CustomDataBuilder()
                    .WithCommand("myAction", "track")
                    .Build())
                .WithCommands(tracker)
                .Boot();

            // Register "myAction" as an important command on a remote construct instance.
            long remoteId = script.Mother.Id + 1;
            script.Bus.RegisterRemoteCommands(remoteId, new List<string> { "!myAction" });

            // Without the underscore the important construct command takes priority:
            // "myAction" is delegated to the remote script and the local tracker never runs.
            script.Bus.RunTerminalCommand("myAction");
            script.Clock.Tick();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(0),
                "Plain 'myAction' should be delegated to the remote important construct command.");

            // With the _ prefix the local config command is resolved regardless of
            // what is registered on the construct.
            script.Bus.RunTerminalCommand("_myAction");
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
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
            var script = new Script<TestProgram>().Boot();
            var capture = new PrintCapture(script);
            var terminal = script.Mother.GetModule<Terminal>();

            // Does not throw.
            Assert.DoesNotThrow(() =>
            {
                script.Bus.RunTerminalCommand("nonexistent_cmd");
                script.Clock.Tick(2);
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
            var script = new Script<TestProgram>().Boot();
            var capture = new PrintCapture(script);
            var terminal = script.Mother.GetModule<Terminal>();

            script.Bus.RunTerminalCommand("help");
            script.Clock.Tick(2);

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
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();
            var clock = script.Mother.GetModule<Clock>();

            // Start long-running coroutines so there is something to clear.
            script.Bus.RunTerminalCommand("track; wait 100; track");
            script.Bus.RunTerminalCommand("track; wait 100; track");
            script.Clock.Tick(2); // advance past first command into wait state

            Assert.That(script.Clock.CoroutineCount, Is.GreaterThan(0),
                "Coroutines should be active before halt is called.");

            // Queue a deferred task so we can assert halt clears it too.
            int queuedBefore = clock.QueuedTaskCount;
            clock.QueueForLater(() => { }, 5.0);
            Assert.That(clock.QueuedTaskCount, Is.EqualTo(queuedBefore + 1));

            // Verify halt is registered on boot.
            var haltCommand = script.Bus.ModuleCommands
                .First(c => c.GetCommandName() == "halt") as HaltCommand;

            Assert.That(haltCommand, Is.Not.Null,
                "HaltCommand should be registered on boot.");

            // Call Execute() directly — tests Clock.Halt() without triggering
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
            var script = new Script<TestProgram>().Boot();
            int bootCount = script.Clock.CoroutineCount;

            bool result = false;
            Assert.DoesNotThrow(() => result = script.Bus.RunTerminalCommand("   "),
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
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();
            var fakeRuntime = script.Mother.Program.Runtime;

            script.Bus.RunTerminalCommand("track; wait 2; track");

            // Tick 1: first "track" executes.
            script.Clock.Tick();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Tick 1: first 'track' should have executed.");

            // Tick 2: wait 2 starts — coroutine yields 2.0s, no new execution.
            script.Clock.Tick();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Tick 2: wait started, second 'track' must not execute yet.");

            // Tick 3 (delta=0): wait still active — still blocked.
            script.Clock.Tick();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Tick 3: wait still active (deltaTime=0), second 'track' must not execute.");

            // Advance simulated time past the 2-second wait threshold.
            A.CallTo(() => fakeRuntime.TimeSinceLastRun).Returns(TimeSpan.FromSeconds(2.1));
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "After the 2s wait expires, the second 'track' should execute.");
        }

        /// <summary>
        /// When a config command expands to a routine that contains parallel groups,
        /// the expansion should launch one coroutine per group — not collapse them
        /// into a single sequential coroutine.
        /// </summary>
        [Test]  // C6
        public void Config_Command_Expanding_To_Parallel_Groups_Launches_Multiple_Coroutines()
        {
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();
            int bootCount = script.Clock.CoroutineCount;

            // Config command whose value is a parallel-group routine.
            script.Mother.ConfigCommands["par"] = "{ track; } { track; }";

            script.Bus.RunTerminalCommand("par");

            // One coroutine holds the unexpanded "par" command.
            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "Before expansion: exactly one coroutine for the 'par' command.");

            // Tick 1: 'par' expands, the two parallel groups are launched as separate
            // coroutines, and the original 'par' coroutine finishes.
            script.Clock.Tick();

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 2),
                "After expansion: two coroutines — one per parallel group.");

            // Tick 2: both group coroutines run.
            script.Clock.Tick();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "Both parallel groups should have each executed their 'track' command.");
        }

        /// <summary>
        /// A <c>wait</c> inside one parallel group must only block that group's
        /// coroutine.  The sibling group runs its commands immediately and must
        /// not stall while the first group is waiting.
        /// </summary>
        [Test]  // C12
        public void Wait_In_Parallel_Group_Does_Not_Block_Other_Parallel_Group()
        {
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();
            int bootCount = script.Clock.CoroutineCount;
            var fakeRuntime = script.Mother.Program.Runtime;

            // Group 1: wait 2 seconds, then track.
            // Group 2: track immediately.
            script.Bus.RunTerminalCommand("{ wait 2; track; } { track; }");

            // Both coroutines are launched before any tick.
            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 2),
                "Two coroutines should be active — one per parallel group.");

            // Tick 1: group 1 hits 'wait 2' and yields; group 2 runs 'track' and yields 0.
            script.Clock.Tick();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Tick 1: only group 2's 'track' should have run; group 1 is blocked by its wait.");

            // Group 2 yielded 0 this tick, so the clock needs one more MoveNext() to
            // confirm it is exhausted.  Both coroutines are still in the list.
            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 2),
                "Tick 1: group 2 yielded 0 and needs one more tick to be collected.");

            // Tick 2 (delta=0): group 2 drains and is removed; group 1 is still waiting.
            script.Clock.Tick();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Tick 2: group 2 is being collected; group 1's wait is still active.");

            Assert.That(script.Clock.CoroutineCount, Is.EqualTo(bootCount + 1),
                "Tick 2: group 2 should have been collected; only group 1 remains.");

            // Advance simulated time past the 2-second wait threshold.
            A.CallTo(() => fakeRuntime.TimeSinceLastRun).Returns(TimeSpan.FromSeconds(2.1));
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "After the 2s wait expires, group 1's 'track' should execute, bringing the total to 2.");
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
            var tracker = new TrackingCommand();
            var script = new Script<TestProgram>().WithCommands(tracker).Boot();

            // Register the important config command directly — no construct owner for "dock".
            script.Mother.ConfigCommands["!dock"] = "track";

            script.Bus.RunTerminalCommand("dock");
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
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
            Mother.ConfigCommands["!dock"] = "help";

            CommandBus commandBus = new CommandBus(Mother);

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("!dock"),
                "GetSelfCommandNames should include the '!dock' key exactly as stored, " +
                "so that receiving scripts can classify it as an important command.");
        }

    }
}


