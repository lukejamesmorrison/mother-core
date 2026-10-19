using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Concrete harness fake for <see cref="IMySearchlight"/> used by extension-script tests.
    /// API surface and member naming follow the PB docs.
    /// Source: https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMySearchlight.html
    /// </summary>
    /// <remarks>
    /// Inherits terminal/functional behavior from <see cref="FakeFunctionalBlock"/> and
    /// keeps searchlight-specific state mutable for module command-path assertions.
    /// </remarks>
    internal sealed class FakeSearchlight : FakeFunctionalBlock, IMySearchlight
    {
        /// <summary>
        /// Initializes a fake searchlight with optional terminal metadata.
        /// </summary>
        public FakeSearchlight(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        /// <summary>Gets or sets aiming radius.</summary>
        public float AimingRadius { get; set; }

        /// <summary>Gets or sets blink interval in seconds.</summary>
        public float BlinkInterval { get; set; }

        /// <summary>Gets or sets blink on-length percentage.</summary>
        public float BlinkLength { get; set; }

        /// <summary>Gets or sets blink cycle offset percentage.</summary>
        public float BlinkOffset { get; set; }

        /// <summary>Gets or sets searchlight color.</summary>
        public Color Color { get; set; } = Color.White;

        /// <summary>Gets or sets idle movement behavior.</summary>
        public bool EnableIdleMovement { get; set; }

        /// <summary>Gets or sets light intensity.</summary>
        public float Intensity { get; set; }

        /// <summary>Gets or sets light offset.</summary>
        public float Offset { get; set; }

        /// <summary>Gets or sets light radius.</summary>
        public float Radius { get; set; }

        /// <summary>Gets or sets whether characters are targeted.</summary>
        public bool TargetCharacters { get; set; }

        /// <summary>Gets or sets whether enemy entities are targeted.</summary>
        public bool TargetEnemy { get; set; }

        /// <summary>Gets or sets whether friendly entities are targeted.</summary>
        public bool TargetFriends { get; set; }

        /// <summary>Gets or sets whether large ships are targeted.</summary>
        public bool TargetLargeShips { get; set; }

        /// <summary>Gets or sets whether target lock for enemies is enabled.</summary>
        public bool TargetLockEnemy { get; set; }

        /// <summary>Gets or sets whether meteors are targeted.</summary>
        public bool TargetMeteors { get; set; }

        /// <summary>Gets or sets whether neutral entities are targeted.</summary>
        public bool TargetNeutrals { get; set; }

        /// <summary>Gets or sets the aggregate targeting options bitmask.</summary>
        public TargetingGroupOptions TargetOptions { get; set; }

        /// <summary>Gets or sets whether rockets are targeted.</summary>
        public bool TargetRockets { get; set; }

        /// <summary>Gets or sets whether small ships are targeted.</summary>
        public bool TargetSmallShips { get; set; }

        /// <summary>Gets or sets whether stations are targeted.</summary>
        public bool TargetStations { get; set; }

        /// <summary>
        /// Gets the last azimuth value passed to <see cref="SetManualAzimuthAndElevation"/>.
        /// </summary>
        public float LastManualAzimuth { get; private set; }

        /// <summary>
        /// Gets the last elevation value passed to <see cref="SetManualAzimuthAndElevation"/>.
        /// </summary>
        public float LastManualElevation { get; private set; }

        /// <summary>
        /// Captures the most recent manual azimuth/elevation values.
        /// </summary>
        public void SetManualAzimuthAndElevation(float azimuth, float elevation)
        {
            LastManualAzimuth = azimuth;
            LastManualElevation = elevation;
        }

        /// <summary>
        /// Assigns target options using the PB API contract.
        /// </summary>
        public void SetTargetOptions(TargetingGroupOptions targetOptions)
        {
            TargetOptions = targetOptions;
        }
    }
}
