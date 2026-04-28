using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Policy;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
using VRageRender;


namespace IngameScript
{
    /// <summary>
    /// The Display class is a wrapper for the IMyTextSurface interface. 
    /// It handles the drawing of sprites and text on the display.
    /// </summary>
    public class Display
    {
        /// <summary>
        /// The IMyTextSurface related to the display.
        /// </summary>
        public IMyTextSurface Surface;

        /// <summary>
        /// The parent block of the surface/display.
        /// </summary>
        public readonly IMyTerminalBlock Block;

        /// <summary>
        /// The viewport of the display.
        /// </summary>
        public RectangleF Viewport;

        /// <summary>
        /// The padding around the display.
        /// </summary>
        protected float LCDDisplayPadding = 4f;

        /// <summary>
        /// The padding around the display.
        /// </summary>
        protected float CockpitDisplayPadding = 0;

        /// <summary>
        /// The default font size for the display.
        /// </summary>
        protected const float BASE_FONT_SIZE = 16f;

        /// <summary>
        /// The default scale for the display.
        /// </summary>
        protected const float DEFAULT_SCALE = 1f;

        /// <summary>
        /// The scale of the display.
        /// </summary>
        public float Scale = DEFAULT_SCALE;

        /// <summary>
        /// The default text scale for the display.
        /// </summary>
        protected const float DEFAULT_TEXT_SCALE = 1f;

        /// <summary>
        /// The font size of the display.
        /// </summary>
        public float FontSize;

        /// <summary>
        /// The frame for drawing sprites.
        /// </summary>
        public MySpriteDrawFrame Frame;

        /// <summary>
        /// Is the display a cockpit display?    
        /// </summary>
        public bool IsCockpitDisplay;

        /// <summary>
        /// The configuration for the block related to the display. For cockpit 
        /// displays, this is the configuration for the cockpit block.
        /// </summary>
        public MyIni Configuration;

        /// <summary>
        /// Vectors representing the corners and center of the viewport.
        /// </summary>
        public Vector2 TopLeft, TopRight, BottomLeft, BottomRight, ViewportCenter;

        /// <summary>
        /// Constructor. We initialize a display with a surface, the parent block, and the block's configuration. 
        /// We account for differences in regular LCD displays vs. cockpit displays and set 
        /// details related to render like scale and viewport size.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="block"></param>
        /// <param name="blockConfiguration"></param>
        /// <param name="isCockpitDisplay"></param>
        public Display(
            IMyTextSurface surface, 
            IMyTerminalBlock block, 
            MyIni blockConfiguration, 
            bool isCockpitDisplay = false
        )
        {
            Surface = surface;
            Block = block;
            Configuration = blockConfiguration;
            IsCockpitDisplay = isCockpitDisplay;

            SetBaseConfiguration();

            SetRenderingDetails();
        }

        /// <summary>
        /// Update the configuration for the display.
        /// </summary>
        /// <param name="config"></param>
        public virtual void UpdateConfig(MyIni config)
        {
            Configuration = config;

            //SetRenderingDetails();
        }

        /// <summary>
        /// Set the rendering details for the display. This includes calculating the 
        /// viewport size, scale and font size.
        /// </summary>
        protected virtual void SetRenderingDetails()
        {
            Viewport = CalculateViewport(Surface, LCDDisplayPadding);

            float widthOffset = IsCockpitDisplay ? CockpitDisplayPadding : LCDDisplayPadding;
            float heightOffset = IsCockpitDisplay ? CockpitDisplayPadding : LCDDisplayPadding;

            // Adjusting for dynamic padding and scaling
            TopLeft = Viewport.Position + new Vector2(widthOffset, heightOffset);
            TopRight = new Vector2(Viewport.X + Viewport.Width - widthOffset, Viewport.Y + heightOffset);
            BottomLeft = new Vector2(Viewport.X + widthOffset, Viewport.Y + Viewport.Height - heightOffset);
            BottomRight = new Vector2(Viewport.X + Viewport.Width - widthOffset, Viewport.Y + Viewport.Height - heightOffset);
            ViewportCenter = new Vector2(
                Viewport.X + (Viewport.Width / 2f),
                Viewport.Y + (Viewport.Height / 2f)
            );

            FontSize = CalculateFontSize();
        }

        /// <summary>
        /// Set the configuration for the display. This uses the configuration 
        /// defined in the block's custom data.
        /// </summary>
        protected virtual void SetBaseConfiguration()
        {
            //if (Configuration.ContainsSection("general"))
            //{
            //    //DEFAULT_SCALE = float.Parse(Configuration.Get("general", "scale").ToString());
            //    //DEFAULT_TEXT_SCALE = float.Parse(Configuration.Get("general", "textScale").ToString());  // Load text scale
            //}
        }

        /// <summary>
        /// Draw the frame for the display. This is where we can draw sprites and text.
        /// </summary>
        public virtual void DrawFrame() => Frame = Surface.DrawFrame();

        /// <summary>
        /// Calculate the viewport size and position based on SurfaceSize (not 
        /// TextureSize) and padding.
        /// </summary>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Text-Panels-and-Drawing-Sprites"/>
        /// <param name="surface"></param>
        /// <param name="padding"></param>
        /// <returns></returns>
        protected RectangleF CalculateViewport(IMyTextSurface surface, float padding)
        {
            var surfaceSize = surface.SurfaceSize;

            return new RectangleF(
                surface.TextureSize.X / 2f - surfaceSize.X / 2f + padding, // X 
                surface.TextureSize.Y / 2f - surfaceSize.Y / 2f + padding, // Y 
                //surfaceSize.X - 2 * padding,  // Width 
                //surfaceSize.Y - 2 * padding   // Height
                surfaceSize.X - (4 * padding),  // Width 
                surfaceSize.Y - (4 * padding)   // Height
            );
        }

        /// <summary>
        /// Set the scale of the display.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public void SetScale(Vector3D min, Vector3D max)
        {
            var range = max - min;
            var scaleX = Viewport.Width / range.X;
            var scaleY = Viewport.Height / range.Y;

            Scale = DEFAULT_SCALE * (float) Math.Min(scaleX, scaleY);
        }

        /// <summary>
        /// Calculate the font size based on the display size and the default text 
        /// scale. We apply a scaling factor and set max and min limits to 
        /// bound font size based.
        /// </summary>
        /// <returns></returns>
        public virtual float CalculateFontSize()
        {
            // 1024px is a reference base for normal scaling
            float displayFactor = Viewport.Width / 1024f; 

            // Adjust font size based on display width and DEFAULT_TEXT_SCALE (for fine-tuning)
            float fontSize = BASE_FONT_SIZE * displayFactor * DEFAULT_TEXT_SCALE;

            // Bound font size
            fontSize = Math.Max(fontSize, 8f);
            fontSize = Math.Min(fontSize, 32f);

            return fontSize;
        }

        /// <summary>
        /// Get the scaling factor for the display. This is used when determining 
        /// the render size of text and sprites. We Bound it between 0 and 1.
        /// </summary>
        /// <returns></returns>
        public float GetScalingFactor() => Math.Min(1f, FontSize / BASE_FONT_SIZE);

        /// <summary>
        /// Returns true when the display aspect ratio exceeds 16:9 (wider than ~1.8).
        /// Used to enable split-screen rendering.
        /// </summary>
        public bool IsWidescreen => Viewport.Width / Viewport.Height > 1.8f;

        /// <summary>
        /// Temporarily replace the active viewport, returning the previous value so
        /// the caller can restore it after rendering a split-screen region.
        /// </summary>
        public RectangleF OverrideViewport(RectangleF viewport)
        {
            RectangleF original = Viewport;
            Viewport = viewport;
            return original;
        }

        /// <summary>
        /// Draw a square sprite on the display.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <param name="color"></param>
        /// <param name="angleDegrees"></param>
        public void DrawSquareSprite(Vector2 center, float size, Color color, float angleDegrees = 0)
        {
            float rotation = MathHelper.ToRadians(angleDegrees);
            Frame.Add(SpriteFactory.CreateShape("SquareSimple", center, new Vector2(size, size), color, rotation));
        }

        /// <summary>
        /// Draw a line sprite on the display. This is used to draw the flight plan legs.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="color"></param>
        /// <param name="thickness"></param>
        public void DrawLineSprite(Vector2 start, Vector2 end, Color color, float thickness)
        {
            var midpoint = (start + end) / 2;
            var length = Vector2.Distance(start, end);
            var angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);

            Frame.Add(SpriteFactory.CreateShape("SquareSimple", midpoint, new Vector2(length, thickness), color, angle));
        }

        /// <summary>
        /// Draw an octagon sprite on the display. This is used to draw the Mother sprite.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <param name="outlineColor"></param>
        /// <param name="fillColor"></param>
        public void DrawOctagonSprite(Vector2 center, float size, Color outlineColor, Color fillColor)
        {
            float radius = size / 2f;
            Vector2[] points = new Vector2[8];
            float angleStep = MathHelper.PiOver4; // 45-degree increments
            float rotationOffset = MathHelper.Pi / 8; // 22.5-degree rotation

            for (int i = 0; i < 8; i++)
            {
                float angle = i * angleStep + rotationOffset;
                points[i] = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            }

            // Draw background (simulates empty interior)
            DrawSquareSprite(center, size, fillColor);

            // Draw octagon outline using separate lines
            for (int i = 0; i < 8; i++)
            {
                DrawLineSprite(points[i], points[(i + 1) % 8], outlineColor, 1f); // Line thickness = 1f
            }
        }


        /// <summary>
        /// Draw a circle sprite on the display. This is used to draw grid sprites.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="diameter"></param>
        /// <param name="color"></param>
        public void DrawCircleSprite(Vector2 center, float diameter, Color color)
        {
            Frame.Add(SpriteFactory.CreateShape("Circle", center, new Vector2(diameter, diameter), color));
        }

        /// <summary>
        /// Draw a triangle sprite on the display. This is used to draw this grid's sprite.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <param name="color"></param>
        /// <param name="angleDegrees"></param>
        public void DrawTriangleSprite(Vector2 center, float size, Color color, float angleDegrees = 0)
        {
            float rotation = MathHelper.ToRadians(angleDegrees);
            Frame.Add(SpriteFactory.CreateShape("Triangle", center, new Vector2(size, size), color, rotation));
        }

        /// <summary>
        /// Draw the background for the display. This is a black rectangle that fills the entire display.
        /// </summary>
        public void DrawBackground()
        {
            Frame.Add(SpriteFactory.CreateShape("SquareSimple", Surface.TextureSize / 2f, Surface.TextureSize, Color.Black));
        }

        /// <summary>
        /// Draw text on the display using an explicit colour and font.
        /// </summary>
        public void DrawText(string text, Vector2 position, Color color, string fontId, float scale = 1f)
        {
            Frame.Add(new MySprite
            {
                Type = SpriteType.TEXT,
                Data = text,
                Position = position,
                RotationOrScale = GetScalingFactor() * scale,
                Color = color,
                Alignment = TextAlignment.LEFT,
                FontId = fontId
            });
        }

        /// <summary>
        /// Draw text on the display using an explicit colour and the default theme font
        /// (Monospace — matches <c>MotherTheme.Fonts.Mono</c>).
        /// </summary>
        public void DrawText(string text, Vector2 position, Color color, float scale = 1f)
            => DrawText(text, position, color, "Monospace", scale);

        /// <summary>
        /// Draw text on the display using the default theme colour (white — matches
        /// <c>MotherTheme.Colors.ValueText</c>) and font (<c>MotherTheme.Fonts.Mono</c>).
        /// </summary>
        public void DrawText(string text, Vector2 position, float scale = 1f)
            => DrawText(text, position, Color.White, "Monospace", scale);

        /// <summary>
        /// Draw horizontally-centred text at <paramref name="position"/>.
        /// The X coordinate of <paramref name="position"/> is treated as the
        /// horizontal centre; Y is the top of the text line.
        /// </summary>
        public void DrawTextCentered(string text, Vector2 position, Color color, float scale = 1f)
        {
            Frame.Add(new MySprite
            {
                Type             = SpriteType.TEXT,
                Data             = text,
                Position         = position,
                RotationOrScale  = GetScalingFactor() * scale,
                Color            = color,
                Alignment        = TextAlignment.CENTER,
                FontId           = "Monospace"
            });
        }

        /// <summary>
        /// Draw the Mother sprite on the display. This is a red circle inside an octagon.
        /// </summary>
        public void DrawMotherSprite()
        {
            // Calculate the ViewportCenter position for the bottom-right corner
            // Subtract the size from the bottom-right corner to keep the sprite fully inside the screen
            Vector2 center = new Vector2(
                Viewport.X + Viewport.Width,
                Viewport.Y + Viewport.Height
            );

            // Adjust the size based on the display type (cockpit or regular)
            float size = IsCockpitDisplay ? 20 : 40;

            // Ensure the sprite is centered at the bottom-right corner
            center -= new Vector2(size / 2, size / 2);

            // Draw the Mother sprite at the calculated position, with the
            // circle inside the octagon
            DrawOctagonSprite(center, size, Color.White, Color.Black);
            DrawCircleSprite(center, size * 0.4f, Color.Red);
        }

        /// <summary>
        /// Draw the debug information on the display. This draws the corners and 
        /// center of the viewport to help with development.
        /// </summary>
        public void DrawDebug()
        {
            // Scale the positioning for small and large screens
            float scale = GetScalingFactor();

            // Draw small dots at the corners and ViewportCenter with scaling applied
            DrawCircleSprite(TopLeft, 5 * scale, Color.Red);       // Top Left
            DrawCircleSprite(TopRight, 5 * scale, Color.Red);      // Top Right
            DrawCircleSprite(BottomLeft, 5 * scale, Color.Red);    // Bottom Left
            DrawCircleSprite(BottomRight, 5 * scale, Color.Red);   // Bottom Right
            DrawCircleSprite(ViewportCenter, 5 * scale, Color.Red);        // Center

            Vector2 textPosition =
                IsCockpitDisplay
                ? BottomLeft - new Vector2(0, 1.5f * FontSize)
                : BottomLeft - new Vector2(0, 2f * FontSize);

            // Draw the text at the bottom-left corner of the screen
            DrawText(
                $"scale={Scale:F2}",
                textPosition,
                Color.White,
                "White"
            );
        }

        /// <summary>
        /// Create a text line with left and right aligned text, separated by dots.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string CreateTextLine(string left, string right, int maxLength)
        {
            const int minDots = 5;

            // Ensure we have room for right side and minimum dots
            int maxLeftLength = maxLength - right.Length - minDots;

            // Not enough space even for right + dots, just return trimmed right
            if (maxLeftLength < 0)
                return right.Substring(Math.Max(0, right.Length - maxLength));

            // Trim left if needed
            string trimmedLeft = left.Length > maxLeftLength ? left.Substring(0, maxLeftLength) : left;

            // Calculate how many dots to insert
            int dotsCount = maxLength - trimmedLeft.Length - right.Length;

            if (dotsCount < minDots)
            {
                // Fallback: trim left more to allow minDots
                int extraNeeded = minDots - dotsCount;
                trimmedLeft = trimmedLeft.Substring(0, Math.Max(0, trimmedLeft.Length - extraNeeded));
                dotsCount = maxLength - trimmedLeft.Length - right.Length;
            }

            string dots = new string('.', dotsCount);

            return $"{trimmedLeft}{dots}{right}";
        }
    }
}
