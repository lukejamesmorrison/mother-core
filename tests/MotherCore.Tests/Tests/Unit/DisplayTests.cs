using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using NUnit.Framework;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace MotherCore.Tests.Tests.Unit
{
    [Category(TestCategories.LayerUnit)]
    public class DisplayTests
    {
        static List<MySprite> GetSprites(MySpriteDrawFrame frame)
        {
            var sprites = new List<MySprite>();
            frame.AddToList(sprites);
            return sprites;
        }

        static Display CreateDisplay(
            Vector2? surfaceSize = null,
            Vector2? textureSize = null,
            bool isCockpitDisplay = false)
        {
            var resolvedSurfaceSize = surfaceSize ?? new Vector2(100f, 50f);
            var resolvedTextureSize = textureSize ?? resolvedSurfaceSize;

            var surface = TextSurfaceFactory.Create(
                surfaceSize: resolvedSurfaceSize,
                textureSize: resolvedTextureSize,
                configure: textSurface =>
                    textSurface.DrawFrameFactory = () => new MySpriteDrawFrame(_ => { }));

            var block = TerminalBlockFactory.Create<Sandbox.ModAPI.Ingame.IMyTerminalBlock>(customName: "LCD");
            var display = new Display(surface.Surface, block, new MyIni(), isCockpitDisplay);

            display.Frame = new MySpriteDrawFrame(_ => { });
            return display;
        }

        [Test]
        public void Constructor_Calculates_Viewport_Corners_And_Minimum_Font_Size()
        {
            var surface = TextSurfaceFactory.Create(
                surfaceSize: new Vector2(100f, 50f),
                textureSize: new Vector2(100f, 50f));

            var block = TerminalBlockFactory.Create<Sandbox.ModAPI.Ingame.IMyTerminalBlock>(customName: "LCD");

            var display = new Display(surface.Surface, block, new MyIni());

            Assert.That(display.Viewport.X, Is.EqualTo(4f));
            Assert.That(display.Viewport.Y, Is.EqualTo(4f));
            Assert.That(display.Viewport.Width, Is.EqualTo(84f));
            Assert.That(display.Viewport.Height, Is.EqualTo(34f));
            Assert.That(display.TopLeft, Is.EqualTo(new Vector2(8f, 8f)));
            Assert.That(display.TopRight, Is.EqualTo(new Vector2(84f, 8f)));
            Assert.That(display.BottomLeft, Is.EqualTo(new Vector2(8f, 34f)));
            Assert.That(display.BottomRight, Is.EqualTo(new Vector2(84f, 34f)));
            Assert.That(display.ViewportCenter, Is.EqualTo(new Vector2(46f, 21f)));
            Assert.That(display.FontSize, Is.EqualTo(8f));
            Assert.That(display.GetScalingFactor(), Is.EqualTo(0.5f));
        }

        [Test]
        public void CalculateFontSize_Bounds_To_Maximum_On_Large_Displays()
        {
            var surface = TextSurfaceFactory.Create(
                surfaceSize: new Vector2(3000f, 1200f),
                textureSize: new Vector2(3000f, 1200f));

            var block = TerminalBlockFactory.Create<Sandbox.ModAPI.Ingame.IMyTerminalBlock>(customName: "Bridge LCD");

            var display = new Display(surface.Surface, block, new MyIni());

            Assert.That(display.FontSize, Is.EqualTo(32f));
            Assert.That(display.GetScalingFactor(), Is.EqualTo(1f));
        }

        [Test]
        public void IsWidescreen_And_OverrideViewport_Work_Against_The_Active_Viewport()
        {
            var surface = TextSurfaceFactory.Create(
                surfaceSize: new Vector2(400f, 200f),
                textureSize: new Vector2(400f, 200f));

            var block = TerminalBlockFactory.Create<Sandbox.ModAPI.Ingame.IMyTerminalBlock>(customName: "Map LCD");

            var display = new Display(surface.Surface, block, new MyIni());
            var replacement = new RectangleF(10f, 20f, 50f, 25f);

            var original = display.OverrideViewport(replacement);

            Assert.That(display.IsWidescreen, Is.True);
            Assert.That(original, Is.EqualTo(new RectangleF(4f, 4f, 384f, 184f)));
            Assert.That(display.Viewport, Is.EqualTo(replacement));
        }

        [Test]
        public void SetScale_Uses_The_More_Constrained_Axis()
        {
            var surface = TextSurfaceFactory.Create(
                surfaceSize: new Vector2(100f, 50f),
                textureSize: new Vector2(100f, 50f));

            var block = TerminalBlockFactory.Create<Sandbox.ModAPI.Ingame.IMyTerminalBlock>(customName: "Scale LCD");

            var display = new Display(surface.Surface, block, new MyIni());

            display.SetScale(Vector3D.Zero, new Vector3D(10d, 5d, 0d));

            Assert.That(display.Scale, Is.EqualTo(6.8f).Within(0.0001f));
        }

        [Test]
        public void CreateTextLine_Trims_The_Left_And_Preserves_Minimum_Dot_Padding()
        {
            var line = Display.CreateTextLine("LongLeft", "END", 10);

            Assert.That(line, Is.EqualTo("Lo.....END"));
        }

        [Test]
        public void CreateTextLine_When_Right_Side_Consumes_The_Line_Returns_Trimmed_Right_Text()
        {
            var line = Display.CreateTextLine("ignored", "ABCDEFG", 4);

            Assert.That(line, Is.EqualTo("DEFG"));
        }

        [Test]
        public void UpdateConfig_Replaces_The_Current_Configuration_Instance()
        {
            var display = CreateDisplay();
            var updated = new MyIni();
            updated.Set("general", "scale", "2");

            display.UpdateConfig(updated);

            Assert.That(display.Configuration, Is.SameAs(updated));
        }

        [Test]
        public void DrawFrame_Creates_A_Frame_And_Adds_A_Transparent_Render_Primer_Sprite()
        {
            var display = CreateDisplay();

            display.DrawFrame();
            var sprites = GetSprites(display.Frame);

            Assert.That(sprites, Has.Count.EqualTo(1));
            Assert.That(sprites[0].Type, Is.EqualTo(SpriteType.TEXTURE));
            Assert.That(sprites[0].Data, Is.EqualTo("SquareSimple"));
            Assert.That(sprites[0].Color, Is.EqualTo(Color.Transparent));
        }

        [Test]
        public void Primitive_Drawing_Methods_Add_The_Expected_Texture_Sprites()
        {
            var display = CreateDisplay();

            display.DrawSquareSprite(new Vector2(10f, 12f), 8f, Color.Blue, 90f);
            display.DrawLineSprite(new Vector2(0f, 0f), new Vector2(6f, 8f), Color.Green, 2f);
            display.DrawCircleSprite(new Vector2(20f, 25f), 9f, Color.Red);
            display.DrawTriangleSprite(new Vector2(30f, 35f), 7f, Color.Yellow, 180f);
            display.DrawBackground();
            var sprites = GetSprites(display.Frame);

            Assert.That(sprites, Has.Count.EqualTo(5));

            Assert.That(sprites[0].Data, Is.EqualTo("SquareSimple"));
            Assert.That(sprites[0].Size, Is.EqualTo(new Vector2(8f, 8f)));
            Assert.That(sprites[0].RotationOrScale, Is.EqualTo(MathHelper.PiOver2).Within(0.0001f));

            Assert.That(sprites[1].Data, Is.EqualTo("SquareSimple"));
            Assert.That(sprites[1].Position, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(sprites[1].Size, Is.EqualTo(new Vector2(10f, 2f)));
            Assert.That(sprites[1].RotationOrScale, Is.EqualTo((float)System.Math.Atan2(8f, 6f)).Within(0.0001f));

            Assert.That(sprites[2].Data, Is.EqualTo("Circle"));
            Assert.That(sprites[2].Size, Is.EqualTo(new Vector2(9f, 9f)));

            Assert.That(sprites[3].Data, Is.EqualTo("Triangle"));
            Assert.That(sprites[3].RotationOrScale, Is.EqualTo(MathHelper.Pi).Within(0.0001f));

            Assert.That(sprites[4].Data, Is.EqualTo("SquareSimple"));
            Assert.That(sprites[4].Position, Is.EqualTo(new Vector2(50f, 25f)));
            Assert.That(sprites[4].Size, Is.EqualTo(new Vector2(100f, 50f)));
            Assert.That(sprites[4].Color, Is.EqualTo(Color.Black));
        }

        [Test]
        public void DrawOctagonSprite_Adds_One_Fill_Sprite_And_Eight_Outline_Segments()
        {
            var display = CreateDisplay();

            display.DrawOctagonSprite(new Vector2(40f, 20f), 16f, Color.White, Color.Black);
            var sprites = GetSprites(display.Frame);

            Assert.That(sprites, Has.Count.EqualTo(9));
            Assert.That(sprites[0].Data, Is.EqualTo("SquareSimple"));
            Assert.That(sprites[0].Color, Is.EqualTo(Color.Black));

            for (int i = 1; i < sprites.Count; i++)
            {
                Assert.That(sprites[i].Data, Is.EqualTo("SquareSimple"));
                Assert.That(sprites[i].Color, Is.EqualTo(Color.White));
                Assert.That(sprites[i].Size.HasValue, Is.True);
                Assert.That(sprites[i].Size.Value.Y, Is.EqualTo(1f));
            }
        }

        [Test]
        public void DrawText_Methods_Add_Text_Sprites_With_Expected_Fonts_Colours_And_Alignment()
        {
            var display = CreateDisplay();

            display.DrawText("alpha", new Vector2(1f, 2f), Color.Aqua, "Debug", 2f);
            display.DrawText("beta", new Vector2(3f, 4f), Color.Orange, 1.5f);
            display.DrawText("gamma", new Vector2(5f, 6f), 0.75f);
            display.DrawTextCentered("delta", new Vector2(7f, 8f), Color.Pink, 1.25f);
            var sprites = GetSprites(display.Frame);

            Assert.That(sprites, Has.Count.EqualTo(4));

            Assert.That(sprites[0].Type, Is.EqualTo(SpriteType.TEXT));
            Assert.That(sprites[0].Data, Is.EqualTo("alpha"));
            Assert.That(sprites[0].FontId, Is.EqualTo("Debug"));
            Assert.That(sprites[0].Color, Is.EqualTo(Color.Aqua));
            Assert.That(sprites[0].Alignment, Is.EqualTo(TextAlignment.LEFT));
            Assert.That(sprites[0].RotationOrScale, Is.EqualTo(1f));

            Assert.That(sprites[1].FontId, Is.EqualTo("Monospace"));
            Assert.That(sprites[1].Color, Is.EqualTo(Color.Orange));
            Assert.That(sprites[1].RotationOrScale, Is.EqualTo(0.75f));

            Assert.That(sprites[2].FontId, Is.EqualTo("Monospace"));
            Assert.That(sprites[2].Color, Is.EqualTo(Color.White));
            Assert.That(sprites[2].RotationOrScale, Is.EqualTo(0.375f));

            Assert.That(sprites[3].Alignment, Is.EqualTo(TextAlignment.CENTER));
            Assert.That(sprites[3].FontId, Is.EqualTo("Monospace"));
            Assert.That(sprites[3].RotationOrScale, Is.EqualTo(0.625f));
        }

        [Test]
        public void DrawMotherSprite_Places_The_Badge_In_The_Bottom_Right_Corner()
        {
            var display = CreateDisplay();

            display.DrawMotherSprite();
            var sprites = GetSprites(display.Frame);

            Assert.That(sprites, Has.Count.EqualTo(10));
            Assert.That(sprites[0].Position, Is.EqualTo(new Vector2(68f, 18f)));
            Assert.That(sprites[0].Size, Is.EqualTo(new Vector2(40f, 40f)));
            Assert.That(sprites[9].Data, Is.EqualTo("Circle"));
            Assert.That(sprites[9].Position, Is.EqualTo(new Vector2(68f, 18f)));
            Assert.That(sprites[9].Size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(sprites[9].Color, Is.EqualTo(Color.Red));
        }

        [Test]
        public void DrawDebug_Adds_Five_Markers_And_A_Scale_Label()
        {
            var display = CreateDisplay(isCockpitDisplay: true);
            display.Scale = 2.5f;

            display.DrawDebug();
            var sprites = GetSprites(display.Frame);

            Assert.That(sprites, Has.Count.EqualTo(6));

            for (int i = 0; i < 5; i++)
            {
                Assert.That(sprites[i].Data, Is.EqualTo("Circle"));
                Assert.That(sprites[i].Color, Is.EqualTo(Color.Red));
                Assert.That(sprites[i].Size, Is.EqualTo(new Vector2(2.5f, 2.5f)));
            }

            Assert.That(sprites[5].Type, Is.EqualTo(SpriteType.TEXT));
            Assert.That(sprites[5].Data, Is.EqualTo("scale=2.50"));
            Assert.That(sprites[5].Position, Is.EqualTo(new Vector2(4f, 26f)));
            Assert.That(sprites[5].FontId, Is.EqualTo("White"));
        }
    }
}

