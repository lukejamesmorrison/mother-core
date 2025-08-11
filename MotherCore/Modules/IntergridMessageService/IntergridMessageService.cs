using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Channels;
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
        /// The Clock core module.
        /// </summary>
        Clock Clock;

        /// <summary>
        /// The Log core module.
        /// </summary>
        Log Log;

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
        /// List of BroadcastListeners for receiving broadcast messages.
        /// </summary>
        readonly List<IMyBroadcastListener> BroadcastListeners = new List<IMyBroadcastListener>();

        /// <summary>
        /// The channels that are used for intergrid communication 
        /// as name=>passcode pairs.
        /// </summary>
        public Dictionary<string, string> Channels = new Dictionary<string, string>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public IntergridMessageService(Mother mother) : base(mother)
        {
            Router = new Router();
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
            Almanac = Mother.GetModule<Almanac>();
            EventBus = Mother.GetModule<EventBus>();

            // Register commands
            Mother.RegisterCommand(new PingCommand(Mother));

            // Load channels
            LoadChannels();
            RegisterIGCListeners();

            // ROUTES
            Router.AddRoute("ping", (request) => CreateResponse(request, Response.ResponseStatusCodes.OK));

            // Ping local, then schedule recurring ping every 2 seconds.
            //PingLocal();
            Clock.Schedule(Ping, 2);
        }

        /// <summary>
        /// Load channels from the configuration. Channels are defined in the "channels" 
        /// section of the programmable block's custom data. The channel name acts as 
        /// the key, and the passcode as its value. If a channel is not defined, 
        /// it will not be available for communication.
        /// </summary>
        void LoadChannels()
        {
            var config = Mother.GetModule<Configuration>();

            // channels are keys
            var keys = new List<MyIniKey>();

            config.Raw.GetKeys("channels", keys);

            keys.ForEach(key =>
            {
                var value = config.Raw.Get(key.Section, key.Name);
                Channels[key.Name] = $"{value}";
            });

        }

        /// <summary>
        /// Register the IGC listeners - unicast and broadcast.
        /// 
        /// We set a message callback for each listener to ensure a response 
        /// is sent from the foreign programmable blocks.
        /// </summary>
        void RegisterIGCListeners()
        {
            // Register a single unicast listener
            UnicastListener = Mother.IGC.UnicastListener;
            UnicastListener.SetMessageCallback();

            // Register a broadcast listener for each channel
            foreach (var channel in Channels)
            {
                IMyBroadcastListener BroadcastListener = Mother.IGC.RegisterBroadcastListener(channel.Key);
                BroadcastListener.SetMessageCallback();
                BroadcastListeners.Add(BroadcastListener);
            }
        }

        /// <summary>
        /// Handle all incoming IGC messages.
        /// </summary>
        public void HandleIncomingIGCMessages()
        {
            // unicast message
            while (UnicastListener?.HasPendingMessage == true)
                HandleIncomingIGCMessage(UnicastListener.AcceptMessage());

            // broadcast message
            BroadcastListeners.ForEach(listener =>
            {
                while (listener?.HasPendingMessage == true)
                    HandleIncomingIGCMessage(listener.AcceptMessage());
            });

            // clear active requests after handling incoming messages
            activeRequests.Clear();
        }

        /// <summary>
        /// Decrypt an incoming IGC message. The message is expected to be encrypted 
        /// with a passcode specific to the communication channel. If the passcode
        /// is not set, the message is returned as is representing an 
        /// unsecure message.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        string DecryptIncomingMessage(MyIGCMessage message)
        {
            string messageData = $"{message.Data}";
            string channel = message.Tag;

            // get channel from available channels
            string passcode = Channels.ContainsKey(channel) ? Channels[channel] : "";

            // Decrypt message if it is encrypted
            if(passcode == "") 
                return messageData;

            else 
                return Security.Decrypt(messageData, passcode);
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
            messageData = Security.IsEncrypted(messageData) 
                ? DecryptIncomingMessage(message) 
                : messageData;

            // Handle an incoming Request
            if (messageData.StartsWith("REQUEST::"))
            {
                Request deserializedRequest = Request.Deserialize(messageData);

                if (deserializedRequest != null)
                {
                    deserializedRequest.Channels.Add(message.Tag);
                    UpdateOrCreateAlmanacRecordFromIncomingRequest(deserializedRequest);
                    HandleIncomingRequest(deserializedRequest);
                }
            
                else Log.Error(Messages.MessageDeserializationFailed);
            }

            // Or handle an incoming Response
            else if (messageData.StartsWith("RESPONSE::"))
            {
                Response deserializedResponse = Response.Deserialize(messageData);

                if (deserializedResponse != null)
                {
                    deserializedResponse.Channels.Add(message.Tag);

                    UpdateOrCreateAlmanacRecordFromIncomingRequest(deserializedResponse);

                    HandleIncomingResponse(deserializedResponse);
                }

                else Log.Error(Messages.MessageDeserializationFailed);
            }
        }

        /// <summary>
        /// Update or create an AlmanacRecord from an incoming Request.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        AlmanacRecord UpdateOrCreateAlmanacRecordFromIncomingRequest(IntergridMessageObject message)
        {
            long originId = message.HLong("OriginId");
            string name = message.HString("OriginName");
            float x = message.HFloat("x");
            float y = message.HFloat("y");
            float z = message.HFloat("z");
            float speed = message.HFloat("speed");

            AlmanacRecord record;
            AlmanacRecord existingRecord = Almanac.GetRecord($"{originId}");

            // if record exists, update it with message data
            if (existingRecord != null)
            {
                existingRecord.Position = new Vector3D(x, y, z);
                existingRecord.Speed = speed;

                // add message channels to existing record channels
                existingRecord.Channels.UnionWith(message.Channels);

                existingRecord.UpdatedAt = DateTime.Now;

                record = existingRecord;
            }


            // if record does not exist, create a new one
            else
            {
                record = new AlmanacRecord(
                    $"{originId}",
                    "grid",
                    new Vector3D(x, y, z),
                    speed
                );

                // set IFF code
                if (OriginIsLocal(originId))
                    record.IFFCode = AlmanacRecord.TransponderCode.Local;

                // add channel
                record.Channels = message.Channels;

                // set nickname
                record.AddNickname(name);
            }

            // if the message channel is not the public channel "*", then we will set to friendly
            if (!message.Channels.Contains("*") && record.IFFCode == AlmanacRecord.TransponderCode.Neutral)
                record.IFFCode = AlmanacRecord.TransponderCode.Friendly;

            Mother.GetModule<Almanac>().AddRecord(record);

            return record;
        }

        /// <summary>
        /// Handle and incoming Request.
        /// </summary>
        /// <param name="request"></param>
        void HandleIncomingRequest(Request request)
        {
            if (request == null) return;

            Mother.GetModule<EventBus>().Emit<RequestReceivedEvent>();

            // Get the Response from a Route
            Response response = Router.HandleRoute(
                request.HString("Path"), 
                request
            );
            
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
                Log.Error($"Response missing RespondingToId header: {Serializer.SerializeDictionary(response.Header)}");
        }

        /// <summary>
        /// Send a unicast message to a specific grid.
        /// </summary>
        /// <param name="TargetId"></param>
        /// <param name="message"></param>
        /// <param name="onResponse"></param>
        public void SendUnicastRequest(long TargetId, IntergridMessageObject message, Action<IntergridMessageObject> onResponse)
        {
            // Register the message callback
            activeRequests[$"{message.Header["Id"]}"] = onResponse;

            bool success = false;

            // sort message.Channels so that public is prioritized last
            var orderedChannels = message.Channels.OrderBy(c => c == "*").ToHashSet();

            foreach (string channel in orderedChannels)
            {
                string passcode = Channels.ContainsKey(channel) ? Channels[channel] : "";
                string outgoingMessage = Security.Encrypt(message.Serialize(), passcode);

                success = Mother.IGC.SendUnicastMessage(TargetId, channel, outgoingMessage);

                if (success) break;
            }

            // Send the message via unicast
            //string outgoingMessage = UseEncryption ?
            //    Mother.GetModule<Security>().Encrypt(message.Serialize()) :
            //    message.Serialize();

            //bool success = Mother.IGC.SendUnicastMessage(TargetId, "default", outgoingMessage);

            if (success)
                Mother.GetModule<EventBus>().Emit<RequestSentEvent>();

            else
                Mother.GetModule<EventBus>().Emit<RequestFailedEvent>();
        }

        /// <summary>
        /// Send an open broadcast message to all grids.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="onResponse"></param>
        public void SendOpenBroadcastRequest(Request request, Action<IntergridMessageObject> onResponse)
        {
            // Register the message callback
            activeRequests[request.Id] = onResponse;

            // sort message.Channels so that public is last
            foreach (var channel in Channels)
            {
                string passcode = Channels.ContainsKey(channel.Key) ? Channels[channel.Key] : "";
                string outgoingMessage = Security.Encrypt(request.Serialize(), passcode);

                Mother.IGC.SendBroadcastMessage(channel.Key, outgoingMessage);
            }

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
            Almanac.GetRecordsByType("grid")
                .ForEach(grid => SendRequestFromRoutine(grid.Id, routine));
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
        /// Get a standard header for all messages sent via the IntergridMessageService. This 
        /// includes common identifiers as well as positional and environmental data.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, object> GetStandardHeader()
        {
            Vector3D currentPosition = Mother.CubeGrid.GetPosition();

            return new Dictionary<string, object>
            {
                // Identifiers
                { "OriginId", $"{Mother.Id}" },
                { "OriginName", Mother.CubeGrid.CustomName },

                // Position
                { "px", $"{currentPosition}" },
                { "x", $"{currentPosition.X}" },
                { "y", $"{currentPosition.Y}" },
                { "z", $"{currentPosition.Z}" },

                // Environment
                { "SafeRadius", $"{Mother.SafeZone.Radius}" },
                { "gravity", $"{Mother.GetGravity()}"   },
                { "speed", $"{Mother.RemoteControl.GetShipSpeed()}" }
            };
        }

        /// <summary>
        /// Create a Request object to a specified path with a standard Header. Optional 
        /// customBody and customHeader parameters may be included for further customization of 
        /// the message payload.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="customBody"></param>
        /// <param name="customHeader"></param>
        /// <returns></returns>
        public Request CreateRequest(
            string path, 
            Dictionary<string, object> customBody = null, 
            Dictionary<string, object> customHeader = null
        )
        {
            Vector3D currentPosition = Mother.CubeGrid.GetPosition();

            Dictionary<string, object> header = GetStandardHeader();
            header["Path"] = path; // add the path to the customHeader

            Dictionary<string, object> body = new Dictionary<string, object>();

            // merge headers with defaults
            if (customHeader != null)
                foreach (KeyValuePair<string, object> entry in customHeader)
                    header[entry.Key] = entry.Value;

            // merge customBody with defaults
            if (customBody != null)
                foreach (KeyValuePair<string, object> entry in customBody)
                    body[entry.Key] = entry.Value;

            return new Request(body, header);
        }

        /// <summary>
        /// Create a Response object for a received Request with a status code and 
        /// standard customHeader. Option customBody and customHeader parameters may be included. 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="code"></param>
        /// <param name="customBody"></param>
        /// <param name="customHeader"></param>
        /// <returns></returns>
        public Response CreateResponse(
            Request request, 
            Response.ResponseStatusCodes code, 
            Dictionary<string, object> customBody = null, 
            Dictionary<string, object> customHeader = null
        )
        {
            Dictionary<string, object> standardHeader = GetStandardHeader();
            Dictionary<string, object> responseHeader = new Dictionary<string, object>()
            {
                { "status", $"{Response.GetResponseCodeValue(code)}" },
                { "TargetId", request.Header["OriginId"] },
                { "TargetName", request.Header["OriginName"] },
                { "RespondingToId", request.Header["Id"] },
            };
            Dictionary<string, object> responseBody = new Dictionary<string, object>();

            // merge default headers
            foreach (KeyValuePair<string, object> entry in standardHeader)
                responseHeader[entry.Key] = entry.Value;

            //Vector3D currentPosition = Mother.CubeGrid.GetPosition();

            //Dictionary<string, object> responseBody = new Dictionary<string, object>();
            //Dictionary<string, object> responseHeader = new Dictionary<string, object>()
            //{
            //    { "status", $"{Response.GetResponseCodeValue(code)}" },
            //    { "OriginId", $"{Mother.Id}" },
            //    { "OriginName", Mother.Name },
            //    { "TargetId", request.Header["OriginId"] },
            //    { "TargetName", request.Header["OriginName"] },
            //    { "RespondingToId", request.Header["Id"] },
            //    { "x", $"{currentPosition.X}" },
            //    { "y", $"{currentPosition.Y}" },
            //    { "z", $"{currentPosition.Z}" },
            //    { "SafeRadius", $"{Mother.SafeZone.Radius}" },
            //    { "gravity", $"{Mother.GetGravity()}"   },
            //    { "speed", $"{Mother.RemoteControl.GetShipSpeed()}" }
            //};

            // merge customHeader with defaults
            if (customHeader != null)
                foreach (KeyValuePair<string, object> entry in customHeader)
                    responseHeader[entry.Key] = entry.Value;

            // merge customBody with defaults
            if (customBody != null)
                foreach (KeyValuePair<string, object> entry in customBody)
                    responseBody[entry.Key] = entry.Value;

            return new Response(responseBody, responseHeader);
        }

        /// <summary>
        /// Send a ping message to all grids running Mother.
        /// </summary>
        public void Ping()
        {
            var channels = Channels.Keys.ToList();

            Request request = CreateRequest("ping");
            request.Channels = new HashSet<string>(channels);

            SendOpenBroadcastRequest(request, null);
        }

        /// <summary>
        /// Send a ping message to all programmable blocks on the local grid. 
        /// This is used during boot to identify cooperative scripts.
        /// </summary>
        public void LocalPing()
        {
            // send lean message only with entity id and mode=master/extension/both on local channel to conduct local handshake.
            string channel = "local";

            Request request = new Request(
                null,
                new Dictionary<string, object>
                {
                    { "mode", "both" },
                    { "OriginId", $"{Mother.Id}" },
                    { "OriginName", Mother.Name },
                }
            );

            request.Channels.Add( channel );

            SendOpenBroadcastRequest(request, null);
        }

        /// <summary>
        /// Check if the origin of a message is a programmable block 
        /// on the local grid.
        /// </summary>
        /// <param name="originId"></param>
        /// <returns></returns>
        bool OriginIsLocal(long originId)
        {
            return Mother.GetModule<BlockCatalogue>()
                .GetBlocks<IMyProgrammableBlock>()
                .Any(pb => pb.EntityId == originId);
        }
    }
}
