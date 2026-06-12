using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
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

    public class FakeTextSurface : IMyTextSurface
    {
        readonly List<string> _selectedImages = new List<string>();
        readonly List<string> _fonts = new List<string> { "Monospace", "Debug" };
        readonly List<string> _sprites = new List<string> { "SquareSimple", "Circle", "Triangle" };
        readonly List<string> _scripts = new List<string>();

        public FakeTextSurface(FakeTextSurfaceState state = null)
        {
            State = state ?? new FakeTextSurfaceState();
        }

        /// <summary>
        /// Compatibility accessor so existing tests can still pass the fake surface explicitly.
        /// </summary>
        public IMyTextSurface Surface => this;

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

        public FakeTextSurfaceState State { get; }

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

        public string CurrentlyShownImage => _selectedImages.Count == 0 || ContentType == ContentType.TEXT_AND_IMAGE
            ? null
            : _selectedImages[0];

        public float FontSize { get; set; } = 1f;

        public Color FontColor { get; set; } = Color.White;

        public Color BackgroundColor { get; set; } = Color.Black;

        public byte BackgroundAlpha { get; set; } = byte.MaxValue;

        public float ChangeInterval { get; set; }

        public string Font { get; set; } = "Monospace";

        public TextAlignment Alignment { get; set; } = TextAlignment.LEFT;

        public string Script { get; set; } = string.Empty;

        public bool PreserveAspectRatio { get; set; }

        public float TextPadding { get; set; }

        public Color ScriptBackgroundColor { get; set; } = Color.Black;

        public Color ScriptForegroundColor { get; set; } = Color.White;

        public string Name { get; set; } = "Surface";

        public string DisplayName { get; set; } = "Surface";

        public virtual bool WriteText(string value, bool append = false)
        {
            value = value ?? string.Empty;
            State.WrittenText = append ? State.WrittenText + value : value;
            return true;
        }

        public virtual string GetText()
        {
            return State.WrittenText;
        }

        public virtual bool WriteText(StringBuilder value, bool append = false)
        {
            string text = value == null ? string.Empty : value.ToString();
            return WriteText(text, append);
        }

        public virtual void ReadText(StringBuilder buffer, bool append = false)
        {
            if (buffer == null)
                return;

            if (!append)
                buffer.Clear();

            buffer.Append(State.WrittenText);
        }

        public void AddImageToSelection(string id, bool checkExistence = false)
        {
            if (checkExistence && _selectedImages.Contains(id))
                return;

            _selectedImages.Add(id);
        }

        public void AddImagesToSelection(List<string> ids, bool checkExistence = false)
        {
            if (ids == null)
                return;

            ids.ForEach(id => AddImageToSelection(id, checkExistence));
        }

        public void RemoveImageFromSelection(string id, bool removeDuplicates = false)
        {
            if (removeDuplicates)
            {
                _selectedImages.RemoveAll(image => image == id);
                return;
            }

            _selectedImages.Remove(id);
        }

        public void RemoveImagesFromSelection(List<string> ids, bool removeDuplicates = false)
        {
            if (ids == null)
                return;

            ids.ForEach(id => RemoveImageFromSelection(id, removeDuplicates));
        }

        public void ClearImagesFromSelection()
        {
            _selectedImages.Clear();
        }

        public void GetSelectedImages(List<string> output)
        {
            if (output == null)
                return;

            output.AddRange(_selectedImages);
        }

        public void GetFonts(List<string> fonts)
        {
            if (fonts == null)
                return;

            fonts.AddRange(_fonts);
        }

        public void GetSprites(List<string> sprites)
        {
            if (sprites == null)
                return;

            sprites.AddRange(_sprites);
        }

        public void GetScripts(List<string> scripts)
        {
            if (scripts == null)
                return;

            scripts.AddRange(_scripts);
        }

        public virtual MySpriteDrawFrame DrawFrame()
        {
            return State.DrawFrameFactory != null
                ? State.DrawFrameFactory()
                : new MySpriteDrawFrame(_ => { });
        }

        public virtual Vector2 MeasureStringInPixels(StringBuilder text, string font, float scale)
        {
            int length = text == null ? 0 : text.Length;
            return new Vector2(length * 10f * scale, 20f * scale);
        }
    }

    public class FakeTextPanel : FakeTerminalBlock, IMyTextPanel
    {
        string _publicTitle = string.Empty;

        /// <summary>
        /// Compatibility accessor so existing tests can keep passing a panel explicitly.
        /// </summary>
        public IMyTextPanel Panel => this;

        public IMyTextSurface Surface => this;

        public FakeTextSurfaceState State { get; } = new FakeTextSurfaceState();

        readonly FakeTextSurface _surfaceDelegate;

        public FakeTextPanel(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
            : base(customName ?? "Text Panel", customData, entityId, cubeGrid)
        {
            _surfaceDelegate = new FakeTextSurface(State)
            {
                Name = customName ?? "Text Panel",
                DisplayName = customName ?? "Text Panel"
            };
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
            get { return _surfaceDelegate.ContentType; }
            set { _surfaceDelegate.ContentType = value; }
        }

        public string CurrentlyShownImage => _surfaceDelegate.CurrentlyShownImage;

        public float FontSize
        {
            get { return _surfaceDelegate.FontSize; }
            set { _surfaceDelegate.FontSize = value; }
        }

        public Color FontColor
        {
            get { return _surfaceDelegate.FontColor; }
            set { _surfaceDelegate.FontColor = value; }
        }

        public Color BackgroundColor
        {
            get { return _surfaceDelegate.BackgroundColor; }
            set { _surfaceDelegate.BackgroundColor = value; }
        }

        public byte BackgroundAlpha
        {
            get { return _surfaceDelegate.BackgroundAlpha; }
            set { _surfaceDelegate.BackgroundAlpha = value; }
        }

        public float ChangeInterval
        {
            get { return _surfaceDelegate.ChangeInterval; }
            set { _surfaceDelegate.ChangeInterval = value; }
        }

        public string Font
        {
            get { return _surfaceDelegate.Font; }
            set { _surfaceDelegate.Font = value; }
        }

        public TextAlignment Alignment
        {
            get { return _surfaceDelegate.Alignment; }
            set { _surfaceDelegate.Alignment = value; }
        }

        public string Script
        {
            get { return _surfaceDelegate.Script; }
            set { _surfaceDelegate.Script = value; }
        }

        public bool PreserveAspectRatio
        {
            get { return _surfaceDelegate.PreserveAspectRatio; }
            set { _surfaceDelegate.PreserveAspectRatio = value; }
        }

        public float TextPadding
        {
            get { return _surfaceDelegate.TextPadding; }
            set { _surfaceDelegate.TextPadding = value; }
        }

        public Color ScriptBackgroundColor
        {
            get { return _surfaceDelegate.ScriptBackgroundColor; }
            set { _surfaceDelegate.ScriptBackgroundColor = value; }
        }

        public Color ScriptForegroundColor
        {
            get { return _surfaceDelegate.ScriptForegroundColor; }
            set { _surfaceDelegate.ScriptForegroundColor = value; }
        }

        public string Name
        {
            get { return _surfaceDelegate.Name; }
            set { _surfaceDelegate.Name = value; }
        }

        public string DisplayName
        {
            get { return _surfaceDelegate.DisplayName; }
            set { _surfaceDelegate.DisplayName = value; }
        }

        public bool WritePublicTitle(string value, bool append = false)
        {
            value = value ?? string.Empty;
            _publicTitle = append ? _publicTitle + value : value;
            return true;
        }

        public string GetPublicTitle()
        {
            return _publicTitle;
        }

        public bool WriteText(string value, bool append = false)
        {
            return _surfaceDelegate.WriteText(value, append);
        }

        public string GetText()
        {
            return _surfaceDelegate.GetText();
        }

        public bool WriteText(StringBuilder value, bool append = false)
        {
            return _surfaceDelegate.WriteText(value, append);
        }

        public void ReadText(StringBuilder buffer, bool append = false)
        {
            _surfaceDelegate.ReadText(buffer, append);
        }

        public void AddImageToSelection(string id, bool checkExistence = false)
        {
            _surfaceDelegate.AddImageToSelection(id, checkExistence);
        }

        public void AddImagesToSelection(List<string> ids, bool checkExistence = false)
        {
            _surfaceDelegate.AddImagesToSelection(ids, checkExistence);
        }

        public void RemoveImageFromSelection(string id, bool removeDuplicates = false)
        {
            _surfaceDelegate.RemoveImageFromSelection(id, removeDuplicates);
        }

        public void RemoveImagesFromSelection(List<string> ids, bool removeDuplicates = false)
        {
            _surfaceDelegate.RemoveImagesFromSelection(ids, removeDuplicates);
        }

        public void ClearImagesFromSelection()
        {
            _surfaceDelegate.ClearImagesFromSelection();
        }

        public void GetSelectedImages(List<string> output)
        {
            _surfaceDelegate.GetSelectedImages(output);
        }

        public void GetFonts(List<string> fonts)
        {
            _surfaceDelegate.GetFonts(fonts);
        }

        public void GetSprites(List<string> sprites)
        {
            _surfaceDelegate.GetSprites(sprites);
        }

        public void GetScripts(List<string> scripts)
        {
            _surfaceDelegate.GetScripts(scripts);
        }

        public MySpriteDrawFrame DrawFrame()
        {
            return _surfaceDelegate.DrawFrame();
        }

        public Vector2 MeasureStringInPixels(StringBuilder text, string font, float scale)
        {
            return _surfaceDelegate.MeasureStringInPixels(text, font, scale);
        }
    }
}