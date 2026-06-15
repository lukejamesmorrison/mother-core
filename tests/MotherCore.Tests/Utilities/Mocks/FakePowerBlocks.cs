using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    internal abstract class FakePowerProducerBlock : FakeTerminalBlock, IMyPowerProducer
    {
        protected FakePowerProducerBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public float CurrentOutput { get; set; }

        public float MaxOutput { get; set; }

        public float CurrentOutputRatio
        {
            get
            {
                return MaxOutput <= 0f ? 0f : CurrentOutput / MaxOutput;
            }
        }
    }

    /// <summary>
    /// Lightweight battery fake for harness tests that need mutable battery charge state.
    /// </summary>
    /// <remarks>
    /// API surface and behavior intent align with the programmable-block references:
    /// https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyBatteryBlock.html
    /// https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.ChargeMode.html
    /// </remarks>
    internal sealed class FakeBatteryBlock : FakePowerProducerBlock, IMyBatteryBlock
    {
        /// <summary>
        /// Initializes a fake battery with optional terminal identity and grid placement.
        /// </summary>
        /// <param name="customName">Optional terminal custom name.</param>
        /// <param name="customData">Optional terminal custom data payload.</param>
        /// <param name="entityId">Optional explicit entity id.</param>
        /// <param name="cubeGrid">Optional owning cube grid.</param>
        public FakeBatteryBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        /// <summary>
        /// Gets whether additional power can still be stored.
        /// </summary>
        public bool HasCapacityRemaining
        {
            get { return CurrentStoredPower < MaxStoredPower; }
        }

        public float CurrentStoredPower { get; set; }

        public float MaxStoredPower { get; set; }

        public float CurrentInput { get; set; }

        public float MaxInput { get; set; }

        public bool IsCharging
        {
            get { return ChargeMode == ChargeMode.Recharge; }
        }

        /// <summary>
        /// Gets or sets the active battery charge mode.
        /// </summary>
        public ChargeMode ChargeMode { get; set; } = ChargeMode.Auto;

        public bool OnlyRecharge
        {
            get { return ChargeMode == ChargeMode.Recharge; }
            set
            {
                if (value)
                {
                    ChargeMode = ChargeMode.Recharge;
                    return;
                }

                if (ChargeMode == ChargeMode.Recharge)
                    ChargeMode = ChargeMode.Auto;
            }
        }

        public bool OnlyDischarge
        {
            get { return ChargeMode == ChargeMode.Discharge; }
            set
            {
                if (value)
                {
                    ChargeMode = ChargeMode.Discharge;
                    return;
                }

                if (ChargeMode == ChargeMode.Discharge)
                    ChargeMode = ChargeMode.Auto;
            }
        }

        public bool SemiautoEnabled { get; set; }
    }

    internal sealed class FakeReactor : FakePowerProducerBlock, IMyReactor
    {
        public FakeReactor(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool UseConveyorSystem { get; set; } = true;
    }
}