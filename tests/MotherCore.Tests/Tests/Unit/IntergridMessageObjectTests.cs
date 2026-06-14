using IngameScript;
using NUnit.Framework;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class IntergridMessageObjectTests
    {
        class TestMessageObject : IntergridMessageObject
        {
            public TestMessageObject(Dictionary<string, object> body, Dictionary<string, object> header)
                : base(body, header) { }
        }

        [Test]
        public void Constructor_Adds_Id_To_Header_When_Missing()
        {
            var message = new TestMessageObject(
                new Dictionary<string, object>(),
                new Dictionary<string, object>()
            );

            Assert.That(message.HString("Id"), Is.Not.Empty);
        }

        [Test]
        public void Constructor_Preserves_Existing_Header_Id()
        {
            var message = new TestMessageObject(
                new Dictionary<string, object>(),
                new Dictionary<string, object> { { "Id", "existing-id" } }
            );

            Assert.That(message.HString("Id"), Is.EqualTo("existing-id"));
        }

        [Test]
        public void B_Returns_Body_Value_And_Empty_String_For_Null_Or_Missing_Keys()
        {
            var message = new TestMessageObject(
                new Dictionary<string, object>
                {
                    { "Name", "Scout" },
                    { "NullField", null }
                },
                new Dictionary<string, object>()
            );

            Assert.That(message.B("Name"), Is.EqualTo("Scout"));
            Assert.That(message.B("NullField"), Is.EqualTo(""));
            Assert.That(message.B("Missing"), Is.EqualTo(""));
        }

        [Test]
        public void BString_BFloat_And_BDouble_Return_Converted_Body_Values()
        {
            var message = new TestMessageObject(
                new Dictionary<string, object>
                {
                    { "Text", 123 },
                    { "FloatValue", "12.5" },
                    { "DoubleValue", "42.75" },
                    { "Invalid", "not-a-number" }
                },
                new Dictionary<string, object>()
            );

            Assert.That(message.BString("Text"), Is.EqualTo("123"));
            Assert.That(message.BFloat("FloatValue"), Is.EqualTo(12.5f).Within(0.0001f));
            Assert.That(message.BDouble("DoubleValue"), Is.EqualTo(42.75d).Within(0.0001d));
            Assert.That(message.BFloat("Invalid"), Is.EqualTo(0f));
            Assert.That(message.BDouble("Invalid"), Is.EqualTo(0d));
        }

        [Test]
        public void H_Returns_Header_Value_And_Empty_String_For_Null_Or_Missing_Keys()
        {
            var message = new TestMessageObject(
                new Dictionary<string, object>(),
                new Dictionary<string, object>
                {
                    { "Origin", "Relay" },
                    { "NullField", null }
                }
            );

            Assert.That(message.H("Origin"), Is.EqualTo("Relay"));
            Assert.That(message.H("NullField"), Is.EqualTo(""));
            Assert.That(message.H("Missing"), Is.EqualTo(""));
        }

        [Test]
        public void HString_HFloat_HDouble_And_HLong_Return_Converted_Header_Values()
        {
            var message = new TestMessageObject(
                new Dictionary<string, object>(),
                new Dictionary<string, object>
                {
                    { "Count", 7 },
                    { "FloatValue", "18.25" },
                    { "DoubleValue", "99.5" },
                    { "LongValue", "123456789" },
                    { "Invalid", "not-a-number" }
                }
            );

            Assert.That(message.HString("Count"), Is.EqualTo("7"));
            Assert.That(message.HFloat("FloatValue"), Is.EqualTo(18.25f).Within(0.0001f));
            Assert.That(message.HDouble("DoubleValue"), Is.EqualTo(99.5d).Within(0.0001d));
            Assert.That(message.HLong("LongValue"), Is.EqualTo(123456789L));
            Assert.That(message.HFloat("Invalid"), Is.EqualTo(0f));
            Assert.That(message.HDouble("Invalid"), Is.EqualTo(0d));
            Assert.That(message.HLong("Invalid"), Is.EqualTo(0L));
        }

        [Test]
        public void Serialize_Wraps_Serialized_Header_And_Body_In_Tags()
        {
            var message = new TestMessageObject(
                new Dictionary<string, object> { { "Command", "ping" } },
                new Dictionary<string, object> { { "Origin", "Scout" } }
            );

            string serialized = message.Serialize();
            string headerContent = IntergridMessageObject.ExtractTagContent(serialized, "header");
            string bodyContent = IntergridMessageObject.ExtractTagContent(serialized, "body");

            Assert.That(serialized, Does.StartWith("<header>"));
            Assert.That(serialized, Does.Contain("</header><body>"));
            Assert.That(serialized, Does.EndWith("</body>"));
            Assert.That(headerContent, Does.Contain("\"Origin\":\"Scout\""));
            Assert.That(headerContent, Does.Contain("\"Id\":\""));
            Assert.That(bodyContent, Is.EqualTo("{\"Command\":\"ping\"}"));
        }

        [Test]
        public void ExtractTagContent_Returns_Tag_Content_For_Valid_Message()
        {
            string message = "<header>{\"Id\":\"123\"}</header><body>{\"Command\":\"ping\"}</body>";

            string header = IntergridMessageObject.ExtractTagContent(message, "header");
            string body = IntergridMessageObject.ExtractTagContent(message, "body");

            Assert.That(header, Is.EqualTo("{\"Id\":\"123\"}"));
            Assert.That(body, Is.EqualTo("{\"Command\":\"ping\"}"));
        }

        [Test]
        public void ExtractTagContent_Returns_Empty_When_Opening_Tag_Is_Missing()
        {
            string message = "payload without header tag but with stray closer </header>";

            string content = IntergridMessageObject.ExtractTagContent(message, "header");

            Assert.That(content, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ExtractTagContent_Returns_Empty_When_Closing_Tag_Is_Missing()
        {
            string message = "<header>{\"Id\":\"123\"}";

            string content = IntergridMessageObject.ExtractTagContent(message, "header");

            Assert.That(content, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ExtractTagContent_Trims_Outer_Whitespace_From_Tag_Content()
        {
            string message = "<header>  {\"Id\":\"123\"}  </header>";

            string content = IntergridMessageObject.ExtractTagContent(message, "header");

            Assert.That(content, Is.EqualTo("{\"Id\":\"123\"}"));
        }
    }
}

