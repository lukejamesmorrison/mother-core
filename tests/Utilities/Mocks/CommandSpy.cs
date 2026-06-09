using IngameScript;
using System.Collections.Generic;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// A test command that counts how many times Execute() is called and records call order.
    /// Used to verify coroutine execution order and count across test files.
    /// </summary>
    public class CommandSpy : BaseModuleCommand
    {
        static int _globalCallIndex = 0;

        /// <summary>
        /// The name used to match this command when it is dispatched by the <see cref="CommandBus"/>.
        /// Settable so a single test can register multiple trackers under different names.
        /// </summary>
        public string CommandName { get; set; }
        public override string Name => CommandName;

        /// <summary>How many times <see cref="Execute"/> has been called on this instance.</summary>
        public int ExecutionCount { get; private set; }

        /// <summary>
        /// Records the global call index at each invocation, allowing tests to assert
        /// relative ordering across multiple <see cref="CommandSpy"/> instances.
        /// </summary>
        public List<int> ExecutionOrder { get; } = new List<int>();

        /// <summary>
        /// Initializes a new <see cref="CommandSpy"/> with an optional command name.
        /// </summary>
        /// <param name="name">The command name to register under. Defaults to <c>"track"</c>.</param>
        public CommandSpy(string name = "track")
        {
            CommandName = name;
        }

        /// <summary>
        /// Increments <see cref="ExecutionCount"/> and appends the next global call index
        /// to <see cref="ExecutionOrder"/>.
        /// </summary>
        /// <param name="command">The dispatched terminal command (not used by the tracker).</param>
        /// <returns>An empty string.</returns>
        public override string Execute(TerminalCommand command)
        {
            ExecutionCount++;
            ExecutionOrder.Add(++_globalCallIndex);
            return "";
        }

        /// <summary>
        /// Resets the global call index counter. Call this in test setup to ensure
        /// ExecutionOrder values are deterministic across test runs.
        /// </summary>
        public static void ResetGlobalIndex()
        {
            _globalCallIndex = 0;
        }
    }
}
