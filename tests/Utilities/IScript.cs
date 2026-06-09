using IngameScript;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Common interface shared by all <see cref="Script{TProgram}"/> instances,
    /// regardless of their program type. Used by <see cref="MockIGCNetwork"/> and
    /// <see cref="PrintCapture"/> so they can work with any typed script without
    /// needing to know the concrete <c>TProgram</c>.
    /// </summary>
    public interface IScript
    {
        /// <summary>The <see cref="Mother"/> instance booted by this script.</summary>
        Mother Mother { get; }

        /// <summary>
        /// The <see cref="MockIGC"/> allocated for this script from the network.
        /// <c>null</c> when the script was not joined to a <see cref="MockIGCNetwork"/>.
        /// </summary>
        MockIGC NetworkIGC { get; }

        /// <summary>The IGC of the underlying program, resolved after boot.</summary>
        IMyIntergridCommunicationSystem IGC { get; }
    }
}