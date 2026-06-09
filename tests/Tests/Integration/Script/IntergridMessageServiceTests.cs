using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using System.Collections.Generic;

namespace MotherCore.Tests.Integration.Script
{
    public class IntergridMessageServiceTests : ScriptTestBase<CoreTestProgram>
    {
        [Test]
        public void CreateResponse_Sets_Request_Correlation_Headers()
        {
            IntergridMessageService service = Mother.GetModule<IntergridMessageService>();

            Request request = new Request(
                new Dictionary<string, object> { { "Command", "ping" } },
                new Dictionary<string, object>
                {
                    { "Id", "request-123" },
                    { "OriginId", "9001" },
                    { "OriginName", "RemoteScout" }
                }
            );

            Response response = service.CreateResponse(request, Response.ResponseStatusCodes.OK);

            Assert.That(response.HString("RespondingToId"), Is.EqualTo("request-123"));
            Assert.That(response.HString("TargetId"), Is.EqualTo("9001"));
            Assert.That(response.HString("TargetName"), Is.EqualTo("RemoteScout"));
            Assert.That(response.HString("Status"), Is.EqualTo("200"));
        }
    }
}