using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
using System.Collections.Generic;

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
    }
}
