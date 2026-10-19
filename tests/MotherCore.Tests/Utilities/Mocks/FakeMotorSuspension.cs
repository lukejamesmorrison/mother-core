using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Fake wheel suspension block for wheel-module tests.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyMotorSuspension.html"/>
    /// </remarks>
    internal sealed class FakeMotorSuspension : FakeMechanicalConnectionBlock, IMyMotorSuspension
    {
        public FakeMotorSuspension(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool Brake { get; set; }

        public bool Steering { get; set; }

        public bool Propulsion { get; set; }

        public bool InvertSteer { get; set; }

        public bool InvertPropulsion { get; set; }

        public float Damping { get; set; }

        public float SteerAngle { get; set; }

        public float SteerSpeed { get; set; }

        public float SteerReturnSpeed { get; set; }

        public float SuspensionTravel { get; set; }

        public bool AirShockEnabled { get; set; }

        public float SteeringOverride { get; set; }

        public float Friction { get; set; }

        public float Height { get; set; }

        public float MaxSteerAngle { get; set; }

        public float PropulsionOverride { get; set; }

        public float Power { get; set; }

        public float Strength { get; set; }

        public bool IsParkingEnabled { get; set; }
    }
}