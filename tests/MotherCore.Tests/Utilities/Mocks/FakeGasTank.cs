using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Fake gas tank for tank-module tests.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyGasTank.html"/>
    /// </remarks>
    internal sealed class FakeGasTank : FakeFunctionalBlock, IMyGasTank
    {
        public FakeGasTank(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool Stockpile { get; set; }

        public double FilledRatio { get; set; }

        public bool AutoRefillBottles { get; set; }

        public float Capacity { get; set; }

        public bool IsResourceSinkByType(MyDefinitionId resourceTypeId)
        {
            return false;
        }

        public void RefillBottles()
        {
        }
    }
}