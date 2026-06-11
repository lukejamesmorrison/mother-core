using FakeItEasy;
using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using System;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    public sealed class FakeTextSurfaceState
    {
        public string WrittenText { get; set; } = string.Empty;

        public Func<MySpriteDrawFrame> DrawFrameFactory { get; set; }

        public ContentType ContentType { get; set; } = ContentType.NONE;

        public Vector2 SurfaceSize { get; set; } = new Vector2(512f, 512f);

        public Vector2 TextureSize { get; set; } = new Vector2(512f, 512f);
    }

    /// <summary>
    /// Lightweight wrapper around a FakeItEasy-backed <see cref="IMyTextSurface"/>.
    /// Only exposes the state commonly asserted by display tests; rarer behavior can
    /// be configured directly on <see cref="Surface"/> when needed.
    /// </summary>
    public class FakeTextSurface
    {
        public FakeTextSurface()
        {
            Surface = A.Fake<IMyTextSurface>();
            Configure(Surface, State);
        }

        /// <summary>
        /// Compatibility accessor so existing tests can still pass the fake surface explicitly.
        /// </summary>
        public IMyTextSurface Surface { get; }

        /// <summary>
        /// Gets the most recently written text buffer for the fake surface.
        /// </summary>
        public string WrittenText => State.WrittenText;

        /// <summary>
        /// Optional factory used by <see cref="DrawFrame()"/>.
        /// </summary>
        public Func<MySpriteDrawFrame> DrawFrameFactory
        {
            get { return State.DrawFrameFactory; }
            set { State.DrawFrameFactory = value; }
        }

        public FakeTextSurfaceState State { get; } = new FakeTextSurfaceState();

        public ContentType ContentType
        {
            get { return State.ContentType; }
            set { State.ContentType = value; }
        }

        public Vector2 SurfaceSize
        {
            get { return State.SurfaceSize; }
            set { State.SurfaceSize = value; }
        }

        public Vector2 TextureSize
        {
            get { return State.TextureSize; }
            set { State.TextureSize = value; }
        }

        internal static void Configure<TSurface>(TSurface surface, FakeTextSurfaceState state)
            where TSurface : class, IMyTextSurface
        {
            A.CallTo(() => surface.ContentType).ReturnsLazily(() => state.ContentType);
            A.CallToSet(() => surface.ContentType)
                .Invokes((ContentType value) => state.ContentType = value);

            A.CallTo(() => surface.SurfaceSize).ReturnsLazily(() => state.SurfaceSize);

            A.CallTo(() => surface.TextureSize).ReturnsLazily(() => state.TextureSize);

            A.CallTo(() => surface.DrawFrame())
                .ReturnsLazily(() => state.DrawFrameFactory != null
                    ? state.DrawFrameFactory()
                    : new MySpriteDrawFrame(_ => { }));

            A.CallTo(() => surface.WriteText(A<string>._, A<bool>._))
                .ReturnsLazily((string value, bool append) =>
                {
                    value = value ?? string.Empty;
                    state.WrittenText = append ? state.WrittenText + value : value;
                    return true;
                });

            A.CallTo(() => surface.WriteText(A<StringBuilder>._, A<bool>._))
                .ReturnsLazily((StringBuilder value, bool append) =>
                {
                    string text = value == null ? string.Empty : value.ToString();
                    state.WrittenText = append ? state.WrittenText + text : text;
                    return true;
                });

            A.CallTo(() => surface.GetText())
                .ReturnsLazily(() => state.WrittenText);

            A.CallTo(() => surface.MeasureStringInPixels(A<StringBuilder>._, A<string>._, A<float>._))
                .ReturnsLazily((StringBuilder text, string font, float scale) =>
                {
                    int length = text == null ? 0 : text.Length;
                    return new Vector2(length * 10f * scale, 20f * scale);
                });

            A.CallTo(() => surface.ReadText(A<StringBuilder>._, A<bool>._))
                .Invokes((StringBuilder buffer, bool append) =>
                {
                    if (buffer == null)
                        return;

                    if (!append)
                        buffer.Clear();

                    buffer.Append(state.WrittenText);
                });
        }
    }

    /// <summary>
    /// Lightweight wrapper around a FakeItEasy-backed <see cref="IMyTextPanel"/>.
    /// Terminal-block behavior comes from <see cref="TerminalBlockFactory"/> while
    /// display-oriented surface state stays in a small observable state bag.
    /// </summary>
    public class FakeTextPanel
    {
        /// <summary>
        /// Compatibility accessor so existing tests can keep passing a panel explicitly.
        /// </summary>
        public IMyTextPanel Panel { get; }

        public IMyTextSurface Surface => Panel;

        public FakeTextSurfaceState State { get; } = new FakeTextSurfaceState();

        public FakeTextPanel(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
        {
            Panel = TerminalBlockFactory.Create<IMyTextPanel>(
                customName: customName ?? "Text Panel",
                customData: customData,
                entityId: entityId,
                grid: cubeGrid);

            FakeTextSurface.Configure(Panel, State);
        }

        public string WrittenText => State.WrittenText;

        public Func<MySpriteDrawFrame> DrawFrameFactory
        {
            get { return State.DrawFrameFactory; }
            set { State.DrawFrameFactory = value; }
        }

        public Vector2 SurfaceSize
        {
            get { return State.SurfaceSize; }
            set { State.SurfaceSize = value; }
        }

        public Vector2 TextureSize
        {
            get { return State.TextureSize; }
            set { State.TextureSize = value; }
        }

        public ContentType ContentType
        {
            get { return State.ContentType; }
            set { State.ContentType = value; }
        }
    }
}