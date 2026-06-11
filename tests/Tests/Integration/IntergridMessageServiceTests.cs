using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace MotherCore.Tests.Integration
{
    public class IntergridMessageServiceTests : ScriptTestBase<CoreTestProgram>
    {
        static string BuildAlphaChannelCustomData(string alias = null, string command = null)
        {
            var composer = new CustomDataComposer()
                .With("channels", "alpha", "key");

            if (!string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(command))
                composer.WithCommand(alias, command);

            return composer.Build();
        }

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

        [Test]
        public void CreateRequest_Includes_Path_And_Standard_Header()
        {
            IntergridMessageService service = Mother.GetModule<IntergridMessageService>();

            Request request = service.CreateRequest("ping",
                new Dictionary<string, object> { { "Command", "help" } },
                new Dictionary<string, object> { { "Custom", "Value" } });

            Assert.That(request.HString("Path"), Is.EqualTo("ping"));
            Assert.That(request.HString("OriginId"), Is.EqualTo($"{Mother.Id}"));
            Assert.That(request.HString("GridId"), Is.EqualTo($"{Mother.GridId}"));
            Assert.That(request.HString("OriginName"), Is.EqualTo(Mother.Name));
            Assert.That(request.HString("Custom"), Is.EqualTo("Value"));
            Assert.That(request.BString("Command"), Is.EqualTo("help"));
        }

        [Test]
        public void SendUnicastRequest_With_Empty_Channels_Falls_Back_To_Construct_Channel()
        {
            var network = new FakeIgcNetwork();

            var sender = new Script("Sender")
                .OnNetwork(network)
                .Boot();

            var receiver = new Script("Receiver")
                .OnNetwork(network)
                .Boot();

            var service = sender.Mother.GetModule<IntergridMessageService>();
            Request request = service.CreateRequest("ping");
            request.Channels.Clear();

            network.ClearSentMessages();
            service.SendUnicastRequest(receiver.IGC.Me, request, null);

            var sent = network.SentMessages.Single();

            Assert.That(sent.IsBroadcast, Is.False);
            Assert.That(sent.TargetId, Is.EqualTo(receiver.IGC.Me));
            Assert.That(sent.Tag, Is.EqualTo(".construct"));
        }

        [Test]
        public void SendUnicastRequest_Emits_RequestSentEvent_On_Success()
        {
            IntergridMessageService service = Mother.GetModule<IntergridMessageService>();
            Request request = service.CreateRequest("ping");

            service.SendUnicastRequest(123456789, request, null);

            Script.AssertEventEmitted<RequestSentEvent>();
        }

        [Test]
        public void SendRequestFromRoutine_Uses_UnicastId_When_Present()
        {
            var network = new FakeIgcNetwork();
            var sender = new Script("Sender")
                .OnNetwork(network)
                .Boot();
            var receiver = new Script("Receiver")
                .OnNetwork(network)
                .Boot();

            var senderAlmanac = sender.Mother.GetModule<Almanac>();
            var record = senderAlmanac.UpdateOrCreateFromMessage(
                "ReceiverShip",
                originId: 111,
                name: "ReceiverShip",
                position: new Vector3D(0, 0, 0),
                speed: 0f,
                channels: new HashSet<string> { "alpha" },
                isOnConstruct: false);
            record.UnicastId = receiver.IGC.Me;

            var routine = new TerminalRoutine("help");
            var service = sender.Mother.GetModule<IntergridMessageService>();

            network.ClearSentMessages();
            service.SendRequestFromRoutine("ReceiverShip", routine);

            var sent = network.SentMessages.Single();
            Assert.That(sent.IsBroadcast, Is.False);
            Assert.That(sent.TargetId, Is.EqualTo(receiver.IGC.Me));
        }

        [Test]
        public void SendRequestFromRoutine_Does_Not_Send_When_Target_Is_Missing()
        {
            var network = new FakeIgcNetwork();
            var sender = new Script("Sender")
                .OnNetwork(network)
                .Boot();

            var service = sender.Mother.GetModule<IntergridMessageService>();

            service.SendRequestFromRoutine("UnknownGrid", new TerminalRoutine("help"));

            Assert.That(network.SentMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public void Ping_Does_Not_Send_When_Instance_Is_Not_Relay()
        {
            var network = new FakeIgcNetwork();
            var script = new Script("RelayCandidate")
                .OnNetwork(network)
                .Boot();

            var service = script.Mother.GetModule<IntergridMessageService>();
            script.Bus.RegisterRemoteCommands(script.Mother.Id - 1, new List<string> { "help" });

            network.ClearSentMessages();

            service.Ping();

            Assert.That(network.SentMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public void Ping_Sends_Broadcast_When_Instance_Is_Relay()
        {
            var network = new FakeIgcNetwork();

            var script = new Script("Relay")
                .OnNetwork(network)
                .Boot();

            var service = script.Mother.GetModule<IntergridMessageService>();

            network.ClearSentMessages();

            service.Ping();

            var sent = network.SentMessages.Single();
            Assert.That(sent.IsBroadcast, Is.True);
            Assert.That(sent.TargetId, Is.EqualTo(-1));
            Assert.That(sent.Tag, Is.EqualTo("*")); // public channel
        }

        [Test]
        public void ConstructPing_Sends_Sync_Request_On_Construct_Channel()
        {
            var network = new FakeIgcNetwork();
            var script = new Script("ConstructNode")
                .OnNetwork(network)
                .Boot();

            var service = script.Mother.GetModule<IntergridMessageService>();

            network.ClearSentMessages();

            service.ConstructPing();

            var sent = network.SentMessages.Single();
            Assert.That(sent.IsBroadcast, Is.True);
            Assert.That(sent.Tag, Is.EqualTo(".construct"));
            Assert.That($"{sent.Data}".StartsWith("REQUEST::"), Is.True);
        }

        [Test]
        public void HandleIncomingIGCMessage_For_Request_Emits_RequestReceivedEvent()
        {
            IntergridMessageService service = Mother.GetModule<IntergridMessageService>();
            Request request = service.CreateRequest("ping");
            var message = new MyIGCMessage(request.Serialize(), ".construct", 9999);

            service.HandleIncomingIGCMessage(message);

            Script.AssertEventEmitted<RequestReceivedEvent>();
        }

        [Test]
        public void HandleIncomingIGCMessage_Encrypted_Request_On_Configured_Channel_Is_Processed()
        {
            var script = new Script("DecryptNode")
                .WithCustomData(new CustomDataComposer().With("channels", "alpha", "key").Build())
                .Boot();

            var service = script.Mother.GetModule<IntergridMessageService>();
            var request = new Request(
                new Dictionary<string, object> { { "Command", "help" } },
                new Dictionary<string, object>
                {
                    { "Id", "enc-req-1" },
                    { "OriginId", "5001" },
                    { "OriginName", "EncryptedSender" },
                    { "Path", "ping" }
                }
            );

            string encryptedPayload = Security.Encrypt(request.Serialize(), "key");
            var incoming = new MyIGCMessage(encryptedPayload, "alpha", 5001);

            service.HandleIncomingIGCMessage(incoming);

            script.AssertEventEmitted<RequestReceivedEvent>();
        }

        [Test]
        public void HandleIncomingIGCMessage_Encrypted_Request_On_Unknown_Channel_Is_Not_Processed()
        {
            var script = new Script("DecryptNode")
                .WithCustomData(new CustomDataComposer().With("channels", "alpha", "key").Build())
                .Boot();

            var service = script.Mother.GetModule<IntergridMessageService>();
            var request = new Request(
                new Dictionary<string, object> { { "Command", "help" } },
                new Dictionary<string, object>
                {
                    { "Id", "enc-req-2" },
                    { "OriginId", "5002" },
                    { "OriginName", "EncryptedSender" },
                    { "Path", "ping" }
                }
            );

            string encryptedPayload = Security.Encrypt(request.Serialize(), "key");
            var incoming = new MyIGCMessage(encryptedPayload, "beta", 5002);

            service.HandleIncomingIGCMessage(incoming);

            script.AssertEventEmitted<RequestReceivedEvent>(0);
        }

        [Test]
        public void HandleIncomingIGCMessage_Encrypted_Request_On_Construct_Channel_Without_Passcode_Is_Not_Processed()
        {
            var script = new Script("DecryptNode")
                .Boot();

            var service = script.Mother.GetModule<IntergridMessageService>();
            var request = new Request(
                new Dictionary<string, object> { { "Command", "help" } },
                new Dictionary<string, object>
                {
                    { "Id", "enc-req-construct" },
                    { "OriginId", "5003" },
                    { "OriginName", "EncryptedSender" },
                    { "Path", "ping" }
                }
            );

            string encryptedPayload = Security.Encrypt(request.Serialize(), "key");
            var incoming = new MyIGCMessage(encryptedPayload, ".construct", 5003);

            service.HandleIncomingIGCMessage(incoming);

            // .construct has no configured passcode in Channels, so encrypted payload
            // remains unreadable and must not be treated as a request.
            script.AssertEventEmitted<RequestReceivedEvent>(0);
        }

        [Test]
        public void HandleIncomingIGCMessage_ExternalRequest_Updates_Almanac_With_GridId_And_Orientation()
        {
            var service = Mother.GetModule<IntergridMessageService>();
            var almanac = Mother.GetModule<Almanac>();
            almanac.Clear();

            var message = new Request(
                new Dictionary<string, object>(),
                new Dictionary<string, object>
                {
                    { "OriginId", "9001" },
                    { "GridId", "grid-alpha" },
                    { "OriginName", "RemoteScout" },
                    { "X", "10" },
                    { "Y", "20" },
                    { "Z", "30" },
                    { "Speed", "55.5" },
                    { "Fx", "1" },
                    { "Fy", "0" },
                    { "Fz", "0" },
                    { "Ux", "0" },
                    { "Uy", "1" },
                    { "Uz", "0" }
                }
            );
            message.Channels = new HashSet<string> { "alpha" };

            var incoming = new MyIGCMessage(message.Serialize(), "alpha", 9001);

            service.HandleIncomingIGCMessage(incoming);

            var result = almanac.GetRecord("grid-alpha");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("grid-alpha"));
            Assert.That(result.UnicastId, Is.EqualTo(9001));
            Assert.That(result.DisplayName, Is.EqualTo("RemoteScout"));
            Assert.That(result.Position.X, Is.EqualTo(10).Within(0.001));
            Assert.That(result.Position.Y, Is.EqualTo(20).Within(0.001));
            Assert.That(result.Position.Z, Is.EqualTo(30).Within(0.001));
            Assert.That(result.Speed, Is.EqualTo(55.5f).Within(0.001));
            Assert.That(result.Forward.X, Is.EqualTo(1).Within(0.001));
            Assert.That(result.Up.Y, Is.EqualTo(1).Within(0.001));
            Assert.That(result.Channels, Contains.Item("alpha"));
        }

        [Test]
        public void HandleIncomingIGCMessage_ExternalRequest_Falls_Back_To_OriginId_When_GridId_Missing()
        {
            var service = Mother.GetModule<IntergridMessageService>();
            var almanac = Mother.GetModule<Almanac>();
            almanac.Clear();

            var message = new Request(
                new Dictionary<string, object>(),
                new Dictionary<string, object>
                {
                    { "OriginId", "777" },
                    { "GridId", "" },
                    { "OriginName", "NoGridIdSender" },
                    { "X", "1" },
                    { "Y", "2" },
                    { "Z", "3" },
                    { "Speed", "0" }
                }
            );
            message.Channels = new HashSet<string> { "*" };

            var incoming = new MyIGCMessage(message.Serialize(), "alpha", 777);
            service.HandleIncomingIGCMessage(incoming);

            var result = almanac.GetRecord("777");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("777"));
            Assert.That(result.UnicastId, Is.EqualTo(777));
        }

        [Test]
        public void Construct_Sync_Request_Registers_Remote_Commands_And_Sends_Construct_Response()
        {
            var network = new FakeIgcNetwork();

            var sender = new Script("Sender")
                .OnNetwork(network)
                .Boot();

            var receiver = new Script("Receiver")
                .OnNetwork(network)
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("dock", "help")
                    .WithCommand("!undock", "help")
                    .Build())
                .Boot();

            network.ClearSentMessages();

            Request syncRequest = new Request(
                new Dictionary<string, object>
                {
                    { "Commands", "dock,!undock" }
                },
                new Dictionary<string, object>
                {
                    { "Id", "sync-test-1" },
                    { "OriginId", $"{sender.IGC.Me}" },
                    { "OriginName", "Sender" },
                    { "Path", "sync" }
                }
            );

            var incoming = new MyIGCMessage(syncRequest.Serialize(), ".construct", sender.IGC.Me);
            receiver.Mother.GetModule<IntergridMessageService>().HandleIncomingIGCMessage(incoming);

            network.DispatchIgc();

            Assert.That(receiver.Bus.ConstructCommands.ContainsKey(sender.IGC.Me), Is.True);
            Assert.That(receiver.Bus.ConstructCommands[sender.IGC.Me], Contains.Item("dock"));
            Assert.That(receiver.Bus.ImportantConstructCommands.ContainsKey(sender.IGC.Me), Is.True);
            Assert.That(receiver.Bus.ImportantConstructCommands[sender.IGC.Me], Contains.Item("undock"));

            var responseMessages = network.SentMessages
                .Where(m => !m.IsBroadcast && m.SourceId == receiver.IGC.Me && m.TargetId == sender.IGC.Me && m.Tag == ".construct")
                .ToList();

            Assert.That(responseMessages.Count, Is.GreaterThan(0));

            string payload = $"{responseMessages.Last().Data}";
            Assert.That(payload.StartsWith("RESPONSE::"), Is.True);

            Response response = Response.Deserialize(payload);
            Assert.That(response.HString("Status"), Is.EqualTo("200"));
            Assert.That(response.BString("Commands"), Does.Contain("help"));
            Assert.That(response.BString("Commands"), Does.Contain("halt"));
        }

        [Test]
        public void Construct_Communication_Delegates_Command_To_Owning_Construct_Instance()
        {
            var world = new TestWorld();
            var sharedGrid = world.CreateGrid("SharedConstruct");

            var sender = world.CreateScript<CoreTestProgram>(sharedGrid, "Sender")
                .OnNetwork()
                .WithCustomData(BuildAlphaChannelCustomData())
                .Boot();

            var receiver = world.CreateScript<CoreTestProgram>(sharedGrid, "Receiver")
                .OnNetwork()
                .WithCustomData(BuildAlphaChannelCustomData("renameMe", "rename ReceiverRenamed"))
                .Boot();

            world.TickMessages();

            sender.RunTerminal("renameMe");

            Assert.That(
                sender.Bus.GetExecutionCount("renameMe", CommandExecutionOutcome.DelegatedToConstruct),
                Is.EqualTo(1));

            world.TickMessages();

            Assert.That(receiver.Mother.Name, Is.EqualTo("ReceiverRenamed"));
        }

        [Test]
        public void Remote_Communication_Can_Send_And_Executes_On_Remote_Script()
        {
            var world = new TestWorld();

            var sender = world.CreateScript<CoreTestProgram>("Sender")
                .OnNetwork()
                .Boot();

            var receiver = world.CreateScript<CoreTestProgram>("Receiver")
                .OnNetwork()
                .Boot();

            var sentBefore = world.SentMessages.Count;

            sender.RunTerminal("@Receiver help");

            Assert.That(
                sender.Bus.GetExecutionCount(string.Empty, CommandExecutionOutcome.RemoteRoutineSent),
                Is.EqualTo(1));

            Assert.That(
                world.SentMessages
                    .Skip(sentBefore)
                    .Any(m => !m.IsBroadcast && m.TargetId == receiver.IGC.Me),
                Is.True);

            world.TickMessages();

            Assert.That(receiver.Bus.GetExecutionCount("help"), Is.EqualTo(1));
        }

        [Test]
        public void Remote_Communication_Can_Broadcast_To_All_And_Executes_On_All_Remotes()
        {
            var world = new TestWorld();

            var sender = world.CreateScript<CoreTestProgram>("Sender")
                .OnNetwork()
                .Boot();

            var receiverA = world.CreateScript<CoreTestProgram>("ReceiverA")
                .OnNetwork()
                .Boot();

            var receiverB = world.CreateScript<CoreTestProgram>("ReceiverB")
                .OnNetwork()
                .Boot();

            var sentBefore = world.SentMessages.Count;

            sender.RunTerminal("@* help");

            Assert.That(
                world.SentMessages.Skip(sentBefore).Count(m => !m.IsBroadcast),
                Is.GreaterThanOrEqualTo(2)
            );

            world.TickMessages();

            Assert.That(receiverA.Bus.GetExecutionCount("help"), Is.EqualTo(1));
            Assert.That(receiverB.Bus.GetExecutionCount("help"), Is.EqualTo(1));
        }
    }
}