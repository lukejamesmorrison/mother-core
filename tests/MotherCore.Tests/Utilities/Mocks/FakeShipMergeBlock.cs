using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Concrete merge-block fake backed by caller-supplied delegates so the harness can
    /// coordinate pair state while tests interact with a real mutable block instance.
    /// </summary>
    internal sealed class FakeShipMergeBlock : FakeTerminalBlock, IMyShipMergeBlock
    {
        Func<bool> _enabledAccessor;
        Action<bool> _enabledSetter;
        Func<MergeState> _stateAccessor;

        public FakeShipMergeBlock(
            string customData = "",
            string customName = null,
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public void Configure(
            Func<bool> enabledAccessor,
            Action<bool> enabledSetter,
            Func<MergeState> stateAccessor)
        {
            if (enabledAccessor == null)
                throw new ArgumentNullException(nameof(enabledAccessor));

            if (enabledSetter == null)
                throw new ArgumentNullException(nameof(enabledSetter));

            if (stateAccessor == null)
                throw new ArgumentNullException(nameof(stateAccessor));

            _enabledAccessor = enabledAccessor;
            _enabledSetter = enabledSetter;
            _stateAccessor = stateAccessor;
        }

        public override bool Enabled
        {
            get { return _enabledAccessor != null ? _enabledAccessor() : base.Enabled; }
            set
            {
                if (_enabledSetter == null)
                {
                    base.Enabled = value;
                    return;
                }

                _enabledSetter(value);
            }
        }

        public bool IsConnected => State == MergeState.Locked;

        public MergeState State => _stateAccessor != null
            ? _stateAccessor()
            : (Enabled ? MergeState.Working : MergeState.None);

        public override void RequestEnable(bool enable)
        {
            Enabled = enable;
        }
    }
}