using IngameScript;
using NUnit.Framework;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class RouterTests
    {
        [Test]
        public void HandleRoute_Invokes_Registered_Handler()
        {
            var router = new Router();
            var request = new Request(new Dictionary<string, object>(), new Dictionary<string, object>());

            router.RegisterRoute("ping", _ => new Response(
                new Dictionary<string, object> { { "Result", "pong" } },
                new Dictionary<string, object> { { "Status", "200" } }
            ));

            Response response = router.HandleRoute("ping", request);

            Assert.That(response, Is.Not.Null);
            Assert.That(response.BString("Result"), Is.EqualTo("pong"));
            Assert.That(response.HString("Status"), Is.EqualTo("200"));
        }

        [Test]
        public void HandleRoute_Returns_Null_When_No_Route_Matches()
        {
            var router = new Router();
            var request = new Request(new Dictionary<string, object>(), new Dictionary<string, object>());

            Response response = router.HandleRoute("missing", request);

            Assert.That(response, Is.Null);
        }
    }
}