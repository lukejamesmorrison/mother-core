using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    internal abstract class FakePowerProducerBlock : FakeTerminalBlock, IMyPowerProducer
    {
        protected FakePowerProducerBlock(
            string customData = "",
            string customName = null,
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

    internal sealed class FakeBatteryBlock : FakePowerProducerBlock, IMyBatteryBlock
    {
        public FakeBatteryBlock(
            string customData = "",
            string customName = null,
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customData, customName, entityId, cubeGrid)
        {
        }

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
            string customData = "",
            string customName = null,
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customData, customName, entityId, cubeGrid)
        {
        }

        public bool UseConveyorSystem { get; set; } = true;
    }
}