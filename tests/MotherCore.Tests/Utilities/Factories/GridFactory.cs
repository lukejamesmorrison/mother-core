using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates lightweight cube-grid fakes for harness and integration tests.
    /// </summary>
    internal static class GridFactory
    {
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
            return new FakeCubeGrid(customName ?? "Test Grid", entityId ?? CreateEntityId());
        }

        /// <summary>
        /// Generates a unique entity ID for a fake grid. The format is a 6-digit random number followed by a 
        /// 10-digit random number, ensuring a very low probability of collisions across multiple test runs.
        /// </summary>
        /// <returns></returns>
        static long CreateEntityId() => EntityIdFactory.Create();
    }
}