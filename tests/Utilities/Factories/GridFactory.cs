using FakeItEasy;
using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates lightweight cube-grid fakes for harness and integration tests.
    /// </summary>
    internal static class GridFactory
    {
        /// <summary>
        /// Shared random number generator for creating unique entity IDs for grids.
        /// </summary>
        static readonly Random Rng = new Random();

        /// <summary>
        /// Creates a lightweight fake grid with the specified custom name and entity ID.
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public static IMyCubeGrid Create(
            string customName = "Test Grid",
            long? entityId = null)
        {
            var grid = A.Fake<IMyCubeGrid>();

            A.CallTo(() => grid.CustomName).Returns(customName ?? "Test Grid");
            A.CallTo(() => grid.EntityId).Returns(entityId ?? CreateEntityId());

            return grid;
        }

        /// <summary>
        /// Generates a unique entity ID for a fake grid. The format is a 6-digit random number followed by a 
        /// 10-digit random number, ensuring a very low probability of collisions across multiple test runs.
        /// </summary>
        /// <returns></returns>
        static long CreateEntityId()
        {
            return ((long)Rng.Next(100000, 1000000) * 10000000000L) + Rng.Next(0, 1000000000);
        }
    }
}