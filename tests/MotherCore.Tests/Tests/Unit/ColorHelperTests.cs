using IngameScript;
using NUnit.Framework;
using VRageMath;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class ColorHelperTests
    {
        //[Test]
        //public void Colors_Dictionary_Contains_Common_Color_Entries()
        //{
        //    Assert.That(ColorHelper.COLORS.ContainsKey("red"), Is.True);
        //    Assert.That(ColorHelper.COLORS.ContainsKey("blue"), Is.True);
        //    Assert.That(ColorHelper.COLORS["red"], Is.EqualTo("255,0,0"));
        //}

        [Test]
        public void GetColorFromRGBString_Parses_Comma_Separated_Components()
        {
            var color = ColorHelper.GetColorFromRGBString("12,34,56");

            Assert.That(color, Is.EqualTo(new Color(12, 34, 56)));
        }

        [Test]
        public void GetColorFromHexString_Parses_With_Hash_Prefix()
        {
            var color = ColorHelper.GetColorFromHexString("#0A141E");

            Assert.That(color, Is.EqualTo(new Color(10, 20, 30)));
        }

        [Test]
        public void GetColorFromHexString_Parses_Without_Hash_Prefix()
        {
            var color = ColorHelper.GetColorFromHexString("FF8000");

            Assert.That(color, Is.EqualTo(new Color(255, 128, 0)));
        }

        [Test]
        public void GetColorFromColorName_Is_Case_Insensitive_For_Known_Colors()
        {
            var color = ColorHelper.GetColorFromColorName("YeLLoW");

            Assert.That(color, Is.EqualTo(new Color(255, 255, 0)));
        }

        [Test]
        public void GetColorFromColorName_Returns_White_For_Unknown_Color()
        {
            var color = ColorHelper.GetColorFromColorName("not-a-real-color");

            Assert.That(color, Is.EqualTo(Color.White));
        }

        [Test]
        public void GetColor_Routes_To_Rgb_When_String_Contains_Comma()
        {
            var color = ColorHelper.GetColor("1,2,3");

            Assert.That(color, Is.EqualTo(new Color(1, 2, 3)));
        }

        [Test]
        public void GetColor_Routes_To_Hex_When_String_Starts_With_Hash()
        {
            var color = ColorHelper.GetColor("#010203");

            Assert.That(color, Is.EqualTo(new Color(1, 2, 3)));
        }

        [Test]
        public void GetColor_Routes_To_Name_When_Not_Rgb_Or_Hex()
        {
            var color = ColorHelper.GetColor("cyan");

            Assert.That(color, Is.EqualTo(new Color(0, 255, 255)));
        }
    }
}

