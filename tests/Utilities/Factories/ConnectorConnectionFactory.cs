using FakeItEasy;
using Sandbox.ModAPI.Ingame;
using System;
using System.Runtime.CompilerServices;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates paired fake ship connectors that can model connector-only links
    /// without treating the participating grids as the same construct.
    /// </summary>
    internal static class ConnectorConnectionFactory
    {
        /// <summary>
        /// Shared mutable state for a paired connector link.
        /// </summary>
        sealed class ConnectorPairState
        {
            /// <summary>
            /// The connector registered on the first grid.
            /// </summary>
            public IMyShipConnector A;

            /// <summary>
            /// The connector registered on the second grid.
            /// </summary>
            public IMyShipConnector B;

            /// <summary>
            /// The current shared status reported by both connectors.
            /// </summary>
            public MyShipConnectorStatus Status;
        }

        /// <summary>
        /// Tracks the shared state associated with each connector fake created by this factory.
        /// </summary>
        static readonly ConditionalWeakTable<IMyShipConnector, ConnectorPairState> States =
            new ConditionalWeakTable<IMyShipConnector, ConnectorPairState>();

        /// <summary>
        /// Creates a paired connector link between two grids.
        /// </summary>
        /// <param name="baseGrid">The grid that owns the primary connector.</param>
        /// <param name="otherGrid">The grid that owns the paired connector.</param>
        /// <param name="otherConnector">Returns the paired connector registered on <paramref name="otherGrid"/>.</param>
        /// <param name="baseCustomName">Optional custom name for the connector on <paramref name="baseGrid"/>.</param>
        /// <param name="otherCustomName">Optional custom name for the connector on <paramref name="otherGrid"/>.</param>
        /// <param name="initialStatus">The initial shared status reported by the connector pair.</param>
        /// <returns>The connector registered on <paramref name="baseGrid"/>.</returns>
        public static IMyShipConnector Create(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            out IMyShipConnector otherConnector,
            string baseCustomName = null,
            string otherCustomName = null,
            MyShipConnectorStatus initialStatus = MyShipConnectorStatus.Connected)
        {
            var baseConnector = TerminalBlockFactory.Create<IMyShipConnector>(
                customName: baseCustomName ?? DefaultName("Test Connector", baseGrid, otherGrid),
                grid: baseGrid);

            otherConnector = TerminalBlockFactory.Create<IMyShipConnector>(
                customName: otherCustomName ?? DefaultName("Test Connector", otherGrid, baseGrid),
                grid: otherGrid);

            var state = new ConnectorPairState
            {
                A = baseConnector,
                B = otherConnector,
                Status = initialStatus,
            };

            States.Add(baseConnector, state);
            States.Add(otherConnector, state);

            ConfigureConnector(baseConnector, () => state.B, state);
            ConfigureConnector(otherConnector, () => state.A, state);

            return baseConnector;
        }

        /// <summary>
        /// Forces an existing connector pair to report a specific shared status.
        /// </summary>
        /// <param name="connector">Any connector instance belonging to the pair.</param>
        /// <param name="status">The shared status to apply to both connectors.</param>
        public static void SetStatus(IMyShipConnector connector, MyShipConnectorStatus status)
        {
            if (connector == null)
                throw new ArgumentNullException(nameof(connector));

            ConnectorPairState state;
            if (!States.TryGetValue(connector, out state))
                throw new InvalidOperationException("The supplied connector is not managed by ConnectorConnectionFactory.");

            state.Status = status;
        }

        /// <summary>
        /// Configures the programmable-block-facing members of one connector in the pair.
        /// </summary>
        /// <param name="connector">The connector instance to configure.</param>
        /// <param name="otherAccessor">Returns the paired connector on the opposite grid.</param>
        /// <param name="state">The shared mutable state backing the connector pair.</param>
        static void ConfigureConnector(
            IMyShipConnector connector,
            Func<IMyShipConnector> otherAccessor,
            ConnectorPairState state)
        {
            A.CallTo(() => connector.Status).ReturnsLazily(() => state.Status);
            A.CallTo(() => connector.IsConnected).ReturnsLazily(() => state.Status == MyShipConnectorStatus.Connected);
            A.CallTo(() => connector.OtherConnector)
                .ReturnsLazily(() => state.Status == MyShipConnectorStatus.Connected ? otherAccessor() : null);

            A.CallTo(() => connector.Connect()).Invokes(() =>
            {
                if (state.Status == MyShipConnectorStatus.Connectable)
                    state.Status = MyShipConnectorStatus.Connected;
            });

            A.CallTo(() => connector.Disconnect()).Invokes(() =>
            {
                state.Status = MyShipConnectorStatus.Unconnected;
            });

            A.CallTo(() => connector.ToggleConnect()).Invokes(() =>
            {
                if (state.Status == MyShipConnectorStatus.Connected)
                    state.Status = MyShipConnectorStatus.Unconnected;
                else if (state.Status == MyShipConnectorStatus.Connectable)
                    state.Status = MyShipConnectorStatus.Connected;
            });
        }

        /// <summary>
        /// Builds a readable default name for a synthetic harness connector.
        /// </summary>
        /// <param name="prefix">The name prefix used to identify the connector family.</param>
        /// <param name="ownGrid">The grid that owns the connector being named.</param>
        /// <param name="otherGrid">The opposite grid in the connector pair.</param>
        /// <returns>A readable default custom name for the connector.</returns>
        static string DefaultName(string prefix, IMyCubeGrid ownGrid, IMyCubeGrid otherGrid)
        {
            return $"{prefix} {ownGrid.CustomName}->{otherGrid.CustomName}";
        }
    }
}