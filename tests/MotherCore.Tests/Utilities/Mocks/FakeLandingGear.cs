using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Landing gear fake used by LandingGearModule tests for lock-mode transitions,
    /// auto-lock behavior, and command-path coverage.
    /// </summary>
    /// <remarks>
    /// Citations:
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/SpaceEngineers.Game.ModAPI.Ingame.IMyLandingGear.html"/>
    /// <see href="https://malforge.github.io/spaceengineers/pbapi/SpaceEngineers.Game.ModAPI.Ingame.LandingGearMode.html"/>
    /// </remarks>
    internal sealed class FakeLandingGear : FakeTerminalBlock, IMyLandingGear
    {
        public FakeLandingGear(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        public float BreakForce { get; set; }

        public bool AutoLock { get; set; }

        public bool IsBreakable { get; set; } = true;

        public bool IsParkingEnabled { get; set; } = true;

        public LandingGearMode LockMode { get; set; } = LandingGearMode.Unlocked;

        public bool IsLocked => LockMode == LandingGearMode.Locked;

        public void Lock()
        {
            LockMode = LandingGearMode.Locked;
        }

        public void Unlock()
        {
            LockMode = LandingGearMode.Unlocked;
        }

        public void ToggleLock()
        {
            LockMode = LockMode == LandingGearMode.Locked
                ? LandingGearMode.Unlocked
                : LandingGearMode.Locked;
        }

        public void ResetAutoLock()
        {
            AutoLock = false;
        }
    }
}
