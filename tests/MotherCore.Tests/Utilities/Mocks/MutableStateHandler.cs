using IngameScript;
using Sandbox.ModAPI.Ingame;
using System;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Test double for block state monitoring that reads state from a caller-provided accessor.
    /// </summary>
    internal sealed class MutableStateHandler : IBlockStateHandler
    {
        /// <summary>
        /// Returns the current synthetic state for the monitored block.
        /// </summary>
        readonly Func<object> _getState;

        /// <summary>
        /// Initializes a new state handler backed by a caller-controlled state accessor.
        /// </summary>
        /// <param name="getState">
        /// Delegate that returns the current state snapshot to compare against the previously stored value.
        /// </param>
        public MutableStateHandler(Func<object> getState)
        {
            _getState = getState;
        }

        /// <summary>
        /// Gets the number of times <see cref="OnBlockStateChanged"/> has been invoked.
        /// </summary>
        public int ChangeCount { get; private set; }

        /// <summary>
        /// Returns the current synthetic block state.
        /// </summary>
        /// <param name="block">The monitored block. The test double does not inspect this value directly.</param>
        /// <returns>The current state snapshot provided by <see cref="_getState"/>.</returns>
        public object GetBlockCurrentState(IMyTerminalBlock block)
        {
            return _getState();
        }

        /// <summary>
        /// Compares the stored previous state with the current synthetic state.
        /// </summary>
        /// <param name="block">The monitored block. The test double does not inspect this value directly.</param>
        /// <param name="previousState">The previously recorded block state.</param>
        /// <returns><see langword="true"/> when the current state differs from <paramref name="previousState"/>; otherwise <see langword="false"/>.</returns>
        public bool HasBlockStateChanged(IMyTerminalBlock block, object previousState)
        {
            return !Equals(previousState, _getState());
        }

        /// <summary>
        /// Records that the monitored block reported a state transition.
        /// </summary>
        /// <param name="block">The block whose state changed.</param>
        public void OnBlockStateChanged(IMyTerminalBlock block)
        {
            ChangeCount++;
        }
    }
}