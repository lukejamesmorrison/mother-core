using IngameScript;
using NUnit.Framework;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace MotherCore.Tests.Tests.Unit
{
    public class SpriteFactoryTests
    {
        [Test]
        public void CreateShape_Creates_A_Centered_Texture_Sprite()
        {
            var position = new Vector2(25f, 40f);
            var size = new Vector2(80f, 20f);
            var color = Color.CadetBlue;

            var sprite = SpriteFactory.CreateShape("SquareSimple", position, size, color, 1.25f);

            Assert.That(sprite.Type, Is.EqualTo(SpriteType.TEXTURE));
            Assert.That(sprite.Data, Is.EqualTo("SquareSimple"));
            Assert.That(sprite.Position, Is.EqualTo(position));
            Assert.That(sprite.Size, Is.EqualTo(size));
            Assert.That(sprite.Color, Is.EqualTo(color));
            Assert.That(sprite.Alignment, Is.EqualTo(TextAlignment.CENTER));
            Assert.That(sprite.RotationOrScale, Is.EqualTo(1.25f));
        }
    }
}