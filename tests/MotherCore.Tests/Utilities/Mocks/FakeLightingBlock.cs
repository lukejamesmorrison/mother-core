using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Concrete harness fake for <see cref="IMyLightingBlock"/> used by extension-script tests.
    /// API surface and member naming follow the PB docs.
    /// Source: https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyLightingBlock.html
    /// </summary>
    internal sealed class FakeLightingBlock : FakeTerminalBlock, IMyLightingBlock
    {
        /// <summary>
        /// Initializes a fake lighting block with optional terminal metadata.
        /// </summary>
        public FakeLightingBlock(
            string customData = "",
            string customName = null,
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName, customData, entityId, cubeGrid)
        {
        }

        /// <summary>Gets or sets the light color.</summary>
        public Color Color { get; set; } = Color.White;

        /// <summary>Gets or sets the base light radius.</summary>
        public float Radius { get; set; }

        /// <summary>
        /// Legacy radius member retained for compatibility with older PB API surface.
        /// </summary>
        public float ReflectorRadius { get; set; }

        /// <summary>Gets or sets the light intensity.</summary>
        public float Intensity { get; set; }

        /// <summary>Gets or sets the light falloff.</summary>
        public float Falloff { get; set; }

        /// <summary>Gets or sets blink interval in seconds.</summary>
        public float BlinkIntervalSeconds { get; set; }

        /// <summary>
        /// Gets or sets the on-time percentage of the blink cycle.
        /// </summary>
        public float BlinkLength { get; set; }

        /// <summary>
        /// Legacy misspelling exposed by the PB API. Mirrors <see cref="BlinkLength"/>.
        /// </summary>
        public float BlinkLenght
        {
            get { return BlinkLength; }
            set { BlinkLength = value; }
        }

        /// <summary>Gets or sets blink cycle offset percentage.</summary>
        public float BlinkOffset { get; set; }

        /// <summary>Gets or sets the light offset.</summary>
        public float Offset { get; set; }

        /// <summary>Gets or sets light rotation speed.</summary>
        public float RotationSpeed { get; set; }
    }
}
