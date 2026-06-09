using IngameScript;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// A shared test environment for multi-script tests.
    /// Owns the common IGC transport so that multiple <see cref="Script{TProgram}"/>
    /// instances can interact inside one test.
    /// </summary>
    /// <remarks>
    /// Basic multi-script test:
    /// <code>
    /// var world = new TestWorld();
    ///
    /// var shipA = world.CreateScript&lt;Program&gt;("ShipA").Boot();
    /// var shipB = world.CreateScript&lt;Program&gt;("ShipB").Boot();
    ///
    /// shipA.Bus.RunTerminalCommand("@ShipB help");
    /// world.DispatchIgc();
    /// world.RunIGC();
    /// </code>
    /// For tick-driven behavior:
    /// <code>
    /// world.Run(UpdateType.Terminal, "rename Frigate");
    /// world.RunMany(5, UpdateType.Update10);
    /// </code>
    /// </remarks>
    public class TestWorld
    {
        readonly FakeIgcNetwork _network = new FakeIgcNetwork();

        /// <summary>
        /// Creates a new <see cref="Script{TProgram}"/> joined to this world's IGC
        /// network. Call <see cref="Script{TProgram}.Boot"/> to complete construction.
        /// </summary>
        public Script<TProgram> CreateScript<TProgram>(string scriptName = null)
            where TProgram : MyGridProgram, new()
        {
            return new Script<TProgram>(scriptName).OnNetwork(_network);
        }

        /// <summary>
        /// Routes all pending IGC messages to their recipients and triggers
        /// <c>HandleIncomingIGCMessages</c> on every script with pending input.
        /// Call this between sending a command and running the receiving script.
        /// </summary>
        public TestWorld DispatchIgc()
        {
            _network.Deliver();

            return this;
        }

        /// <summary>
        /// Runs one <c>Mother.Run</c> cycle on every script registered in this world.
        /// Mirrors a single real game update tick across all scripts.
        /// </summary>
        public TestWorld Run(UpdateType updateType, string argument = "")
        {
            foreach (var script in _network.Sessions)
                script.Mother.Run(argument, updateType);

            return this;
        }

        /// <summary>
        /// Convenience helper. Equivalent to <see cref="Run"/> with
        /// <see cref="UpdateType.IGC"/>. Use after <see cref="DispatchIgc"/> to
        /// let each script process its incoming message queue.
        /// </summary>
        public TestWorld RunIGC() => Run(UpdateType.IGC);

        /// <summary>
        /// Advances the world by <paramref name="count"/> consecutive cycles
        /// with the same <paramref name="updateType"/>.
        /// </summary>
        public TestWorld RunMany(int count, UpdateType updateType, string argument = "")
        {
            for (int i = 0; i < count; i++)
                Run(updateType, argument);

            return this;
        }
    }
}
