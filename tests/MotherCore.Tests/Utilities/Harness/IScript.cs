using IngameScript;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Mother-agnostic script runtime surface used by world and transport helpers.
    /// </summary>
    public interface IRuntimeScript
    {
        /// <summary>
        /// The underlying programmable block program instance.
        /// </summary>
        Sandbox.ModAPI.IMyGridProgram ProgramInstance { get; }

        /// <summary>
        /// Script identity used for addressing and world-level assertions.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The primary grid that owns this script's programmable block.
        /// </summary>
        IMyCubeGrid PrimaryGrid { get; }

        /// <summary>
        /// The IGC of the underlying program, resolved after boot.
        /// </summary>
        IMyIntergridCommunicationSystem IGC { get; }

        /// <summary>
        /// Runs one script update by invoking <c>Program.Main(argument, updateType)</c>
        /// through the harness runtime.
        /// </summary>
        void Run(UpdateType updateType, string argument = "");

        /// <summary>
        /// Advances clock time by one game tick (1/60 second) without invoking
        /// <c>Program.Main</c>. Use <see cref="Run(UpdateType, string)"/> to execute
        /// script logic.
        /// </summary>
        void Tick();
    }

    /// <summary>
    /// Common interface shared by all <see cref="Script{TProgram}"/> instances,
    /// regardless of their program type. Used by <see cref="FakeIgcNetwork"/> and
    /// <see cref="PrintCapture"/> so they can work with any typed script without
    /// needing to know the concrete <c>TProgram</c>.
    /// </summary>
    public interface IScript : IRuntimeScript
    {
        /// <summary>
        /// The optional <see cref="Mother"/> instance booted by this script.
        /// This is null for non-Mother programs.
        /// </summary>
        Mother Mother { get; }

        /// <summary>
        /// Indicates whether this script exposes a <see cref="Mother"/> runtime.
        /// </summary>
        bool HasMother { get; }

        /// <summary>
        /// Clears all recorded EventBus emissions for this script's Mother runtime.
        /// </summary>
        /// <remarks>
        /// Intended for transition-specific assertions where prior emissions should
        /// not influence expected counts.
        /// </remarks>
        void ClearEventEmissions();
    }

    /// <summary>
    /// Optional capability for runtime scripts that can register peer endpoints
    /// into an Almanac-like discovery store.
    /// </summary>
    public interface IAlmanacSyncTarget
    {
        /// <summary>
        /// Registers or updates a peer entry using the supplied runtime name
        /// and unicast endpoint identifier.
        /// </summary>
        void SyncPeerToAlmanac(string peerName, long peerUnicastId);
    }
}