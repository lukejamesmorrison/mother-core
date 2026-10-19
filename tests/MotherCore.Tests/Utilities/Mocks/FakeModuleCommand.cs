using IngameScript;
using System.Collections.Generic;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Reusable fake command for command-focused tests and for exercising
    /// protected helper methods on <see cref="BaseModuleCommand"/>.
    /// </summary>
    public class FakeModuleCommand : BaseModuleCommand
    {
        /// <summary>
        /// Backing store for <see cref="Name"/>.
        /// </summary>
        readonly string _name;

        /// <summary>
        /// Command path used by the command bus.
        /// </summary>
        public override string Name => _name;

        /// <summary>
        /// Initializes a new fake command with an optional command name.
        /// </summary>
        /// <param name="name">Command path exposed through <see cref="Name"/>.</param>
        public FakeModuleCommand(string name = "test/cmd")
        {
            _name = name;
        }

        /// <summary>
        /// Default no-op execution for tests that only care about registration
        /// and helper behavior.
        /// </summary>
        /// <param name="command">Parsed terminal command payload.</param>
        /// <returns>Always returns an empty string.</returns>
        public override string Execute(TerminalCommand command) => string.Empty;

        /// <summary>
        /// Test-facing shim for <see cref="BaseModuleCommand.GetIncrementalValue"/>.
        /// </summary>
        /// <param name="valueString">Numerical value from command arguments.</param>
        /// <param name="options">Parsed command options.</param>
        /// <returns>The incremental value derived from arguments and options.</returns>
        public float ReadIncrementalValue(string valueString, Dictionary<string, string> options)
            => GetIncrementalValue(valueString, options);

        /// <summary>
        /// Test-facing shim for <see cref="BaseModuleCommand.GetDistributedValue"/>.
        /// </summary>
        /// <param name="totalValue">The total value before distribution.</param>
        /// <param name="blockCount">Number of addressed blocks.</param>
        /// <param name="isCumulative">Whether command options indicate cumulative mode.</param>
        /// <returns>The value that should be applied per block.</returns>
        public float ReadDistributedValue(float totalValue, int blockCount, bool isCumulative)
            => GetDistributedValue(totalValue, blockCount, isCumulative);

        /// <summary>
        /// Test-facing shim for <see cref="BaseModuleCommand.IsSharedMode"/>.
        /// </summary>
        /// <param name="options">Parsed command options.</param>
        /// <returns><c>true</c> when shared mode is requested; otherwise <c>false</c>.</returns>
        public bool ReadSharedMode(Dictionary<string, string> options)
            => IsSharedMode(options);
    }
}