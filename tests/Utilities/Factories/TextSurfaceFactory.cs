using FakeItEasy;
using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace MotherCore.Tests.Utilities.Factories
{
    /// <summary>
    /// Captures observable state for a fake <see cref="IMyTextSurface"/> used in tests.
    /// </summary>
    public class TextSurfaceDouble
    {
        string _writtenText = string.Empty;
        ContentType _contentType = ContentType.NONE;

        /// <summary>
        /// Gets the fake text surface instance exposed to the code under test.
        /// </summary>
        public IMyTextSurface Surface { get; internal set; }

        /// <summary>
        /// Gets the most recently written text buffer for the fake surface.
        /// </summary>
        public string WrittenText => _writtenText;

        /// <summary>
        /// Gets the currently assigned content type for the fake surface.
        /// </summary>
        public ContentType ContentType => _contentType;

        /// <summary>
        /// Write text to the display surface.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="append"></param>
        internal void Write(string text, bool append)
        {
            text = text ?? string.Empty;
            _writtenText = append ? _writtenText + text : text;
        }

        /// <summary>
        /// Set the content type of the text surface.
        /// </summary>
        /// <param name="contentType"></param>
        internal void SetContentType(ContentType contentType)
        {
            _contentType = contentType;
        }
    }

    /// <summary>
    /// Specialized capture wrapper for fake <see cref="IMyTextPanel"/> instances.
    /// </summary>
    public sealed class TextPanelDouble : TextSurfaceDouble
    {
        /// <summary>
        /// Gets the fake text panel exposed to the code under test.
        /// </summary>
        public IMyTextPanel Panel { get; internal set; }
    }

    /// <summary>
    /// Creates fake text surfaces and panels with lightweight state capture for display tests.
    /// </summary>
    public static class TextSurfaceFactory
    {
        /// <summary>
        /// Creates a fake <see cref="IMyTextSurface"/> and returns its paired capture wrapper.
        /// </summary>
        /// <param name="surfaceSize">Optional logical surface size reported by the fake.</param>
        /// <param name="textureSize">Optional texture size reported by the fake.</param>
        /// <param name="configure">Optional callback for additional fake configuration.</param>
        /// <returns>A capture wrapper that exposes the fake surface and its observed writes.</returns>
        public static TextSurfaceDouble Create(
            Vector2? surfaceSize = null,
            Vector2? textureSize = null,
            Action<IMyTextSurface> configure = null)
        {
            var surface = A.Fake<IMyTextSurface>();
            var capture = new TextSurfaceDouble();

            ConfigureSurface(
                surface,
                capture,
                surfaceSize ?? new Vector2(512f, 512f),
                textureSize ?? surfaceSize ?? new Vector2(512f, 512f));

            configure?.Invoke(surface);
            return capture;
        }

        /// <summary>
        /// Creates a fake <see cref="IMyTextPanel"/> and returns its paired capture wrapper.
        /// </summary>
        /// <param name="customName">Optional block name for the fake panel.</param>
        /// <param name="customData">Optional custom data content for the fake panel.</param>
        /// <param name="surfaceSize">Optional logical surface size reported by the fake.</param>
        /// <param name="textureSize">Optional texture size reported by the fake.</param>
        /// <param name="configure">Optional callback for additional fake configuration.</param>
        /// <returns>A capture wrapper that exposes the fake panel and its observed writes.</returns>
        public static TextPanelDouble CreatePanel(
            string customName = null,
            string customData = "",
            Vector2? surfaceSize = null,
            Vector2? textureSize = null,
            Action<IMyTextPanel> configure = null)
        {
            var panel = TerminalBlockFactory.Create<IMyTextPanel>(
                customName: customName,
                customData: customData);

            var capture = new TextPanelDouble
            {
                Panel = panel,
            };

            ConfigureSurface(
                panel,
                capture,
                surfaceSize ?? new Vector2(512f, 512f),
                textureSize ?? surfaceSize ?? new Vector2(512f, 512f));

            configure?.Invoke(panel);
            return capture;
        }

        /// <summary>
        /// Configure the text surface.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="capture"></param>
        /// <param name="surfaceSize"></param>
        /// <param name="textureSize"></param>
        static void ConfigureSurface(
            IMyTextSurface surface,
            TextSurfaceDouble capture,
            Vector2 surfaceSize,
            Vector2 textureSize)
        {
            capture.Surface = surface;

            A.CallTo(() => surface.SurfaceSize).Returns(surfaceSize);
            A.CallTo(() => surface.TextureSize).Returns(textureSize);

            A.CallTo(() => surface.ContentType)
                .ReturnsLazily(() => capture.ContentType);
            A.CallToSet(() => surface.ContentType)
                .Invokes((ContentType value) => capture.SetContentType(value));

            A.CallTo(() => surface.WriteText(A<string>._, A<bool>._))
                .Invokes((string text, bool append) => capture.Write(text, append))
                .Returns(true);
        }
    }
}