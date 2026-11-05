using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Lightweight jump drive fake with mutable charge and status.
    /// </summary>
    internal sealed class FakeJumpDrive : FakeFunctionalBlock, IMyJumpDrive
    {
        public FakeJumpDrive(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public float CurrentStoredPower { get; set; }

        public float MaxStoredPower { get; set; }

        public MyJumpDriveStatus Status { get; set; }

        public float JumpDistanceMeters { get; set; }

        public float JumpDistanceRatio { get; set; }

        public float MaxJumpDistanceMeters { get; set; }

        public float MinJumpDistanceMeters { get; set; }

        public bool Recharge { get; set; }
    }
}
