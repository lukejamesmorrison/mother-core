using IngameScript;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class RequestTests
    {

        [Test]
        public void It_Can_Be_Instantiated_With_A_Body_and_Header()
        {
            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 }
            };

            Dictionary<string, object> header = new Dictionary<string, object>
            {
                { "headerKey1", "headerValue1" },
                { "headerKey2", 3.14 }
            };

            Request request = new Request(body, header);

            Assert.That(request.Body, Is.EqualTo(body));
            Assert.That(request.Header, Is.EqualTo(header));
        }

        [Test]
        public void It_Can_Serialize_And_Deserialize_A_Request()
        {
            var body = new Dictionary<string, object>
            {
                { "Command", "ping" },
                { "Retries", "2" }
            };

            var header = new Dictionary<string, object>
            {
                { "Id", "req-1" },
                { "OriginName", "Scout" },
                { "Path", "ping" },
                { "OriginId", "1001" }
            };

            Request request = new Request(body, header);
            string serialized = request.Serialize();

            Request deserialized = Request.Deserialize(serialized);

            Assert.That(serialized, Does.StartWith("REQUEST::"));
            Assert.That(deserialized.BString("Command"), Is.EqualTo("ping"));
            Assert.That(deserialized.BDouble("Retries"), Is.EqualTo(2d));
            Assert.That(deserialized.HString("OriginName"), Is.EqualTo("Scout"));
            Assert.That(deserialized.HString("Path"), Is.EqualTo("ping"));
            Assert.That(deserialized.HLong("OriginId"), Is.EqualTo(1001L));
            Assert.That(deserialized.HString("Id"), Is.EqualTo("req-1"));
        }

        [Test]
        public void To_Sets_Target_Header_And_Channels_From_Almanac_Record()
        {
            var request = new Request(new Dictionary<string, object>(), new Dictionary<string, object>());
            var record = new AlmanacRecord("grid-123", "grid", new Vector3D(1, 2, 3))
            {
                DisplayName = "Mothership",
                Channels = new HashSet<string> { "default", "secure" }
            };

            request.To(record);

            Assert.That(request.HString("TargetId"), Is.EqualTo("grid-123"));
            Assert.That(request.HString("TargetName"), Is.EqualTo("Mothership"));
            Assert.That(request.Channels.OrderBy(channel => channel), Is.EqualTo(record.Channels.OrderBy(channel => channel)));
        }
    }
}


