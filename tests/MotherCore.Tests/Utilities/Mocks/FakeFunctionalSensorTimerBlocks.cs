using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using System.Collections.Generic;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Lightweight functional block fake used by module command-path tests that
    /// only require <see cref="IMyFunctionalBlock.Enabled"/> semantics.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyFunctionalBlock.html"/>
    /// </remarks>
    internal class FakeFunctionalBlock : FakeTerminalBlock, IMyFunctionalBlock
    {
        public FakeFunctionalBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }
    }

    /// <summary>
    /// Sensor block fake with mutable detection state used by SensorModule
    /// transition and hook tests.
    /// </summary>
    /// <remarks>
    /// Citations:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMySensorBlock.html"/>
    /// </remarks>
    internal sealed class FakeSensorBlock : FakeFunctionalBlock, IMySensorBlock
    {
        public FakeSensorBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool IsActive { get; set; }

    /// <summary>
    /// Timer block fake used by TimerModule tests to track trigger/start/stop
    /// invocations and countdown state changes.
    /// </summary>
    /// <remarks>
    /// Citation:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/SpaceEngineers.Game.ModAPI.Ingame.IMyTimerBlock.html"/>
    /// </remarks>
        public MyDetectedEntityInfo LastDetectedEntity { get; set; }

        public bool DetectAsteroids { get; set; }

        public bool DetectFloatingObjects { get; set; }

        public bool DetectLargeShips { get; set; }

        public bool DetectSmallShips { get; set; }

        public bool DetectOwner { get; set; }

        public bool DetectFriendly { get; set; }

        public bool DetectNeutral { get; set; }

        public bool DetectEnemy { get; set; }

        public bool DetectPlayers { get; set; }

        public bool DetectSubgrids { get; set; }

        public bool DetectStations { get; set; }

        public bool PlayProximitySound { get; set; }

        public float MaxRange { get; set; }

        public bool IsSensing { get; set; }

        public float LeftExtend { get; set; }

        public float RightExtend { get; set; }

        public float TopExtend { get; set; }

        public float BottomExtend { get; set; }

        public float FrontExtend { get; set; }

        public float BackExtend { get; set; }

        public void DetectedEntities(List<MyDetectedEntityInfo> entities)
        {
            if (entities == null)
                return;

            entities.Add(LastDetectedEntity);
        }

        public void MoveFront(float value)
        {
            FrontExtend = value;
        }

        public void MoveBack(float value)
        {
            BackExtend = value;
        }

        public void MoveLeft(float value)
        {
            LeftExtend = value;
        }

        public void MoveRight(float value)
        {
            RightExtend = value;
        }

        public void MoveTop(float value)
        {
            TopExtend = value;
        }

        public void MoveBottom(float value)
        {
            BottomExtend = value;
        }
    }

    internal sealed class FakeTimerBlock : FakeFunctionalBlock, IMyTimerBlock
    {
        public FakeTimerBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public bool IsCountingDown { get; set; }

        public bool Silent { get; set; }

        public float TriggerDelay { get; set; }

        public int TriggerCount { get; private set; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public void Trigger()
        {
            TriggerCount++;
        }

        public void StartCountdown()
        {
            StartCount++;
            IsCountingDown = true;
        }

        public void StopCountdown()
        {
            StopCount++;
            IsCountingDown = false;
        }
    }
}
