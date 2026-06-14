using IngameScript;
using NUnit.Framework;
using System.Collections.Generic;
using VRageMath;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class SerializerTests
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
        public void It_Can_Serialize_And_Deserialize_A_Flat_List()
        {
            var items = new List<object> { "one", "two", "three" };

            string serialized = Serializer.SerializeList(items);
            List<object> deserialized = Serializer.DeserializeList(serialized);

            Assert.That(serialized, Is.EqualTo("[\"one\",\"two\",\"three\"]"));
            Assert.That(deserialized, Is.EqualTo(items));
        }

        [Test]
        public void It_Can_Serialize_And_Deserialize_Nested_Lists()
        {
            var items = new List<object>
            {
                "root",
                new List<object> { "nested-1", "nested-2" },
                new List<object>
                {
                    "deeper",
                    new List<object> { "deepest" }
                }
            };

            string serialized = Serializer.SerializeList(items);
            List<object> deserialized = Serializer.DeserializeList(serialized);

            Assert.That(serialized, Is.EqualTo("[\"root\",[\"nested-1\",\"nested-2\"],[\"deeper\",[\"deepest\"]]]"));
            Assert.That(deserialized[0], Is.EqualTo("root"));
            Assert.That((List<object>)deserialized[1], Is.EqualTo(new List<object> { "nested-1", "nested-2" }));

            var deeper = (List<object>)deserialized[2];
            Assert.That(deeper[0], Is.EqualTo("deeper"));
            Assert.That((List<object>)deeper[1], Is.EqualTo(new List<object> { "deepest" }));
        }

        [Test]
        public void It_Can_Serialize_And_Deserialize_A_List_Of_Dictionaries()
        {
            var items = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "name", "alpha" },
                    { "value", "1" }
                },
                new Dictionary<string, object>
                {
                    { "name", "beta" },
                    { "value", "2" }
                }
            };

            string serialized = Serializer.SerializeList(items);
            List<object> deserialized = Serializer.DeserializeList(serialized);

            Assert.That(serialized, Is.EqualTo("[{\"name\":\"alpha\",\"value\":\"1\"},{\"name\":\"beta\",\"value\":\"2\"}]"));

            var first = (Dictionary<string, object>)deserialized[0];
            var second = (Dictionary<string, object>)deserialized[1];

            Assert.That(first["name"], Is.EqualTo("alpha"));
            Assert.That(first["value"], Is.EqualTo("1"));
            Assert.That(second["name"], Is.EqualTo("beta"));
            Assert.That(second["value"], Is.EqualTo("2"));
        }

        [Test]
        public void It_Can_Serialize_And_Deserialize_A_Dictionary_With_List_Values()
        {
            var items = new Dictionary<string, object>
            {
                {
                    "names",
                    new List<object> { "alpha", "beta" }
                },
                {
                    "groups",
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "name", "primary" },
                            { "members", new List<object> { "a", "b" } }
                        }
                    }
                }
            };

            string serialized = Serializer.SerializeDictionary(items);
            Dictionary<string, object> deserialized = Serializer.DeserializeDictionary(serialized);

            Assert.That(serialized, Is.EqualTo("{\"names\":[\"alpha\",\"beta\"],\"groups\":[{\"name\":\"primary\",\"members\":[\"a\",\"b\"]}]}"));

            Assert.That((List<object>)deserialized["names"], Is.EqualTo(new List<object> { "alpha", "beta" }));

            var groups = (List<object>)deserialized["groups"];
            var firstGroup = (Dictionary<string, object>)groups[0];
            Assert.That(firstGroup["name"], Is.EqualTo("primary"));
            Assert.That((List<object>)firstGroup["members"], Is.EqualTo(new List<object> { "a", "b" }));
        }

        [Test]
        public void It_Preserves_Quotes_And_Backslashes_In_Round_Trips()
        {
            var items = new Dictionary<string, object>
            {
                { "quote", "He said \"hello\"" },
                { "path", "C:\\Mother\\Tests" },
                { "list", new List<object> { "\\server\\share", "\"wrapped\"" } }
            };

            string serialized = Serializer.SerializeDictionary(items);
            Dictionary<string, object> deserialized = Serializer.DeserializeDictionary(serialized);

            Assert.That(serialized, Does.Contain("\\\"hello\\\""));
            Assert.That(serialized, Does.Contain("C:\\\\Mother\\\\Tests"));
            Assert.That(deserialized["quote"], Is.EqualTo("He said \"hello\""));
            Assert.That(deserialized["path"], Is.EqualTo("C:\\Mother\\Tests"));

            var list = (List<object>)deserialized["list"];
            Assert.That(list[0], Is.EqualTo("\\server\\share"));
            Assert.That(list[1], Is.EqualTo("\"wrapped\""));
        }

        [Test]
        public void It_Can_Serialize_And_Deserialize_Empty_Collections()
        {
            var emptyDictionary = new Dictionary<string, object>();
            var emptyList = new List<object>();

            Assert.That(Serializer.SerializeDictionary(emptyDictionary), Is.EqualTo("{}"));
            Assert.That(Serializer.SerializeList(emptyList), Is.EqualTo("[]"));
            Assert.That(Serializer.DeserializeDictionary("{}"), Is.Empty);
            Assert.That(Serializer.DeserializeList("[]"), Is.Empty);
        }

        [Test]
        public void Malformed_Input_Falls_Back_To_Empty_Collections()
        {
            Assert.That(Serializer.DeserializeDictionary(null), Is.Empty);
            Assert.That(Serializer.DeserializeDictionary(string.Empty), Is.Empty);
            Assert.That(Serializer.DeserializeDictionary("{\"key\":\"value\""), Is.Empty);

            Assert.That(Serializer.DeserializeList(null), Is.Empty);
            Assert.That(Serializer.DeserializeList(string.Empty), Is.Empty);
            Assert.That(Serializer.DeserializeList("[\"value\""), Is.Empty);
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


