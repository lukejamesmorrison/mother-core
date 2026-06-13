using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    internal sealed class FakeBasicTerminalBlock : FakeTerminalBlock
    {
        public FakeBasicTerminalBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }
    }

    internal sealed class FakeMotorRotor : FakeTerminalBlock, IMyMotorRotor
    {
        Func<bool> _isAttached;
        Func<IMyMechanicalConnectionBlock> _baseAccessor;

        public FakeMotorRotor(
            IMyCubeGrid cubeGrid = null,
            long? entityId = null,
            Func<bool> isAttached = null,
            Func<IMyMechanicalConnectionBlock> baseAccessor = null)
            : base(customName: nameof(FakeMotorRotor), entityId: entityId, cubeGrid: cubeGrid)
        {
            Configure(cubeGrid, EntityId, isAttached, baseAccessor);
        }

        public bool IsAttached => _isAttached();

        public IMyMechanicalConnectionBlock Base => _baseAccessor();

        public void Configure(
            IMyCubeGrid cubeGrid,
            long entityId,
            Func<bool> isAttached,
            Func<IMyMechanicalConnectionBlock> baseAccessor)
        {
            CubeGrid = cubeGrid;
            EntityId = entityId;
            _isAttached = isAttached ?? (() => true);
            _baseAccessor = baseAccessor ?? (() => null);
        }
    }

    internal sealed class FakePistonTop : FakeTerminalBlock, IMyPistonTop
    {
        Func<bool> _isAttached;
        Func<IMyMechanicalConnectionBlock> _baseAccessor;

        public FakePistonTop(
            IMyCubeGrid cubeGrid = null,
            long? entityId = null,
            Func<bool> isAttached = null,
            Func<IMyMechanicalConnectionBlock> baseAccessor = null)
            : base(customName: nameof(FakePistonTop), entityId: entityId, cubeGrid: cubeGrid)
        {
            Configure(cubeGrid, EntityId, isAttached, baseAccessor);
        }

        public bool IsAttached => _isAttached();

        public IMyMechanicalConnectionBlock Base => _baseAccessor();

        public void Configure(
            IMyCubeGrid cubeGrid,
            long entityId,
            Func<bool> isAttached,
            Func<IMyMechanicalConnectionBlock> baseAccessor)
        {
            CubeGrid = cubeGrid;
            EntityId = entityId;
            _isAttached = isAttached ?? (() => true);
            _baseAccessor = baseAccessor ?? (() => null);
        }
    }
}