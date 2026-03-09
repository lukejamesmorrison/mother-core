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
        /// The tag for this construct's channel.
        /// </summary>
        const string CONSTRUCT_CHANNEL_TAG = ".construct";

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
            Router.RegisterRoute("ping", request => CreateResponse(request, Response.ResponseStatusCodes.OK));
            Router.RegisterRoute("sync", request => HandleSyncRequest(request));

            // Ping scripts on the construct to perform handshake after boot completes
            Clock.QueueForLater(() => ConstructPing(), 0.5);
            
            // Re-sync scripts on the construct periodically to catch late-booting scripts
            Clock.Schedule(ConstructPing, 5);

            // Ping remote grids periodically
            Clock.Schedule(Ping, 2);
        }

        /// <summary>
        /// Handles a sync request from another Mother Core instance on the construct.
        /// Returns this instance's commands in the response body.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Response HandleSyncRequest(Request request)
        {
            SyncConstructCommands(request);

            // Return our commands
            var selfCommands = Mother.GetModule<CommandBus>().GetSelfCommandNames();
            
            var response = CreateResponse(
                request,
                Response.ResponseStatusCodes.OK,
                new Dictionary<string, object>
                {
                    { "Commands", string.Join(",", selfCommands) }
                }
            );
            
            // Ensure response uses the construct channel
            response.Channels.Add(CONSTRUCT_CHANNEL_TAG);
            
            return response;
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

            // register broadcast listener for construct channel
            IMyBroadcastListener ConstructBroadcastListener = Mother.IGC.RegisterBroadcastListener(CONSTRUCT_CHANNEL_TAG);
            ConstructBroadcastListener.SetMessageCallback();
            BroadcastListeners.Add(ConstructBroadcastListener);

            // Register a broadcast listener for each external channel
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

            string passcode = GetChannelPasscode(channel);

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
                ProcessIncomingMessage(
                    Request.Deserialize(messageData),
                    message.Tag,
                    msg => HandleIncomingRequest((Request)msg)
                );
            }

            // Or handle an incoming Response
            else if (messageData.StartsWith("RESPONSE::"))
            {
                ProcessIncomingMessage(
                    Response.Deserialize(messageData),
                    message.Tag,
                    msg => HandleIncomingResponse((Response)msg)
                );
            }
        }

        /// <summary>
        /// Process a deserialized incoming message by adding the channel tag,
        /// updating the almanac, and invoking the appropriate handler.
        /// </summary>
        /// <param name="deserialized"></param>
        /// <param name="channelTag"></param>
        /// <param name="handler"></param>
        void ProcessIncomingMessage(IntergridMessageObject deserialized, string channelTag, Action<IntergridMessageObject> handler)
        {
            if (deserialized != null)
            {
                deserialized.Channels.Add(channelTag);
                UpdateOrCreateAlmanacRecordFromIncomingRequest(deserialized);
                handler(deserialized);
            }
            else
            {
                Log.Error(Messages.MessageDeserializationFailed);
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
                if (OriginIsOnConstruct(originId))
                    record.IFFCode = AlmanacRecord.TransponderCode.Construct;

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
            
            // If no channels specified, try .construct for messages on this construct
            if (orderedChannels.Count == 0)
                orderedChannels.Add(CONSTRUCT_CHANNEL_TAG);

            foreach (string channel in orderedChannels)
            {
                string outgoingMessage = Security.Encrypt(
                    message.Serialize(),
                    GetChannelPasscode(channel)
                );

                success = Mother.IGC.SendUnicastMessage(TargetId, channel, outgoingMessage);

                if (success) break;
            }

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
                string outgoingMessage = Security.Encrypt(
                    request.Serialize(),
                    GetChannelPasscode(channel.Key)
                );

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
                { "gravity", $"{Mother?.GetGravity() ?? Vector3D.Zero}"   },
                { "speed", $"{Mother?.RemoteControl?.GetShipSpeed()}" }
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

            MergeDictionary(header, customHeader);
            MergeDictionary(body, customBody);

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

            MergeDictionary(responseHeader, standardHeader);
            MergeDictionary(responseHeader, customHeader);
            MergeDictionary(responseBody, customBody);

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
        /// Send a ping message to all programmable blocks on the construct. 
        /// This is used during boot to identify cooperative scripts.
        /// </summary>
        public void ConstructPing()
        {
            var constructCommands = Mother.GetModule<CommandBus>().GetSelfCommandNames();

            Request request = CreateRequest(
                "sync",
                new Dictionary<string, object>
                {
                    { "Commands", string.Join(",", constructCommands) }
                },
                new Dictionary<string, object>
                {
                    { "OriginName", Mother.Name }
                }
            );

            SendConstructBroadcastRequest(request, response => OnSyncResponse(response));
        }

        /// <summary>
        /// Handle sync response from other Mother Core instances on this construct.
        /// </summary>
        /// <param name="response"></param>
        void OnSyncResponse(IntergridMessageObject response)
        {
            SyncConstructCommands(response);
        }

        /// <summary>
        /// Send a command to another Mother Core instances on this construct.
        /// </summary>
        /// <param name="targetId">The EntityId of the target script.</param>
        /// <param name="command">The command to execute.</param>
        public void SendConstructCommand(long targetId, string command)
        {
            Request request = CreateRequest(
                "localcmd",
                new Dictionary<string, object>
                {
                    { "Command", command }
                }
            );

            Mother.IGC.SendUnicastMessage(targetId, CONSTRUCT_CHANNEL_TAG, request.Serialize());
            Mother.Print($"> @local {command}");
        }

        /// <summary>
        /// Send a broadcast to programmable blocks on this construct.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="onResponse"></param>
        public void SendConstructBroadcastRequest(Request request, Action<IntergridMessageObject> onResponse)
        {
            activeRequests[request.Id] = onResponse;
            Mother.IGC.SendBroadcastMessage(CONSTRUCT_CHANNEL_TAG, request.Serialize(), TransmissionDistance.CurrentConstruct);
        }

        /// <summary>
        /// Get the passcode for a given channel name.
        /// Returns an empty string if the channel is not found.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        string GetChannelPasscode(string channel)
        {
            return Channels.ContainsKey(channel) ? Channels[channel] : "";
        }

        /// <summary>
        /// Merge entries from the source dictionary into the target dictionary.
        /// Existing keys in the target will be overwritten.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        void MergeDictionary(Dictionary<string, object> target, Dictionary<string, object> source)
        {
            if (source == null) return;

            foreach (KeyValuePair<string, object> entry in source)
                target[entry.Key] = entry.Value;
        }

        /// <summary>
        /// Parse and register remote commands from an incoming sync 
        /// message (request or response).
        /// </summary>
        /// <param name="message"></param>
        void SyncConstructCommands(IntergridMessageObject message)
        {
            long originId = message.HLong("OriginId");
            string commandsStr = message.BString("Commands");
            
            if (!string.IsNullOrEmpty(commandsStr) && originId != Mother.Id)
            {
                var commands = new List<string>(commandsStr.Split(','));
                Mother.GetModule<CommandBus>().RegisterRemoteCommands(originId, commands);
            }
        }

        /// <summary>
        /// Check if the origin of a message is a programmable block 
        /// on the construct.
        /// </summary>
        /// <param name="originId"></param>
        /// <returns></returns>
        bool OriginIsOnConstruct(long originId)
        {
            return Mother.GetModule<BlockCatalogue>()
                .GetBlocks<IMyProgrammableBlock>()
                .Any(pb => pb.EntityId == originId);
        }
    }
}
