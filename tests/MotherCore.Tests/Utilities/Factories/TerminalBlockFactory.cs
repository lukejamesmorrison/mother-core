using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates lightweight terminal block fakes that implement any
    /// <see cref="IMyTerminalBlock"/>-derived interface without needing a
    /// dedicated concrete fake type for each block family.
    ///
    /// To add support for a new terminal block family:
    /// 1) Add a concrete fake under Utilities.Mocks that derives from <see cref="FakeTerminalBlock"/>.
    /// 2) Implement the target in-game interface (for example, <c>IMyTimerBlock</c>).
    /// 3) Add a mapping in <see cref="CreateConcreteBlock{TBlock}(string, string, long?, IMyCubeGrid)"/>
    ///    from the interface type to the new fake constructor.
    /// </summary>
    public static class TerminalBlockFactory
    {
        public static TBlock Create<TBlock>(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid grid = null,
            Action<TBlock> configure = null)
            where TBlock : class, IMyTerminalBlock
        {
            var concreteBlock = CreateConcreteBlock<TBlock>(customName, customData, entityId, grid);
            if (concreteBlock != null)
            {
                configure?.Invoke(concreteBlock);
                return concreteBlock;
            }

            throw new NotSupportedException(
                $"TerminalBlockFactory does not have a concrete fake for '{typeof(TBlock).FullName}'. "
                + "Add an explicit harness fake instead of relying on a generic interface proxy.");
        }

        static TBlock CreateConcreteBlock<TBlock>(
            string customName,
            string customData,
            long? entityId,
            IMyCubeGrid grid)
            where TBlock : class, IMyTerminalBlock
        {
            if (typeof(TBlock) == typeof(IMyDoor))
                return new FakeDoor(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            if (typeof(TBlock) == typeof(IMyTerminalBlock))
                return new FakeBasicTerminalBlock(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            if (typeof(TBlock) == typeof(IMyBatteryBlock))
                return new FakeBatteryBlock(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            if (typeof(TBlock) == typeof(IMyReactor))
                return new FakeReactor(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            if (typeof(TBlock) == typeof(IMyShipConnector))
                return new FakeShipConnector(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            if (typeof(TBlock) == typeof(IMyShipMergeBlock))
                return new FakeShipMergeBlock(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            if (typeof(TBlock) == typeof(IMyLightingBlock))
                return new FakeLightingBlock(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            if (typeof(TBlock) == typeof(IMyAirVent))
                return new FakeAirVent(customName: customName, customData: customData, entityId: entityId, cubeGrid: grid) as TBlock;

            return null;
        }

        internal static bool TryAssignCubeGrid(IMyTerminalBlock block, IMyCubeGrid grid)
        {
            if (block == null || grid == null)
                return false;

            var fakeBlock = block as FakeTerminalBlock;
            if (fakeBlock != null)
            {
                fakeBlock.CubeGrid = grid;
                return true;
            }

            return Equals(block.CubeGrid, grid);
        }

        internal static bool TryAssignSameConstructEvaluator(
            IMyTerminalBlock block,
            Func<IMyTerminalBlock, bool> evaluator)
        {
            if (block == null || evaluator == null)
                return false;

            var fakeBlock = block as FakeTerminalBlock;
            if (fakeBlock != null)
            {
                fakeBlock.SameConstructEvaluator = evaluator;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Create a unique Entity Id.
        /// </summary>
        /// <returns></returns>
        static long CreateEntityId() => EntityIdFactory.Create();
    }
}