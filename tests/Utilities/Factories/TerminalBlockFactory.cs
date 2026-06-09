using FakeItEasy;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using System;
using System.Runtime.CompilerServices;
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
        sealed class TerminalBlockState
        {
            public string CustomData;
            public string CustomName;
            public IMyCubeGrid CubeGrid;
            public long EntityId;
            public bool IsFunctional = true;
            public bool IsWorking = true;
            public bool Closed;
            public Func<IMyTerminalBlock, bool> SameConstructEvaluator;
        }

        static readonly ConditionalWeakTable<IMyTerminalBlock, TerminalBlockState> States =
            new ConditionalWeakTable<IMyTerminalBlock, TerminalBlockState>();

        static readonly Random Rng = new Random();

        public static TBlock Create<TBlock>(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid grid = null,
            Action<TBlock> configure = null)
            where TBlock : class, IMyTerminalBlock
        {
            var block = A.Fake<TBlock>();
            var state = new TerminalBlockState
            {
                CustomData = customData ?? string.Empty,
                CustomName = customName ?? typeof(TBlock).Name,
                CubeGrid = grid,
                EntityId = entityId ?? CreateEntityId(),
            };

            States.Add(block, state);
            ConfigureTerminalBlock(block, state);
            configure?.Invoke(block);

            return block;
        }

        internal static IMyCubeGrid CreateCubeGrid(string customName = "Test Grid", long? entityId = null)
        {
            var grid = A.Fake<IMyCubeGrid>();

            A.CallTo(() => grid.CustomName).Returns(customName ?? "Test Grid");
            A.CallTo(() => grid.EntityId).Returns(entityId ?? CreateEntityId());

            return grid;
        }

        internal static bool TryAssignCubeGrid(IMyTerminalBlock block, IMyCubeGrid grid)
        {
            if (block == null || grid == null)
                return false;

            TerminalBlockState state;
            if (States.TryGetValue(block, out state))
            {
                state.CubeGrid = grid;
                return true;
            }

            var programmableBlock = block as FakeProgrammableBlock;
            if (programmableBlock != null)
            {
                programmableBlock.CubeGrid = grid;
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

            TerminalBlockState state;
            if (!States.TryGetValue(block, out state))
                return false;

            state.SameConstructEvaluator = evaluator;
            return true;
        }

        static void ConfigureTerminalBlock<TBlock>(TBlock block, TerminalBlockState state)
            where TBlock : class, IMyTerminalBlock
        {
            A.CallTo(() => block.CustomData).ReturnsLazily(() => state.CustomData);
            A.CallToSet(() => block.CustomData)
                .Invokes((string value) => state.CustomData = value ?? string.Empty);

            A.CallTo(() => block.CustomName).ReturnsLazily(() => state.CustomName);
            A.CallToSet(() => block.CustomName)
                .Invokes((string value) => state.CustomName = value ?? string.Empty);

            A.CallTo(() => block.DisplayNameText).ReturnsLazily(() => state.CustomName);
            A.CallTo(() => block.CubeGrid).ReturnsLazily(() => state.CubeGrid);
            A.CallTo(() => block.EntityId).ReturnsLazily(() => state.EntityId);

            A.CallTo(() => block.IsFunctional).ReturnsLazily(() => state.IsFunctional);
            A.CallTo(() => block.IsWorking).ReturnsLazily(() => state.IsWorking);
            A.CallTo(() => block.Closed).ReturnsLazily(() => state.Closed);

            A.CallTo(() => block.IsSameConstructAs(A<IMyTerminalBlock>._))
                .ReturnsLazily((IMyTerminalBlock other) =>
                {
                    if (state.SameConstructEvaluator != null)
                        return state.SameConstructEvaluator(other);

                    return other != null
                        && state.CubeGrid != null
                        && other.CubeGrid != null
                        && state.CubeGrid.EntityId == other.CubeGrid.EntityId;
                });
        }

        static long CreateEntityId()
        {
            return ((long)Rng.Next(100000, 1000000) * 10000000000L) + Rng.Next(0, 1000000000);
        }
    }
}