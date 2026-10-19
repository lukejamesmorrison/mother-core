using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Fake thruster for thrust-module tests.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyThrust.html"/>
    /// </remarks>
    internal sealed class FakeThrust : FakeFunctionalBlock, IMyThrust
    {
        public FakeThrust(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public float CurrentThrust { get; set; }

        public float CurrentThrustPercentage { get; set; }

        public float MaxThrust { get; set; }

        public float MaxEffectiveThrust { get; set; }

        public float ThrustOverride { get; set; }

        public float ThrustOverridePercentage { get; set; }

        public float PowerConsumptionMultiplier { get; set; }

        public float ThrustMultiplier { get; set; }

        public Vector3I GridThrustDirection { get; set; }
    }
}