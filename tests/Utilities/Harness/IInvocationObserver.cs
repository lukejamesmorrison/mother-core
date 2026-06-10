namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Shared abstraction for lightweight test observers that count invocations.
    /// Used by event and command spies so assertion helpers can stay generic.
    /// </summary>
    public interface IInvocationObserver
    {
        /// <summary>
        /// The number of invocations observed so far.
        /// </summary>
        int InvocationCount { get; }
    }
}