using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Lightweight cockpit fake used by harness tests that need ship-controller behavior
    /// (handbrake, dampeners, occupancy) without the full game runtime.
    /// </summary>
    /// <remarks>
    /// API surface and behavior intent are aligned with the programmable-block references:
    /// https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyCockpit.html
    /// https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyShipController.html
    /// https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider.html
    /// </remarks>
    internal sealed class FakeCockpit : FakeTerminalBlock, IMyCockpit
    {
        readonly List<IMyTextSurface> _surfaces = new List<IMyTextSurface>();

        /// <summary>
        /// Initializes a fake cockpit with optional terminal identity and grid placement.
        /// </summary>
        /// <param name="customName">Optional terminal custom name.</param>
        /// <param name="customData">Optional terminal custom data payload.</param>
        /// <param name="entityId">Optional explicit entity id.</param>
        /// <param name="cubeGrid">Optional owning cube grid.</param>
        public FakeCockpit(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
            _surfaces.Add(new FakeTextSurface());
        }

        /// <summary>
        /// Gets or sets whether this cockpit can currently control the ship.
        /// </summary>
        public bool CanControlShip { get; set; } = true;

        public Vector3D CenterOfMass { get; set; } = Vector3D.Zero;

        public bool ControlThrusters { get; set; } = true;

        public bool ControlWheels { get; set; } = true;

        public bool DampenersOverride { get; set; }

        public bool HandBrake { get; set; }

        public bool HasWheels { get; set; }

        /// <summary>
        /// Gets or sets whether this cockpit is currently flagged as the main cockpit.
        /// </summary>
        public bool IsMainCockpit { get; set; }

        /// <summary>
        /// Gets or sets whether this cockpit is under active control.
        /// Tests can mutate this to trigger occupancy state-monitor transitions.
        /// </summary>
        public bool IsUnderControl { get; set; }

        public Vector3 MoveIndicator { get; set; } = Vector3.Zero;

        public float RollIndicator { get; set; }

        public Vector2 RotationIndicator { get; set; } = Vector2.Zero;

        public bool ShowHorizonIndicator { get; set; }

        public float OxygenCapacity { get; set; } = 1f;

        public float OxygenFilledRatio { get; set; } = 1f;

        /// <summary>
        /// Gets the number of text surfaces exposed by the cockpit.
        /// </summary>
        public int SurfaceCount => _surfaces.Count;

        public bool UseGenericLcd { get; set; } = true;

        /// <summary>
        /// Returns a synthetic ship mass payload.
        /// </summary>
        public MyShipMass CalculateShipMass()
        {
            return default(MyShipMass);
        }

        public Vector3D GetArtificialGravity()
        {
            return Vector3D.Zero;
        }

        public Vector3D GetNaturalGravity()
        {
            return Vector3D.Zero;
        }

        public double GetShipSpeed()
        {
            return 0d;
        }

        public MyShipVelocities GetShipVelocities()
        {
            return default(MyShipVelocities);
        }

        /// <summary>
        /// Returns a cockpit text surface by index.
        /// </summary>
        /// <param name="index">Zero-based surface index.</param>
        /// <returns>The matching surface, or null when out of range.</returns>
        public IMyTextSurface GetSurface(int index)
        {
            if (index < 0 || index >= _surfaces.Count)
                return null;

            return _surfaces[index];
        }

        public Vector3D GetTotalGravity()
        {
            return Vector3D.Zero;
        }

        /// <summary>
        /// Attempts to resolve planet elevation. The fake reports unavailable by default.
        /// </summary>
        public bool TryGetPlanetElevation(MyPlanetElevation detail, out double elevation)
        {
            elevation = 0d;
            return false;
        }

        /// <summary>
        /// Attempts to resolve nearest planet position. The fake reports unavailable by default.
        /// </summary>
        public bool TryGetPlanetPosition(out Vector3D position)
        {
            position = Vector3D.Zero;
            return false;
        }
    }
}