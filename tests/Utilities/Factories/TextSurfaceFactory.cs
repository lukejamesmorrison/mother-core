using Sandbox.ModAPI.Ingame;
using System;
using MotherCore.Tests.Utilities.Mocks;
using VRageMath;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Creates fake text surfaces and panels with lightweight state capture for display tests.
    /// </summary>
    public static class TextSurfaceFactory
    {
        /// <summary>
        /// Creates a fake <see cref="IMyTextSurface"/> and returns the concrete fake instance.
        /// </summary>
        /// <param name="surfaceSize">Optional logical surface size reported by the fake.</param>
        /// <param name="textureSize">Optional texture size reported by the fake.</param>
        /// <param name="configure">Optional callback for additional fake configuration.</param>
        /// <returns>A concrete fake surface with observable state for test assertions.</returns>
        public static FakeTextSurface Create(
            Vector2? surfaceSize = null,
            Vector2? textureSize = null,
            Action<FakeTextSurface> configure = null)
        {
            var surface = new FakeTextSurface
            {
                SurfaceSize = surfaceSize ?? new Vector2(512f, 512f),
                TextureSize = textureSize ?? surfaceSize ?? new Vector2(512f, 512f),
            };

            configure?.Invoke(surface);
            return surface;
        }

        /// <summary>
        /// Creates a fake <see cref="IMyTextPanel"/> and returns the concrete fake instance.
        /// </summary>
        /// <param name="customName">Optional block name for the fake panel.</param>
        /// <param name="customData">Optional custom data content for the fake panel.</param>
        /// <param name="surfaceSize">Optional logical surface size reported by the fake.</param>
        /// <param name="textureSize">Optional texture size reported by the fake.</param>
        /// <param name="configure">Optional callback for additional fake configuration.</param>
        /// <returns>A concrete fake text panel with observable state for test assertions.</returns>
        public static FakeTextPanel CreatePanel(
            string customName = null,
            string customData = "",
            Vector2? surfaceSize = null,
            Vector2? textureSize = null,
            Action<FakeTextPanel> configure = null)
        {
            var panel = new FakeTextPanel(
                customName: customName,
                customData: customData);

            panel.SurfaceSize = surfaceSize ?? new Vector2(512f, 512f);
            panel.TextureSize = textureSize ?? surfaceSize ?? new Vector2(512f, 512f);

            configure?.Invoke(panel);
            return panel;
        }
    }
}
