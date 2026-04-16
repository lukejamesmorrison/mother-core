using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
using System.Collections.Generic;
using VRageMath;

namespace MotherCore.Tests.Tests
{
    public class SerializerTests : BaseModuleTests
    {

        [Test]
        public void It_Can_Serialize_A_Dictionary()
        {
            Dictionary<string, object> items = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", "42" }
            };

            string serialized = Serializer.SerializeDictionary(items);

            Assert.That(serialized, Is.EqualTo("{\"key1\":\"value1\",\"key2\":\"42\"}"));
        }

        [Test]
        public void It_Can_Deserialize_A_Dictionary()
        {
            string serialized = "{\"key1\":\"value1\",\"key2\":\"42\"}";

            Dictionary<string, object> items = Serializer.DeserializeDictionary(serialized);

            Assert.That(items["key1"], Is.EqualTo("value1"));
            Assert.That(items["key2"], Is.EqualTo("42"));
        }

        [Test]
        public void It_Can_Serialize_And_Deserialize_AlmanacRecord_With_Orientation()
        {
            var record = new AlmanacRecord("12345", "grid", new Vector3D(100, 200, 300), 50);
            record.DisplayName = "TestGrid";
            record.UnicastId = 99999;
            record.SafeRadius = 75.5;
            record.Forward = new Vector3D(0.5, 0.6, 0.7);
            record.Up = new Vector3D(0.1, 0.2, 0.3);

            string serialized = record.Serialize();
            Dictionary<string, object> dict = Serializer.DeserializeDictionary(serialized);
            AlmanacRecord deserialized = AlmanacRecord.CreateFromDict(dict);

            Assert.That(deserialized.Id, Is.EqualTo("12345"));
            Assert.That(deserialized.DisplayName, Is.EqualTo("TestGrid"));
            Assert.That(deserialized.UnicastId, Is.EqualTo(99999));
            Assert.That(deserialized.Forward.X, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(deserialized.Forward.Y, Is.EqualTo(0.6).Within(0.0001));
            Assert.That(deserialized.Forward.Z, Is.EqualTo(0.7).Within(0.0001));
            Assert.That(deserialized.Up.X, Is.EqualTo(0.1).Within(0.0001));
            Assert.That(deserialized.Up.Y, Is.EqualTo(0.2).Within(0.0001));
            Assert.That(deserialized.Up.Z, Is.EqualTo(0.3).Within(0.0001));
            Assert.That(deserialized.SafeRadius, Is.EqualTo(75.5).Within(0.0001));
        }

        [Test]
        public void It_Can_Deserialize_AlmanacRecord_Without_Orientation()
        {
            // Simulate a record serialized without orientation fields (backward compatibility)
            var dict = new Dictionary<string, object>
            {
                { "Id", "67890" },
                { "pos", "X:10 Y:20 Z:30" },
                { "EntityType", "grid" },
                { "LastKnownSpeed", "0" }
            };

            AlmanacRecord deserialized = AlmanacRecord.CreateFromDict(dict);

            Assert.That(deserialized.Id, Is.EqualTo("67890"));
            Assert.That(deserialized.Forward, Is.EqualTo(Vector3D.Forward));
            Assert.That(deserialized.Up, Is.EqualTo(Vector3D.Up));
        }

        [Test]
        public void It_Can_Serialize_And_Deserialize_Request_With_Orientation_Headers()
        {
            var header = new Dictionary<string, object>
            {
                { "OriginId", "111" },
                { "GridId", "222" },
                { "OriginName", "TestShip" },
                { "x", "10" },
                { "y", "20" },
                { "z", "30" },
                { "speed", "5" },
                { "fx", "0.5" },
                { "fy", "0.6" },
                { "fz", "0.7" },
                { "ux", "0.1" },
                { "uy", "0.2" },
                { "uz", "0.3" }
            };

            var body = new Dictionary<string, object>
            {
                { "Command", "test" }
            };

            Request request = new Request(body, header);
            string serialized = request.Serialize();
            Request deserialized = Request.Deserialize(serialized);

            Assert.That(deserialized.HString("fx"), Is.EqualTo("0.5"));
            Assert.That(deserialized.HString("fy"), Is.EqualTo("0.6"));
            Assert.That(deserialized.HString("fz"), Is.EqualTo("0.7"));
            Assert.That(deserialized.HString("ux"), Is.EqualTo("0.1"));
            Assert.That(deserialized.HString("uy"), Is.EqualTo("0.2"));
            Assert.That(deserialized.HString("uz"), Is.EqualTo("0.3"));
        }
    }
}
