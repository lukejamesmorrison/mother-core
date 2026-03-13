using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.Tests
{
    public class CommandBusTests : BaseModuleTests
    {
        // --- Construction ---

        [Test]
        public void It_Can_Be_Instantiated_With_An_Instance_Of_Mother()
        {
            CommandBus commandBus = new CommandBus(_mother);

            Assert.That(commandBus.Mother, Is.SameAs(_mother));
        }

        [Test]
        public void It_Can_Be_Booted()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.Boot();

            Assert.Pass();
        }

        // --- Command registration ---

        [Test]
        public void A_Module_Command_Can_Be_Registered()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.RegisterCommand(new HelpCommand(commandBus));

            Assert.That(commandBus.ModuleCommands.Count, Is.EqualTo(1));
        }

        [Test]
        public void A_Module_Command_Can_Be_Run_From_A_Terminal_Command()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void Multiple_Module_Commands_Can_Be_Registered()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.RegisterCommand(new HelpCommand(commandBus));
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            Assert.That(commandBus.ModuleCommands.Count, Is.EqualTo(2));
        }

        // --- Variable substitution ---

        [Test]
        public void It_Substitutes_Variables_In_Terminal_Input()
        {
            _mother.ConfigVariables["BLOCK"] = "Light1";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool commandRun = commandBus.RunTerminalCommand("light/on $BLOCK");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Substitutes_Multiple_Variables_In_Terminal_Input()
        {
            _mother.ConfigVariables["BLOCK"] = "Light1";
            _mother.ConfigVariables["COLOR"] = "red";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool commandRun = commandBus.RunTerminalCommand("light/color $BLOCK $COLOR");

            Assert.That(commandRun, Is.True);
        }

        [Test]
        public void It_Runs_Terminal_Command_Without_Variables_When_None_Defined()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            bool commandRun = commandBus.RunTerminalCommand("help");

            Assert.That(commandRun, Is.True);
        }

        // --- RunTerminalCommand edge cases ---

        [Test]
        public void RunTerminalCommand_Returns_False_For_Empty_String()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("");

            Assert.That(result, Is.False);
        }

        [Test]
        public void RunTerminalCommand_Handles_Semicolon_Separated_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);
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
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.Boot();

            var helpCommand = commandBus.ModuleCommands
                .FirstOrDefault(c => c.GetCommandName() == "help");

            Assert.That(helpCommand, Is.Not.Null);
        }

        [Test]
        public void Boot_Clears_Construct_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);

            commandBus.ConstructCommands[999] = new List<string> { "stale" };

            commandBus.Boot();

            Assert.That(commandBus.ConstructCommands.Count, Is.EqualTo(0));
        }

        // --- GetSelfCommandNames ---

        [Test]
        public void GetSelfCommandNames_Includes_Module_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Config_Commands()
        {
            _mother.ConfigCommands["myCommand"] = "help";

            CommandBus commandBus = new CommandBus(_mother);

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("myCommand"));
        }

        [Test]
        public void GetSelfCommandNames_Includes_Both_Module_And_Config_Commands()
        {
            _mother.ConfigCommands["myCommand"] = "help";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.RegisterCommand(new HelpCommand(commandBus));

            List<string> names = commandBus.GetSelfCommandNames();

            Assert.That(names, Contains.Item("help"));
            Assert.That(names, Contains.Item("myCommand"));
            Assert.That(names.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetSelfCommandNames_Returns_Empty_When_No_Commands()
        {
            CommandBus commandBus = new CommandBus(_mother);

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
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId = _mother.Id + 1;
            var commands = new List<string> { "dock", "undock" };

            commandBus.RegisterRemoteCommands(remoteId, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(remoteId), Is.True);
            Assert.That(commandBus.ConstructCommands[remoteId], Is.EqualTo(commands));
        }

        [Test]
        public void RegisterRemoteCommands_Ignores_Self_Id()
        {
            CommandBus commandBus = new CommandBus(_mother);

            var commands = new List<string> { "dock" };

            commandBus.RegisterRemoteCommands(_mother.Id, commands);

            Assert.That(commandBus.ConstructCommands.ContainsKey(_mother.Id), Is.False);
        }

        [Test]
        public void RegisterRemoteCommands_Overwrites_Existing_Entry()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId = _mother.Id + 1;

            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "old" });
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "new" });

            Assert.That(commandBus.ConstructCommands[remoteId].Count, Is.EqualTo(1));
            Assert.That(commandBus.ConstructCommands[remoteId][0], Is.EqualTo("new"));
        }

        // --- FindInstanceWithCommand ---

        [Test]
        public void FindInstanceWithCommand_Returns_Script_Id_When_Found()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId = _mother.Id + 1;
            commandBus.RegisterRemoteCommands(remoteId, new List<string> { "dock", "undock" });

            long result = commandBus.FindInstanceWithCommand("dock");

            Assert.That(result, Is.EqualTo(remoteId));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_Zero_When_Not_Found()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long result = commandBus.FindInstanceWithCommand("nonexistent");

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void FindInstanceWithCommand_Returns_First_Match_From_Multiple_Scripts()
        {
            CommandBus commandBus = new CommandBus(_mother);

            long remoteId1 = _mother.Id + 1;
            long remoteId2 = _mother.Id + 2;

            commandBus.RegisterRemoteCommands(remoteId1, new List<string> { "dock" });
            commandBus.RegisterRemoteCommands(remoteId2, new List<string> { "undock" });

            Assert.That(commandBus.FindInstanceWithCommand("dock"), Is.EqualTo(remoteId1));
            Assert.That(commandBus.FindInstanceWithCommand("undock"), Is.EqualTo(remoteId2));
        }

        // --- Config command expansion ---

        [Test]
        public void RunTerminalCommand_Expands_Config_Command()
        {
            _mother.ConfigCommands["myAction"] = "help";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("myAction");

            Assert.That(result, Is.True);
        }

        [Test]
        public void RunTerminalCommand_Expands_Config_Command_With_Multiple_Steps()
        {
            _mother.ConfigCommands["sequence"] = "help;help";

            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();

            bool result = commandBus.RunTerminalCommand("sequence");

            Assert.That(result, Is.True);
        }

        // --- WaypointRoutineQueue ---

        [Test]
        public void WaypointRoutineQueue_Is_Initialized_On_Construction()
        {
            CommandBus commandBus = new CommandBus(_mother);

            Assert.That(commandBus.WaypointRoutineQueue, Is.Not.Null);
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
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            commandBus.RunTerminalCommand("track");

            Assert.That(clock.CoroutineCount, Is.EqualTo(1),
                "A single command should add exactly one coroutine.");

            clock.Run();
            clock.Run();

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
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            commandBus.RunTerminalCommand("track; track; track");

            Assert.That(clock.CoroutineCount, Is.EqualTo(1),
                "Semicolon-separated commands should share a single coroutine.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "First tick: only the first command should have executed.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "Second tick: the second command should have executed.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "Third tick: the third command should have executed.");
        }

        /// <summary>
        /// Parallel groups should each get their own coroutine.
        /// </summary>
        [Test]
        public void Parallel_Groups_Launch_One_Coroutine_Per_Group()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            commandBus.RunTerminalCommand("{ track; } { track; } { track; }");

            Assert.That(clock.CoroutineCount, Is.EqualTo(3),
                "Three parallel groups should launch three coroutines.");

            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "All three parallel groups should execute on the same tick.");
        }

        /// <summary>
        /// Commands within a single parallel group run sequentially inside
        /// the same coroutine — they share one coroutine, not one each.
        /// </summary>
        [Test]
        public void Commands_Within_A_Group_Share_A_Single_Coroutine()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            commandBus.RunTerminalCommand("{ track; track; track; }");

            Assert.That(clock.CoroutineCount, Is.EqualTo(1),
                "A single parallel group with 3 commands should launch only 1 coroutine.");

            clock.Run();
            clock.Run();
            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "All three sequential commands in the group should execute.");
        }

        // =====================================================================
        // Coroutine execution: config command expansion
        // =====================================================================

        /// <summary>
        /// A config command that expands to sequential steps should be inlined
        /// into a single coroutine, not spawned as separate coroutines.
        /// </summary>
        [Test]
        public void Config_Command_Sequential_Steps_Produce_One_Coroutine()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            _mother.ConfigCommands["mySeq"] = "track; track; track";

            commandBus.RunTerminalCommand("mySeq");

            Assert.That(clock.CoroutineCount, Is.EqualTo(1),
                "Config command expanding to sequential steps should produce one coroutine.");

            clock.Run();
            clock.Run();
            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "All three inlined steps should execute.");
        }

        /// <summary>
        /// A config command containing parallel groups should spawn
        /// additional coroutines from within the parent coroutine
        /// during its first tick.
        /// </summary>
        [Test]
        public void Config_Command_Parallel_Groups_Launch_Separate_Coroutines()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            _mother.ConfigCommands["myParallel"] = "{ track; } { track; }";

            commandBus.RunTerminalCommand("myParallel");

            Assert.That(clock.CoroutineCount, Is.EqualTo(1),
                "Before first tick, only the parent coroutine should exist.");

            // Tick 1: parent resolves the config command and spawns 2 child coroutines.
            // The Clock iterates backwards, so newly appended children are not
            // visited until the next tick.
            clock.Run();

            Assert.That(clock.CoroutineCount, Is.EqualTo(2),
                "After first tick, parent completes and 2 child coroutines remain.");

            // Tick 2: both child coroutines execute their 'track' command.
            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "Both parallel groups should execute their 'track' command.");
        }

        /// <summary>
        /// A semicolon sequence where one command is a config command that
        /// expands to parallel groups should still proceed sequentially at
        /// the top level. The parallel groups are spawned when that command
        /// is reached, but subsequent commands in the sequence continue
        /// after the parent coroutine yields past the expansion.
        /// </summary>
        [Test]
        public void Semicolon_Sequence_With_Parallel_Config_Expands_Correctly()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            _mother.ConfigCommands["parallel"] = "{ track; } { track; }";

            // Sequence: track → parallel (expands to 2 groups) → track
            commandBus.RunTerminalCommand("track; parallel; track");

            Assert.That(clock.CoroutineCount, Is.EqualTo(1),
                "Initial routine should be a single coroutine.");

            // Tick 1: first "track" executes
            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "First tick: first track should execute.");

            // Tick 2: "parallel" is resolved — spawns 2 child coroutines,
            // parent continues to final "track" on next yield
            clock.Run();

            // Tick 3: child coroutines execute, final "track" executes
            clock.Run();

            // Tick 4: allow remaining coroutines to finish
            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(4),
                "Total: 1 (first track) + 2 (parallel groups) + 1 (last track) = 4.");
        }

        // =====================================================================
        // Coroutine execution: wait behavior
        // =====================================================================

        /// <summary>
        /// A wait command should block subsequent commands in the same
        /// coroutine. Since deltaTime ≈ 0 in tests, the wait never elapses.
        /// </summary>
        [Test]
        public void Wait_Blocks_Subsequent_Commands_In_Same_Coroutine()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            _mother.ConfigCommands["seq"] = "track; wait 999; track";

            commandBus.RunTerminalCommand("seq");

            clock.Run();
            clock.Run();
            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Only the first command should execute; the wait blocks the second.");
        }

        /// <summary>
        /// A wait in one parallel group must not affect the execution
        /// of commands in a different parallel group.
        /// </summary>
        [Test]
        public void Wait_In_One_Group_Does_Not_Block_Other_Groups()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            commandBus.RunTerminalCommand("{ wait 999; track; } { track; }");

            clock.Run();
            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Only the non-waiting group's command should have executed.");
        }

        /// <summary>
        /// Multiple waits in sequence should accumulate — the total blocked
        /// time is the sum of all wait durations.
        /// </summary>
        [Test]
        public void Sequential_Waits_Accumulate_Blocking_Time()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            _mother.ConfigCommands["seq"] = "track; wait 500; wait 500; track";

            commandBus.RunTerminalCommand("seq");

            for (int i = 0; i < 20; i++)
                clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "Second track should remain blocked behind both waits.");
        }

        // =====================================================================
        // Coroutine execution: per-tick sequencing
        // =====================================================================

        /// <summary>
        /// When commands are inlined sequentially via a config command, each
        /// primitive command executes one per tick (since each yields 0
        /// after executing). Verifies strict per-tick ordering.
        /// </summary>
        [Test]
        public void Inlined_Sequential_Commands_Execute_One_Per_Tick()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            _mother.ConfigCommands["seq"] = "track; track; track";

            commandBus.RunTerminalCommand("seq");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "First tick: only the first command should have executed.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "Second tick: the second command should have executed.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "Third tick: the third command should have executed.");
        }

        /// <summary>
        /// Parallel groups at the top level should all execute on the same
        /// tick, since each group is its own coroutine and the Clock
        /// advances all coroutines within a single Run() call.
        /// </summary>
        [Test]
        public void Parallel_Groups_All_Execute_On_Same_Tick()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();

            commandBus.RunTerminalCommand("{ track; } { track; } { track; }");

            clock.Run();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "All three parallel group coroutines should execute on the first tick.");
        }

        // --- Waypoint routine execution ---

        [Test]
        public void Waypoint_Routine_Is_Stored_Without_Brace_Wrapping()
        {
            CommandBus commandBus = new CommandBus(_mother);
            IWaypoint waypoint = new GPSWaypoint("GPS:TestWP:1:2:3:#FF75C9F1:");

            string routine = "cmd1; cmd2; cmd3";
            commandBus.AddRoutineForWaypoint(waypoint, routine);

            string stored = commandBus.WaypointRoutineQueue.GetRoutineForWaypoint("TestWP");

            Assert.That(stored, Is.EqualTo(routine));
        }

        [Test]
        public void Waypoint_Routine_Executes_Sequentially_Not_As_Parallel_Group()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            Clock clock = _mother.GetModule<Clock>();
            IWaypoint waypoint = new GPSWaypoint("GPS:TestWP:1:2:3:#FF75C9F1:");

            commandBus.AddRoutineForWaypoint(waypoint, "track; track; track");
            commandBus.RunRoutineForWaypoint("TestWP");

            Assert.That(clock.CoroutineCount, Is.EqualTo(1),
                "Waypoint routine should execute as a single sequential coroutine.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(1),
                "First tick: only the first command should have executed.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(2),
                "Second tick: the second command should have executed.");

            clock.Run();
            Assert.That(tracker.ExecutionCount, Is.EqualTo(3),
                "Third tick: the third command should have executed.");
        }

        [Test]
        public void Waypoint_Routine_Is_Removed_After_Execution()
        {
            var tracker = new ExecutionTracker();
            CommandBus commandBus = BootedBusWithTracker(tracker);
            IWaypoint waypoint = new GPSWaypoint("GPS:TestWP:1:2:3:#FF75C9F1:");

            commandBus.AddRoutineForWaypoint(waypoint, "track");
            commandBus.RunRoutineForWaypoint("TestWP");

            Assert.That(commandBus.WaypointRoutineQueue.IsEmpty(), Is.True,
                "Waypoint routine should be removed after execution.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Creates a booted CommandBus with the given tracker registered.
        /// Resets the clock to isolate coroutine counts from boot-time activity.
        /// </summary>
        CommandBus BootedBusWithTracker(ExecutionTracker tracker)
        {
            CommandBus commandBus = new CommandBus(_mother);
            commandBus.Boot();
            commandBus.RegisterCommand(tracker);

            _mother.GetModule<Clock>().Reset();

            return commandBus;
        }

        /// <summary>
        /// A test command that counts how many times Execute() is called.
        /// Used to verify coroutine execution order and count.
        /// </summary>
        class ExecutionTracker : BaseModuleCommand
        {
            /// <summary>
            /// The name of the tracking command.
            /// </summary>
            public override string Name => "track";

            /// <summary>
            /// Counter tracking how many times this command has been executed.
            /// </summary>
            public int ExecutionCount = 0;

            /// <summary>
            /// Executes the tracking command, incrementing the execution counter.
            /// </summary>
            /// <param name="command">The terminal command (ignored).</param>
            /// <returns>Empty string.</returns>
            public override string Execute(TerminalCommand command)
            {
                ExecutionCount++;
                return "";
            }
        }
    }
}
