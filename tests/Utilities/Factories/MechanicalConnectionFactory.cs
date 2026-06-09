using FakeItEasy;
using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates fake mechanical connection blocks and their corresponding top parts
    /// so the harness can model construct traversal through rotors, hinges, and pistons.
    /// </summary>
    internal static class MechanicalConnectionFactory
    {
        /// <summary>
        /// Shared mutable attachment state used by a fake mechanical base block and
        /// its corresponding top part.
        /// </summary>
        sealed class MechanicalConnectionState
        {
            /// <summary>
            /// Tracks whether the fake connection should report itself as attached.
            /// </summary>
            public bool IsAttached = true;
        }

        static readonly Random Rng = new Random();

        /// <summary>
        /// Creates a fake mechanical connection block that joins the supplied base
        /// grid to the supplied top grid using the requested connection family.
        /// </summary>
        /// <param name="kind">The rotor, hinge, or piston style to synthesize.</param>
        /// <param name="baseGrid">The grid that owns the mechanical base block.</param>
        /// <param name="topGrid">The grid exposed through the mechanical top part.</param>
        /// <param name="customName">
        /// Optional custom name for the fake base block. When omitted, a harness-specific
        /// default name is generated from the connection kind and grid names.
        /// </param>
        /// <returns>
        /// A fake <see cref="IMyMechanicalConnectionBlock"/> whose runtime type matches
        /// the requested connection family.
        /// </returns>
        public static IMyMechanicalConnectionBlock Create(
            MechanicalConnectionKind kind,
            IMyCubeGrid baseGrid,
            IMyCubeGrid topGrid,
            string customName = null)
        {
            switch (kind)
            {
                case MechanicalConnectionKind.Piston:
                    return CreatePistonConnection(baseGrid, topGrid, customName);

                case MechanicalConnectionKind.Hinge:
                    return CreateMotorConnection(baseGrid, topGrid, customName ?? DefaultName("Harness Hinge", baseGrid, topGrid));

                default:
                    return CreateMotorConnection(baseGrid, topGrid, customName ?? DefaultName("Harness Rotor", baseGrid, topGrid));
            }
        }

        /// <summary>
        /// Creates a fake rotor or hinge-style stator on the base grid and a matching
        /// rotor top part on the attached grid.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the fake stator.</param>
        /// <param name="topGrid">The grid that owns the fake rotor top part.</param>
        /// <param name="customName">The custom name assigned to the fake stator.</param>
        /// <returns>A fake <see cref="IMyMotorStator"/> exposed as a mechanical connection block.</returns>
        static IMyMechanicalConnectionBlock CreateMotorConnection(
            IMyCubeGrid baseGrid,
            IMyCubeGrid topGrid,
            string customName)
        {
            var state = new MechanicalConnectionState();
            IMyMotorStator stator = null;

            var rotor = A.Fake<IMyMotorRotor>();
            ConfigureAttachableTop(rotor, topGrid, () => state.IsAttached, () => stator);

            stator = TerminalBlockFactory.Create<IMyMotorStator>(customName: customName, grid: baseGrid);
            ConfigureMechanicalConnection(stator, rotor, topGrid, state);

            return stator;
        }

        /// <summary>
        /// Creates a fake piston base on the base grid and a matching piston top
        /// part on the attached grid.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the fake piston base.</param>
        /// <param name="topGrid">The grid that owns the fake piston top part.</param>
        /// <param name="customName">The custom name assigned to the fake piston base.</param>
        /// <returns>A fake <see cref="IMyPistonBase"/> exposed as a mechanical connection block.</returns>
        static IMyMechanicalConnectionBlock CreatePistonConnection(
            IMyCubeGrid baseGrid,
            IMyCubeGrid topGrid,
            string customName)
        {
            var state = new MechanicalConnectionState();
            IMyPistonBase piston = null;

            var pistonTop = A.Fake<IMyPistonTop>();
            ConfigureAttachableTop(pistonTop, topGrid, () => state.IsAttached, () => piston);

            piston = TerminalBlockFactory.Create<IMyPistonBase>(
                customName: customName ?? DefaultName("Harness Piston", baseGrid, topGrid),
                grid: baseGrid);

            ConfigureMechanicalConnection(piston, pistonTop, topGrid, state);

            return piston;
        }

        /// <summary>
        /// Configures the shared programmable-block-facing members exposed by a fake
        /// mechanical connection block.
        /// </summary>
        /// <typeparam name="TBlock">The concrete fake base block interface being configured.</typeparam>
        /// <param name="block">The fake mechanical base block.</param>
        /// <param name="top">The fake top part attached to the base block.</param>
        /// <param name="topGrid">The grid reported through <see cref="IMyMechanicalConnectionBlock.TopGrid"/>.</param>
        /// <param name="state">The shared mutable attachment state for the connection pair.</param>
        static void ConfigureMechanicalConnection<TBlock>(
            TBlock block,
            IMyAttachableTopBlock top,
            IMyCubeGrid topGrid,
            MechanicalConnectionState state)
            where TBlock : class, IMyMechanicalConnectionBlock
        {
            A.CallTo(() => block.IsAttached).ReturnsLazily(() => state.IsAttached);
            A.CallTo(() => block.PendingAttachment).ReturnsLazily(() => !state.IsAttached);
            A.CallTo(() => block.Top).ReturnsLazily(() => state.IsAttached ? top : null);
            A.CallTo(() => block.TopGrid).ReturnsLazily(() => state.IsAttached ? topGrid : null);

            A.CallTo(() => block.Attach()).Invokes(() => state.IsAttached = true);
            A.CallTo(() => block.Detach()).Invokes(() => state.IsAttached = false);
        }

        /// <summary>
        /// Configures the programmable-block-facing members exposed by a fake attachable
        /// top part such as a rotor head or piston top.
        /// </summary>
        /// <typeparam name="TTop">The concrete top-part interface being configured.</typeparam>
        /// <param name="top">The fake top part instance.</param>
        /// <param name="grid">The grid that owns the fake top part.</param>
        /// <param name="isAttached">Returns the current attachment state shared with the base block.</param>
        /// <param name="baseAccessor">Returns the owning fake mechanical base block.</param>
        static void ConfigureAttachableTop<TTop>(
            TTop top,
            IMyCubeGrid grid,
            Func<bool> isAttached,
            Func<IMyMechanicalConnectionBlock> baseAccessor)
            where TTop : class, IMyAttachableTopBlock
        {
            long entityId = CreateEntityId();

            A.CallTo(() => top.CubeGrid).Returns(grid);
            A.CallTo(() => top.EntityId).Returns(entityId);
            A.CallTo(() => top.IsAttached).ReturnsLazily(() => isAttached());
            A.CallTo(() => top.Base).ReturnsLazily(() => baseAccessor());
        }

        /// <summary>
        /// Builds the default harness block name for a synthetic mechanical connection.
        /// </summary>
        /// <param name="prefix">The connection-family-specific prefix to use.</param>
        /// <param name="baseGrid">The grid that owns the base block.</param>
        /// <param name="topGrid">The grid attached through the top part.</param>
        /// <returns>A readable default custom name for the fake connection block.</returns>
        static string DefaultName(string prefix, IMyCubeGrid baseGrid, IMyCubeGrid topGrid)
        {
            return $"{prefix} {baseGrid.CustomName}->{topGrid.CustomName}";
        }

        /// <summary>
        /// Generates a synthetic entity ID for fake top parts created by this factory.
        /// </summary>
        /// <returns>A pseudo-random positive entity ID.</returns>
        static long CreateEntityId()
        {
            return ((long)Rng.Next(100000, 1000000) * 10000000000L) + Rng.Next(0, 1000000000);
        }
    }
}