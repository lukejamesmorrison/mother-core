using IngameScript;
using NUnit.Framework;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class ResponseTests
    {
        [Test]
        public void It_Can_Serialize_And_Deserialize_A_Response()
        {
            var body = new Dictionary<string, object>
            {
                { "Result", "OK" },
                { "Distance", "42.5" }
            };

            var header = new Dictionary<string, object>
            {
                { "Id", "resp-1" },
                { "Status", "200" },
                { "OriginName", "Relay" },
                { "OriginId", "2002" },
                { "RespondingToId", "request-123" },
                { "TargetId", "9001" },
                { "TargetName", "RemoteScout" }
            };

            Response response = new Response(body, header);
            string serialized = response.Serialize();

            Response deserialized = Response.Deserialize(serialized);

            Assert.That(serialized, Does.StartWith("RESPONSE::"));
            Assert.That(deserialized.BString("Result"), Is.EqualTo("OK"));
            Assert.That(deserialized.BDouble("Distance"), Is.EqualTo(42.5d));
            Assert.That(deserialized.HString("Status"), Is.EqualTo("200"));
            Assert.That(deserialized.HString("OriginName"), Is.EqualTo("Relay"));
            Assert.That(deserialized.HLong("OriginId"), Is.EqualTo(2002L));
            Assert.That(deserialized.HString("RespondingToId"), Is.EqualTo("request-123"));
            Assert.That(deserialized.HString("TargetId"), Is.EqualTo("9001"));
            Assert.That(deserialized.HString("TargetName"), Is.EqualTo("RemoteScout"));
            Assert.That(deserialized.HString("Id"), Is.EqualTo("resp-1"));
        }

        [Test]
        public void GetResponseCodeValue_Returns_Enum_Integer_Value()
        {
            int code = Response.GetResponseCodeValue(Response.ResponseStatusCodes.NOT_FOUND);

            Assert.That(code, Is.EqualTo(404));
        }
    }
}