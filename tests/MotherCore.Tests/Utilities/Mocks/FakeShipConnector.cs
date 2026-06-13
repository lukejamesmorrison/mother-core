using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Concrete connector fake backed by caller-supplied delegates so paired connector
    /// state can live in the harness while tests still interact with a real mutable block object.
    /// </summary>
    internal sealed class FakeShipConnector : FakeTerminalBlock, IMyShipConnector
    {
        Func<MyShipConnectorStatus> _statusAccessor;
        Func<IMyShipConnector> _otherAccessor;
        Action _connect;
        Action _disconnect;
        Action _toggle;

        public FakeShipConnector(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public void Configure(
            Func<MyShipConnectorStatus> statusAccessor,
            Func<IMyShipConnector> otherAccessor,
            Action connect,
            Action disconnect,
            Action toggle)
        {
            if (statusAccessor == null)
                throw new ArgumentNullException(nameof(statusAccessor));

            if (connect == null)
                throw new ArgumentNullException(nameof(connect));

            if (disconnect == null)
                throw new ArgumentNullException(nameof(disconnect));

            if (toggle == null)
                throw new ArgumentNullException(nameof(toggle));

            _statusAccessor = statusAccessor;
            _otherAccessor = otherAccessor;
            _connect = connect;
            _disconnect = disconnect;
            _toggle = toggle;
        }

        public MyShipConnectorStatus Status => _statusAccessor != null
            ? _statusAccessor()
            : MyShipConnectorStatus.Unconnected;

        public bool IsConnected => Status == MyShipConnectorStatus.Connected;

        public bool IsLocked => IsConnected;

        public bool IsParkingEnabled { get; set; } = true;

        public float PullStrength { get; set; } = 1f;

        public bool ThrowOut { get; set; }

        public bool CollectAll { get; set; }

        public IMyShipConnector OtherConnector => Status == MyShipConnectorStatus.Connected
            ? _otherAccessor?.Invoke()
            : null;

        public void Connect()
        {
            _connect?.Invoke();
        }

        public void Disconnect()
        {
            _disconnect?.Invoke();
        }

        public void ToggleConnect()
        {
            _toggle?.Invoke();
        }

    }
}