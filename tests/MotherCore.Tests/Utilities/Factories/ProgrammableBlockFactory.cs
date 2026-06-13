using Sandbox.ModAPI.Ingame;
using MotherCore.Tests.Utilities.Mocks;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates <see cref="IMyProgrammableBlock"/> test doubles for use in tests.
    /// </summary>
    internal static class ProgrammableBlockFactory
    {
        /// <summary>
        /// Creates a lightweight <see cref="IMyProgrammableBlock"/> with the specified properties.
        /// </summary>
        /// <param name="customName">The CustomName to return. Defaults to <c>"Mother Core PB"</c>.</param>
        /// <param name="customData">The CustomData string to return. Defaults to empty string.</param>
        /// <param name="entityId">The EntityId to return. Defaults to <c>1000</c>.</param>
        public static IMyProgrammableBlock Create(
            string customName = "Mother Core PB",
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
        {
            var resolvedId = entityId ?? EntityIdFactory.Create();

            return new FakeProgrammableBlock(customName: customName, customData: customData, entityId: resolvedId, cubeGrid: cubeGrid);
        }
    }
}
