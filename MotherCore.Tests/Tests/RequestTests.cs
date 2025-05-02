using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
using System.Collections.Generic;

//using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class RequestTests : BaseModuleTests
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
    }
}
