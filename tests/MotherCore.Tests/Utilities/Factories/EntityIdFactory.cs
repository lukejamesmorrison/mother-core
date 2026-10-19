using System.Threading;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Generates monotonically increasing synthetic entity IDs for harness objects.
    /// Using one shared source avoids cross-factory collisions during parallel test execution.
    /// </summary>
    internal static class EntityIdFactory
    {
        /// <summary>
        /// The next entity Id to be created.
        /// </summary>
        static long _nextEntityId = 1000000000000L;

        /// <summary>
        /// Create a new entity Id.
        /// </summary>
        /// <returns></returns>
        public static long Create()
        {
            return Interlocked.Increment(ref _nextEntityId);
        }
    }
}