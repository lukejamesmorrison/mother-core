using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Fake gyro for gyro-module lifecycle tests.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyGyro.html"/>
    /// </remarks>
    internal sealed class FakeGyro : FakeFunctionalBlock, IMyGyro
    {
        public FakeGyro(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool GyroOverride { get; set; }

        public float GyroPower { get; set; }

        public float Pitch { get; set; }

        public float Roll { get; set; }

        public float Yaw { get; set; }
    }
}