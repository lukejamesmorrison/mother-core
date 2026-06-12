using IngameScript;
using Sandbox.ModAPI.Ingame;
using VRage.Game;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Common interface shared by all <see cref="Script{TProgram}"/> instances,
    /// regardless of their program type. Used by <see cref="FakeIgcNetwork"/> and
    /// <see cref="PrintCapture"/> so they can work with any typed script without
    /// needing to know the concrete <c>TProgram</c>.
    /// </summary>
    public interface IScript
    {
        /// <summary>
        /// The <see cref="Mother"/> instance booted by this script.
        /// </summary>
        Mother Mother { get; }

        /// <summary>
        /// The IGC of the underlying program, resolved after boot.
        /// In the current harness this is backed by <c>FakeIgc</c>, whether the
        /// script is using a shared <c>FakeIgcNetwork</c> or a standalone endpoint.
        /// </summary>
        IMyIntergridCommunicationSystem IGC { get; }

        /// <summary>
        /// Runs one update cycle against the underlying program.
        /// </summary>
        void Run(UpdateType updateType, string argument = "");

        /// <summary>
        /// Advances the script clock until no coroutines remain active or the
        /// requested maximum cycle count is reached.
        /// </summary>
        void RunToIdle(int maxTicks = 100);

        /// <summary>
        /// Advances the script clock by one cycle.
        /// </summary>
        void Tick();
    }
}