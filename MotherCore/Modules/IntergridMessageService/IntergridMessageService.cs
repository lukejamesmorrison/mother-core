using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
//using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRageMath;

namespace IngameScript
{

    /// <summary>
    /// The IntergridMessageService manages communications between grids running Mother. It 
    /// sends Requests and expects a Response in return. It is able to send messages to 
    /// all grids via a broadcast or to a specific grid via unicast. Messages may 
    /// be encrypted to enable private messaging and command execution.
    /// </summary>
    public class IntergridMessageService : BaseCoreModule
    {
        /// <summary>
        /// Messages that may be printed to the console for the IntergridMessageService.
        /// </summary>
        class Messages
        {
            public const string MessageDeserializationFailed = "Cannot de-serialize message.";
            public const string NoActiveRequest = "No active request found for RespondingToId: {0}";
        }

        /// <summary>
        /// The Mother instance.
        /// </summary>
        //readonly Mother Mother;

        /// <summary>
        /// The Clock core module.
        /// </summary>
        Clock Clock;

        /// <summary>
        /// The Log core module.
        /// </summary>
        Log Log;

        /// <summary>
        /// The Security core module.
        /// </summary>
        Security Security;

        /// <summary>
        /// The Almanac core module.
        /// </summary>
        Almanac Almanac;

        /// <summary>
        /// The EventBus core module.
        /// </summary>
        EventBus EventBus;

        /// <summary>
        /// The Router instance. It is used to route incoming requests 
        /// to the appropriate controller.
        /// </summary>
        public readonly Router Router;

        /// <summary>
        /// Active Requests that are awaiting a Response.
        /// </summary>
        public Dictionary<string, Action<IntergridMessageObject>> activeRequests = new Dictionary<string, Action<IntergridMessageObject>>();

        /// <summary>
        /// The UnicastListener instance for receiving unicast messages.
        /// </summary>
        IMyUnicastListener UnicastListener;

        /// <summary>
        /// The BroadcastListener instance for receiving broadcast messages.
        /// </summary>
        IMyBroadcastListener BroadcastListener;

        /// <summary>
        /// Is the IntergridMessageService using encryption.
        /// </summary>
        bool UseEncryption;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public IntergridMessageService(Mother mother) : base(mother)
        {
            //Mother = mother;
            Router = new Router();

            RegisterIGCListeners();
        }

        /// <summary>
        /// Boot the module. Here we can reference important modules, register 
        /// commands, add communication routes, observe blocks for state 
        /// changes, and schedule activities for execution later.
        /// </summary>
        public override void Boot()
        {
            Clock = Mother.GetModule<Clock>();
            Log = Mother.GetModule<Log>();
            Security = Mother.GetModule<Security>();
            Almanac = Mother.GetModule<Almanac>();
            EventBus = Mother.GetModule<EventBus>();

            UseEncryption = Security.USE_ENCRYPTION;

            // Register commands
            Mother.RegisterCommand(new PingCommand(Mother));

            // ROUTES
            Router.AddRoute("ping", (request) => CreatePingResponse(request));

            Clock.Schedule(Ping, 2);
        }

        /// <summary>
        /// Register the IGC listeners - unicast and broadcast.
        /// </summary>
        void RegisterIGCListeners()
        {
            //List<string> channels = new List<string>()
            //{
            //    "default"
            //};

            //channels.ForEach(channel =>
            //{
            //    if (Mother.IGC.UnicastListener == null)
            //        Mother.IGC.UnicastListener = Mother.IGC.RegisterUnicastListener(channel);
            //    if (Mother.IGC.BroadcastListener == null)
            //        Mother.IGC.BroadcastListener = Mother.IGC.RegisterBroadcastListener(channel);
            //});

            UnicastListener = Mother.IGC.UnicastListener;
            BroadcastListener = Mother.IGC.RegisterBroadcastListener("default");

            UnicastListener.SetMessageCallback("default");
            BroadcastListener.SetMessageCallback("default");
        }

        /// <summary>
        /// Handle all incoming IGC messages.
        /// </summary>
        public void HandleIncomingIGCMessages()
        {
            // unicast
            if (UnicastListener.HasPendingMessage)
                while (UnicastListener.HasPendingMessage)
                    HandleIncomingIGCMessage(UnicastListener.AcceptMessage());

            // broadcast
            if (BroadcastListener.HasPendingMessage)
                while (BroadcastListener.HasPendingMessage)
                    HandleIncomingIGCMessage(BroadcastListener.AcceptMessage());

            // clear active requests after handling incoming messages
            activeRequests.Clear();
        }

        /// <summary>
        /// Handle individual incoming IGC message for decryption and action based 
        /// on message type: Request or Response.
        /// </summary>
        /// <param name="message"></param>
        public void HandleIncomingIGCMessage(MyIGCMessage message)
        {
            string messageData = $"{message.Data}";

            // Decrypt message if it is encrypted
            messageData = Security.IsEncrypted(messageData) ?
                Security.Decrypt(messageData) :
                messageData;

            // Handle an incoming Request
            if (messageData.StartsWith("REQUEST::"))
            {
                Request deserializedRequest = Request.Deserialize(messageData);

                if (deserializedRequest != null)
                    HandleIncomingRequest(deserializedRequest);
                else
                    Log.Error(Messages.MessageDeserializationFailed);
            }

            // Or handle an incoming Response
            else if (messageData.StartsWith("RESPONSE::"))
            {
                Response deserializedResponse = Response.Deserialize(messageData);

                if (deserializedResponse != null)
                    HandleIncomingResponse(deserializedResponse);
                else
                    Log.Error(Messages.MessageDeserializationFailed);
            }
        }

        /// <summary>
        /// Handle and incoming Request.
        /// </summary>
        /// <param name="request"></param>
        void HandleIncomingRequest(Request request)
        {
            if (request == null) return;

            //EventBus.Emit<RequestReceivedEvent>();
            Mother.GetModule<EventBus>().Emit<RequestReceivedEvent>();

            string id = request.HString("OriginId");
            string name = request.HString("OriginName");
            float x = request.HFloat("x");
            float y = request.HFloat("y");
            float z = request.HFloat("z");

            // attempt to update almanac with request data
            AlmanacRecord record = new AlmanacRecord(
                $"{id}",
                "grid",
                new Vector3D(x, y, z),
                0f
            );

            record.AddNickname(name);


            Mother.GetModule<Almanac>().AddRecord(record);

            Response response = Router.HandleRoute(request.HString("Path"), request);
            
            if(response != null)
                SendUnicastRequest(response.HLong("TargetId"), response, null);
        }


        /// <summary>
        /// Handle incoming Response. We expect to receive a Response 
        /// related to an earlier outgoing Request.
        /// </summary>
        /// <param name="response"></param>
        void HandleIncomingResponse(Response response)
        {
            if (response == null) return;

            if (response.Header.ContainsKey("RespondingToId"))
            {
                string respondingToId = response.HString("RespondingToId");

                if (activeRequests.ContainsKey(respondingToId))
                    activeRequests[respondingToId]?.Invoke(response);
            }
            else
            {
                Log.Error($"Response missing RespondingToId header: {Serializer.SerializeDictionary(response.Header)}");
            }
        }

        /// <summary>
        /// Send a unicast request to a specific grid.
        /// </summary>
        /// <param name="TargetId"></param>
        /// <param name="message"></param>
        /// <param name="onResponse"></param>
        public void SendUnicastRequest(long TargetId, IntergridMessageObject message, Action<IntergridMessageObject> onResponse)
        {
            // Register the message callback
            activeRequests[$"{message.Header["Id"]}"] = onResponse;

            // Send the message via unicast
            string outgoingMessage = UseEncryption ?
                Mother.GetModule<Security>().Encrypt(message.Serialize()) :
                message.Serialize();

            bool success = Mother.IGC.SendUnicastMessage(TargetId, "default", outgoingMessage);

            if (success)
                Mother.GetModule<EventBus>().Emit<RequestSentEvent>();
            else
                Mother.GetModule<EventBus>().Emit<RequestFailedEvent>();

        }

        /// <summary>
        /// Send an open broadcast request to all grids.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="onResponse"></param>
        public void SendOpenBroadcastRequest(Request request, Action<IntergridMessageObject> onResponse)
        {
            // Register the message callback
            activeRequests[request.Id] = onResponse;

            string outgoingMessage = UseEncryption ?
                Security.Encrypt(request.Serialize()) :
                request.Serialize();

            Mother.IGC.SendBroadcastMessage("default", outgoingMessage);

            EventBus.Emit<RequestSentEvent>();
        }

        /// <summary>
        /// Send a Request from a Terminal Routine to a specific grid. 
        /// This is done using the remote command syntax.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="routine"></param>
        public void SendRequestFromRoutine(string target, TerminalRoutine routine)
        {
            AlmanacRecord record = Almanac.GetRecord(target);

            if (record != null && record.EntityType == AlmanacRecord.EntityTypes["grid"])
            {
                Request request = BuildCommandRequest(routine.UnpackedRoutineString)
                    .To(record);

                SendUnicastRequest(record.GetLongId(), request, null);
            }
        }
        
        /// <summary>
        /// Send a Request to all grids from a Terminal Routine.
        /// This is done using the remote command syntax.
        /// </summary>
        /// <param name="routine"></param>
        public void SendRequestToAllFromRoutine(TerminalRoutine routine)
        {
            Almanac.GetRecordsByType("grid").ForEach(grid =>
            {
                SendRequestFromRoutine(grid.Id, routine);
            });
        }

        /// <summary>
        /// Build a command Request for a remote terminal command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        Request BuildCommandRequest(string command)
        {
            return CreateRequest("command", 
                new Dictionary<string, object>
                {
                    { "Command", command }
                }
            );
        }

        /// <summary>
        /// Create a Request object to a specified path with a standard Header. Optional 
        /// body and header parameters may be included for further customization of 
        /// the message payload.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="body"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        public Request CreateRequest(string path, Dictionary<string, object> body = null, Dictionary<string, object> header = null)
        {
            Vector3D currentPosition = Mother.CubeGrid.GetPosition();

            /// THIS HAS VERY SIMILAR FIELDS TO THE RESPONSE DEFINITION. MAYBE WE SHOULD ABSTRACT TO "STANDARD HEADER" \+ TYPE HEADER"
            Dictionary<string, object> requestHeader = new Dictionary<string, object>
            {
                { "OriginId", $"{Mother.Id}" },
                { "OriginName", Mother.CubeGrid.CustomName },
                { "Path", path },
                { "x", $"{currentPosition.X}" },
                { "y", $"{currentPosition.Y}" },
                { "z", $"{currentPosition.Z}" },
                { "SafeRadius", $"{Mother.SafeZone.Radius}" },
                { "gravity", $"{Mother.GetGravity()}"   },
                { "speed", $"{Mother.RemoteControl.GetShipSpeed()}" }
            };

            Dictionary<string, object> requestBody = new Dictionary<string, object>();

            // merge headers with defaults
            if (header != null)
                foreach (KeyValuePair<string, object> entry in header)
                {
                    requestHeader[entry.Key] = entry.Value;
                }

            // merge body with defaults
            if (body != null)
                foreach (KeyValuePair<string, object> entry in body)
                {
                    requestBody[entry.Key] = entry.Value;
                }

            return new Request(requestBody, requestHeader);
        }

        /// <summary>
        /// Create a Response object for a received Request with a status code and 
        /// standard header. Option body and header parameters may be included. 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="code"></param>
        /// <param name="body"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        public Response CreateResponse(Request request, Response.ResponseStatusCodes code, Dictionary<string, object> body = null, Dictionary<string, object> header = null)
        {
            Vector3D currentPosition = Mother.CubeGrid.GetPosition();

            Dictionary<string, object> responseBody = new Dictionary<string, object>();
            Dictionary<string, object> responseHeader = new Dictionary<string, object>()
            {
                { "status", $"{Response.GetResponseCodeValue(code)}" },
                { "OriginId", $"{Mother.Id}" },
                { "OriginName", Mother.Name },
                { "TargetId", request.Header["OriginId"] },
                { "TargetName", request.Header["OriginName"] },
                { "RespondingToId", request.Header["Id"] },
                { "x", $"{currentPosition.X}" },
                { "y", $"{currentPosition.Y}" },
                { "z", $"{currentPosition.Z}" },
                { "SafeRadius", $"{Mother.SafeZone.Radius}" },
                { "gravity", $"{Mother.GetGravity()}"   },
                { "speed", $"{Mother.RemoteControl.GetShipSpeed()}" }
            };

            // merge header with defaults
            if (header != null)
                foreach (KeyValuePair<string, object> entry in header)
                    responseHeader[entry.Key] = entry.Value;

            // merge body with defaults
            if (body != null)
                foreach (KeyValuePair<string, object> entry in body)
                    responseBody[entry.Key] = entry.Value;

            return new Response(responseBody, responseHeader);
        }

        /// <summary>
        /// Send a ping request to all grids running Mother.
        /// </summary>
        public void Ping()
        {
            SendOpenBroadcastRequest(CreateRequest("ping"), OnPingResponse);
        }

        /// <summary>
        /// Create a ping response to a ping request. This is used to to share the 
        /// grid's position details with other grids.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public Response CreatePingResponse(Request request)
        {
            Vector3D currentPosition = Mother.CubeGrid.GetPosition();

            Dictionary<string, object> responseBody = new Dictionary<string, object>()
            {
                { "x", $"{currentPosition.X}" },
                { "y", $"{currentPosition.Y}" },
                { "z", $"{currentPosition.Z}" },
                //{ "speed", $"{speed}" },
                { "Name", Mother.Name },
                { "Id", $"{Mother.Id}" },
                { "SafeRadius", $"{Mother.SafeZone.Radius}" }
            };

            return CreateResponse(request, Response.ResponseStatusCodes.OK, responseBody);
        }

        /// <summary>
        /// Handle a ping response.
        /// </summary>
        /// <param name="response"></param>
        void OnPingResponse(IntergridMessageObject response)
        {
            AlmanacRecord almanacRecord = new AlmanacRecord(
                response.BString("Id"),
                "grid",
                new Vector3D(
                    response.BDouble("x"),
                    response.BDouble("y"),
                    response.BDouble("z")
                )
            )
            {
                SafeRadius = response.HDouble("SafeRadius"),
            };

            almanacRecord.AddNickname(response.HString("OriginName"));

            // TODO: get this working to support IFF build out
            almanacRecord.IsLocal = Mother
                .GetModule<BlockCatalogue>()
                .GetBlocks<IMyProgrammableBlock>()
                .Any(pb =>pb.EntityId == response.HLong("OriginId"));

            if (!almanacRecord.IsLocal)
                Almanac.AddRecord(almanacRecord);
        }
    }
}
