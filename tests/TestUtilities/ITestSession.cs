using IngameScript;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.TestUtilities
{
    /// <summary>
    /// Common interface shared by all <see cref="TestSession{TProgram}"/> instances,
    /// regardless of their program type. Used by <see cref="MockIGCNetwork"/> and
    /// <see cref="PrintCapture"/> so they can work with any typed session without
    /// needing to know the concrete <c>TProgram</c>.
    /// </summary>
    public interface ITestSession
    {
        /// <summary>The <see cref="Mother"/> instance booted by this session.</summary>
        Mother Mother { get; }

        /// <summary>
        /// The <see cref="MockIGC"/> allocated for this session from the network.
        /// <c>null</c> when the session was not joined to a <see cref="MockIGCNetwork"/>.
        /// </summary>
        MockIGC NetworkIGC { get; }

        /// <summary>The IGC of the underlying program, resolved after boot.</summary>
        IMyIntergridCommunicationSystem IGC { get; }
    }
}
