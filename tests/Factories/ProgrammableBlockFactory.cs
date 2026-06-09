using Sandbox.ModAPI.Ingame;
using System;
using MotherCore.Tests.TestUtilities;

namespace MotherCore.Tests
{
    /// <summary>
    /// Creates <see cref="IMyProgrammableBlock"/> test doubles for use in tests.
    /// </summary>
    internal static class ProgrammableBlockFactory
    {
        static readonly Random _rng = new Random();
        /// <summary>
        /// Creates a lightweight <see cref="IMyProgrammableBlock"/> with the specified properties.
        /// </summary>
        /// <param name="customData">The CustomData string to return. Defaults to empty string.</param>
        /// <param name="customName">The CustomName to return. Defaults to <c>"Test PB"</c>.</param>
        /// <param name="entityId">The EntityId to return. Defaults to <c>1000</c>.</param>
        public static IMyProgrammableBlock Create(
            string customData = "",
            string customName = "Mother Core PB",
            long? entityId = null)
        {
            var resolvedId = entityId ?? ((long)_rng.Next(100000, 1000000) * 10000000000L
                + _rng.Next(0, 1000000000));

            return new TestProgrammableBlock(customData, customName, resolvedId);
        }
    }
}
