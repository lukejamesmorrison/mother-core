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