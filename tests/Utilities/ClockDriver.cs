using IngameScript;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Wraps <see cref="Clock"/> to provide structured, assertion-friendly tick control for
    /// coroutine tests. Eliminates fragile manual <c>clock.Run()</c> call counting and makes
    /// tick boundaries explicit at each assertion point.
    /// </summary>
    public class ClockDriver
    {
        readonly Clock _clock;

        /// <param name="clock">The Clock module instance to drive.</param>
        public ClockDriver(Clock clock)
        {
            _clock = clock;
        }

        /// <summary>The number of coroutines currently active on the underlying clock.</summary>
        public int CoroutineCount => _clock.CoroutineCount;

        /// <summary>
        /// Advances the clock by exactly <paramref name="n"/> ticks and returns
        /// <c>this</c> for chaining.
        /// </summary>
        public ClockDriver Tick(int n = 1)
        {
            for (int i = 0; i < n; i++)
                _clock.Run();

            return this;
        }

        /// <summary>
        /// Advances the clock until no coroutines remain active or
        /// <paramref name="maxTicks"/> is reached, then returns <c>this</c>.
        /// </summary>
        public ClockDriver RunToIdle(int maxTicks = 100)
        {
            int t = 0;

            while (_clock.CoroutineCount > 0 && t++ < maxTicks)
                _clock.Run();

            return this;
        }
    }
}
