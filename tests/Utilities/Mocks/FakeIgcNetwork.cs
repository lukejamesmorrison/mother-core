using IngameScript;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// A simulated intergrid communication network that connects multiple
    /// <see cref="Script"/> instances for multi-script tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each script on the network gets a unique <see cref="FakeIgc"/> that routes
    /// outbound messages through the network. Pending messages are buffered until
    /// <see cref="Deliver"/> is called, giving tests precise control over when
    /// each message is processed.
    /// </para>
    /// <code>
    /// var network = new FakeIgcNetwork();
    ///
    /// var shipA = new Script("ShipA")
    ///     .OnNetwork(network)
    ///     .WithCustomData(new CustomDataComposer().WithCommand("attack", "@ShipB weapons/fire").Build())
    ///     .Boot();
    ///
    /// var shipB = new Script("ShipB").OnNetwork(network).Boot();
    ///
    /// shipA.Bus.RunTerminalCommand("attack");
    ///
    /// // Verify the outbound message without full delivery
    /// Assert.That(network.SentMessages.Any(m => m.TargetId == shipB.IGC.Me), Is.True);
    ///
    /// // Or deliver fully and assert execution on the recipient
    /// network.Deliver();
    /// </code>
    /// </remarks>
    public class FakeIgcNetwork
    {
        /// <summary>
        /// The next synthetic endpoint ID for this in-memory network.
        /// Starts high to reduce collisions with realistic in-game IDs in assertions.
        /// </summary>
        static long _nextId = 100_000_000_000L;

        /// <summary>
        /// Endpoints currently registered on this fake network.
        /// Used for message routing and reachability checks.
        /// </summary>
        readonly List<FakeIgc> _endpoints = new List<FakeIgc>();

        /// <summary>
        /// Scripts registered on this network via <see cref="Script{TProgram}.Boot"/>,
        /// paired with script names for Almanac cross-registration.
        /// </summary>
        readonly List<(IScript Session, string GridName)> _scripts = new List<(IScript, string)>();

        /// <summary>
        /// Pending deliveries queued by send operations and consumed by <see cref="Deliver"/>.
        /// </summary>
        readonly List<PendingDelivery> _pending = new List<PendingDelivery>();

        /// <summary>
        /// Outbound traffic capture since the last <see cref="ClearSentMessages"/> call.
        /// Use for lightweight assertions without forcing delivery.
        /// </summary>
        public List<SentMessage> SentMessages { get; } = new List<SentMessage>();

        readonly List<DroppedMessage> _droppedMessages = new List<DroppedMessage>();

        /// <summary>
        /// Read-only transport telemetry for dropped deliveries and rejected sends.
        /// </summary>
        public IReadOnlyList<DroppedMessage> DroppedMessages => _droppedMessages;

        /// <summary>
        /// Registered scripts ordered by registration time.
        /// </summary>
        public IReadOnlyList<IScript> Scripts => _scripts.Select(s => s.Session).ToList();

        /// <summary>
        /// Creates and registers a new <see cref="FakeIgc"/> endpoint on this network.
        /// Called during <see cref="Script{TProgram}.Boot"/> when a script joins via
        /// <see cref="Script{TProgram}.OnNetwork(FakeIgcNetwork)"/>.
        /// </summary>
        /// <returns>The created endpoint bound to this network instance.</returns>
        public FakeIgc CreateNetworkEndpoint()
        {
            var igc = new FakeIgc(this, _nextId++);

            _endpoints.Add(igc);

            return igc;
        }

        /// <summary>
        /// Registers a booted script and performs two-way Almanac cross-registration
        /// against all already-registered scripts.
        /// Called automatically by <see cref="Script{TProgram}.Boot"/>.
        /// </summary>
        /// <param name="script">The newly booted script to register.</param>
        /// <param name="gridName">The script name used for Almanac identity and addressing.</param>
        internal void RegisterScript(IScript script, string gridName)
        {
            foreach (var (existing, existingName) in _scripts)
            {
                SyncToAlmanac(script, existing, existingName);
                SyncToAlmanac(existing, script, gridName);
            }

            _scripts.Add((script, gridName));
        }

        /// <summary>
        /// Adds or updates the recipient's Almanac record for a peer script.
        /// </summary>
        /// <param name="recipient">The script that will receive the Almanac update.</param>
        /// <param name="subject">The script that is the subject of the Almanac update.</param>
        /// <param name="subjectName">The grid name of the subject script.</param>
        static void SyncToAlmanac(IScript recipient, IScript subject, string subjectName)
        {
            var almanac = recipient.Mother.GetModule<Almanac>();

            if (almanac == null) return;

            almanac.UpdateOrCreateFromMessage(
                subjectName, subject.IGC.Me, subjectName,
                new VRageMath.Vector3D(0, 0, 0), 0f,
                new HashSet<string>(), 
                true, 
                null, 
                null
            );
        }

        /// <summary>
        /// Routes all pending messages to their recipients, then calls
        /// <c>HandleIncomingIGCMessages</c> on every script that has pending input.
        /// Returns <c>this</c> for chaining.
        /// </summary>
        public FakeIgcNetwork Deliver()
        {
            foreach (var delivery in _pending.ToList())
            {
                var target = _endpoints.FirstOrDefault(e => e.Me == delivery.TargetId);
                if (target == null)
                {
                    RecordDrop(
                        DroppedMessageReason.UnknownEndpoint,
                        delivery.SourceId,
                        delivery.TargetId,
                        delivery.Tag,
                        delivery.Data,
                        delivery.IsBroadcast);
                    continue;
                }

                var msg = new MyIGCMessage(delivery.Data, delivery.Tag, delivery.SourceId);

                if (delivery.IsBroadcast)
                    target.EnqueueBroadcast(delivery.Tag, msg);
                else
                    target.EnqueueUnicast(msg);
            }
            _pending.Clear();

            foreach (var (session, _) in _scripts)
            {
                var igc = session.IGC as FakeIgc;

                if (igc?.HasPendingMessages == true)
                    session.Mother.GetModule<IntergridMessageService>()?.HandleIncomingIGCMessages();
            }

            return this;
        }

        /// <summary>
        /// Clears transport telemetry captured since the previous phase.
        /// This resets both <see cref="SentMessages"/> and <see cref="DroppedMessages"/>.
        /// </summary>
        /// <returns>The current network for fluent chaining.</returns>
        public FakeIgcNetwork ClearSentMessages()
        {
            SentMessages.Clear();
            _droppedMessages.Clear();

            return this;
        }

        /// <summary>
        /// Routes all pending messages to their recipients and triggers
        /// <c>HandleIncomingIGCMessages</c> on every script with pending input.
        /// Preferred alias for <see cref="Deliver"/> that aligns with the
        /// <see cref="TestWorld"/> API naming.
        /// </summary>
        public FakeIgcNetwork DispatchIgc() => Deliver();

        /// <summary>
        /// Returns whether the specified endpoint ID exists on this network.
        /// Used by <see cref="FakeIgc.IsEndpointReachable(long, TransmissionDistance)"/>.
        /// </summary>
        /// <param name="id">The endpoint ID to check.</param>
        /// <returns><c>true</c> when an endpoint with that ID is registered; otherwise <c>false</c>.</returns>
        internal bool HasEndpoint(long id) => _endpoints.Any(e => e.Me == id);

        /// <summary>
        /// Queues a unicast send for later delivery.
        /// Called by <see cref="FakeIgc.SendUnicastMessage{TData}(long, string, TData)"/>.
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="tag"></param>
        /// <param name="data"></param>
        /// <param name="sourceId"></param>
        internal void EnqueueUnicast(long targetId, string tag, object data, long sourceId)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                RecordDrop(DroppedMessageReason.InvalidTag, sourceId, targetId, tag, data, isBroadcast: false);
                return;
            }

            SentMessages.Add(new SentMessage(sourceId, targetId, tag, data, isBroadcast: false));
            _pending.Add(new PendingDelivery(targetId, tag, data, sourceId, isBroadcast: false));
        }

        /// <summary>
        /// Queues a broadcast send for later delivery to all endpoints except the sender.
        /// Called by <see cref="FakeIgc.SendBroadcastMessage{TData}(string, TData, TransmissionDistance)"/>.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="data"></param>
        /// <param name="sourceId"></param>
        internal void EnqueueBroadcast(string tag, object data, long sourceId)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                RecordDrop(DroppedMessageReason.InvalidTag, sourceId, -1, tag, data, isBroadcast: true);
                return;
            }

            SentMessages.Add(new SentMessage(sourceId, targetId: -1, tag, data, isBroadcast: true));

            foreach (var endpoint in _endpoints)
                if (endpoint.Me != sourceId)
                    _pending.Add(new PendingDelivery(endpoint.Me, tag, data, sourceId, isBroadcast: true));
        }

        /// <summary>
        /// Records a dropped or rejected message for assertion-friendly transport telemetry.
        /// </summary>
        internal void RecordDrop(
            DroppedMessageReason reason,
            long sourceId,
            long targetId,
            string tag,
            object data,
            bool isBroadcast)
        {
            _droppedMessages.Add(new DroppedMessage(reason, sourceId, targetId, tag, data, isBroadcast));
        }

        /// <summary>
        /// Asserts that a unicast message matching the specified source, target, and tag was captured.
        /// </summary>
        public void ShouldHaveUnicast(long sourceId, long targetId, string tag)
        {
            Assert.That(
                SentMessages.Any(m => !m.IsBroadcast && m.SourceId == sourceId && m.TargetId == targetId && m.Tag == tag),
                Is.True,
                $"Expected a unicast message from '{sourceId}' to '{targetId}' on tag '{tag}', but none was recorded.");
        }

        /// <summary>
        /// Asserts that a broadcast message matching the specified source and tag was captured.
        /// </summary>
        public void ShouldHaveBroadcast(long sourceId, string tag)
        {
            Assert.That(
                SentMessages.Any(m => m.IsBroadcast && m.SourceId == sourceId && m.Tag == tag),
                Is.True,
                $"Expected a broadcast message from '{sourceId}' on tag '{tag}', but none was recorded.");
        }

        /// <summary>
        /// Asserts that no sent or dropped traffic has been captured.
        /// </summary>
        public void ShouldHaveNoTraffic()
        {
            Assert.That(SentMessages, Is.Empty,
                "Expected no sent traffic, but sent messages were recorded.");
            Assert.That(DroppedMessages, Is.Empty,
                "Expected no dropped traffic, but dropped messages were recorded.");
        }

        /// <summary>
        /// Asserts that dropped-traffic telemetry contains a message matching the given criteria.
        /// </summary>
        public void ShouldHaveDroppedMessage(
            DroppedMessageReason reason,
            long sourceId,
            long? targetId = null,
            string tag = null)
        {
            Assert.That(
                DroppedMessages.Any(message =>
                    message.Reason == reason
                    && message.SourceId == sourceId
                    && (!targetId.HasValue || message.TargetId == targetId.Value)
                    && (tag == null || message.Tag == tag)),
                Is.True,
                $"Expected dropped message with reason '{reason}', source '{sourceId}', target '{targetId}', and tag '{tag}', but none was recorded.");
        }

        // =====================================================================
        // Captured message record
        // =====================================================================

        /// <summary>
        /// A record of a message sent through the network, captured in the <see cref="SentMessages"/> 
        /// log for lightweight assertions.
        /// </summary>
        public class SentMessage
        {
            /// <summary>The <c>IGC.Me</c> of the sending script.</summary>
            public long SourceId { get; }

            /// <summary>The target ID for unicast messages, or <c>-1</c> for broadcasts.</summary>
            public long TargetId { get; }

            /// <summary>The IGC channel tag.</summary>
            public string Tag { get; }

            /// <summary>The raw message payload.</summary>
            public object Data { get; }

            /// <summary><c>true</c> if this was a broadcast; <c>false</c> for unicast.</summary>
            public bool IsBroadcast { get; }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="sourceId"></param>
            /// <param name="targetId"></param>
            /// <param name="tag"></param>
            /// <param name="data"></param>
            /// <param name="isBroadcast"></param>
            internal SentMessage(long sourceId, long targetId, string tag, object data, bool isBroadcast)
            {
                SourceId = sourceId;
                TargetId = targetId;
                Tag = tag;
                Data = data;
                IsBroadcast = isBroadcast;
            }
        }

        /// <summary>
        /// Captured telemetry for a dropped or rejected message.
        /// </summary>
        public class DroppedMessage
        {
            public DroppedMessageReason Reason { get; }
            public long SourceId { get; }
            public long TargetId { get; }
            public string Tag { get; }
            public object Data { get; }
            public bool IsBroadcast { get; }

            internal DroppedMessage(
                DroppedMessageReason reason,
                long sourceId,
                long targetId,
                string tag,
                object data,
                bool isBroadcast)
            {
                Reason = reason;
                SourceId = sourceId;
                TargetId = targetId;
                Tag = tag;
                Data = data;
                IsBroadcast = isBroadcast;
            }
        }

        /// <summary>
        /// Drop categories used by transport telemetry.
        /// </summary>
        public enum DroppedMessageReason
        {
            UnknownEndpoint,
            DisabledListener,
            InvalidTag
        }

        // =====================================================================
        // Internal delivery record
        // =====================================================================

        struct PendingDelivery
        {
            public long TargetId;
            public string Tag;
            public object Data;
            public long SourceId;
            public bool IsBroadcast;

            public PendingDelivery(long targetId, string tag, object data, long sourceId, bool isBroadcast)
            {
                TargetId = targetId;
                Tag = tag;
                Data = data;
                SourceId = sourceId;
                IsBroadcast = isBroadcast;
            }
        }
    }

    // =========================================================================
    // FakeIgc — IMyIntergridCommunicationSystem implementation
    // =========================================================================

    /// <summary>
    /// Test double for <see cref="IMyIntergridCommunicationSystem"/> that routes
    /// messages through <see cref="FakeIgcNetwork"/> instead of the game engine.
    /// </summary>
    public class FakeIgc : IMyIntergridCommunicationSystem
    {
        readonly FakeIgcNetwork _network;
        readonly FakeUnicastListener _unicastListener;
        readonly Dictionary<string, FakeBroadcastListener> _broadcastListeners
            = new Dictionary<string, FakeBroadcastListener>();

        /// <inheritdoc/>
        public long Me { get; }

        /// <inheritdoc/>
        public IMyUnicastListener UnicastListener => _unicastListener;

        internal FakeIgc(FakeIgcNetwork network, long id)
        {
            _network = network;
            Me = id;
            _unicastListener = new FakeUnicastListener();
        }

        /// <inheritdoc/>
        public IMyBroadcastListener RegisterBroadcastListener(string tag)
        {
            if (!_broadcastListeners.ContainsKey(tag))
            {
                _broadcastListeners[tag] = new FakeBroadcastListener(tag);
            }
            else
            {
                _broadcastListeners[tag].Enable();
            }

            return _broadcastListeners[tag];
        }

        /// <inheritdoc/>
        public bool SendUnicastMessage<TData>(long target, string tag, TData data)
        {
            _network.EnqueueUnicast(target, tag, data, Me);
            return true;
        }

        /// <inheritdoc/>
        public void SendBroadcastMessage<TData>(string tag, TData data,
            TransmissionDistance transmissionDistance = TransmissionDistance.TransmissionDistanceMax)
        {
            _network.EnqueueBroadcast(tag, data, Me);
        }

        /// <inheritdoc/>
        public bool IsEndpointReachable(long id,
            TransmissionDistance transmissionDistance = TransmissionDistance.TransmissionDistanceMax)
            => _network.HasEndpoint(id);

        /// <inheritdoc/>
        public void DisableBroadcastListener(IMyBroadcastListener listener)
        {
            if (listener is FakeBroadcastListener fakeListener) fakeListener.Disable();
        }

        /// <inheritdoc/>
        public void GetBroadcastListeners(List<IMyBroadcastListener> listeners,
            Func<IMyBroadcastListener, bool> collect = null)
        {
            foreach (var listener in _broadcastListeners.Values)
                if (collect == null || collect(listener))
                    listeners.Add(listener);
        }

        internal void EnqueueUnicast(MyIGCMessage message) => _unicastListener.Enqueue(message);

        internal void EnqueueBroadcast(string tag, MyIGCMessage message)
        {
            if (_broadcastListeners.TryGetValue(tag, out var listener))
            {
                if (!listener.IsActive)
                {
                    _network.RecordDrop(
                        FakeIgcNetwork.DroppedMessageReason.DisabledListener,
                        message.Source,
                        Me,
                        tag,
                        message.Data,
                        isBroadcast: true);
                    return;
                }

                listener.Enqueue(message);
            }
        }

        internal bool HasPendingMessages =>
            _unicastListener.HasPendingMessage ||
            _broadcastListeners.Values.Any(l => l.HasPendingMessage);
    }

    // =========================================================================
    // FakeUnicastListener
    // =========================================================================

    /// <summary>Test double for <see cref="IMyUnicastListener"/>.</summary>
    public class FakeUnicastListener : IMyUnicastListener
    {
        readonly Queue<MyIGCMessage> _messages = new Queue<MyIGCMessage>();

        /// <inheritdoc/>
        public bool HasPendingMessage => _messages.Count > 0;

        /// <inheritdoc/>
        public int MaxWaitingMessages => int.MaxValue;

        /// <inheritdoc/>
        public MyIGCMessage AcceptMessage() => _messages.Dequeue();

        /// <inheritdoc/>
        public void SetMessageCallback(string callbackName = "") { }

        /// <inheritdoc/>
        public void DisableMessageCallback() { }

        internal void Enqueue(MyIGCMessage message) => _messages.Enqueue(message);
    }

    // =========================================================================
    // FakeBroadcastListener
    // =========================================================================

    /// <summary>Test double for <see cref="IMyBroadcastListener"/>.</summary>
    public class FakeBroadcastListener : IMyBroadcastListener
    {
        readonly Queue<MyIGCMessage> _messages = new Queue<MyIGCMessage>();

        /// <inheritdoc/>
        public string Tag { get; }

        /// <inheritdoc/>
        public bool IsActive { get; private set; } = true;

        /// <inheritdoc/>
        public bool HasPendingMessage => _messages.Count > 0;

        /// <inheritdoc/>
        public int MaxWaitingMessages => int.MaxValue;

        /// <inheritdoc/>
        public MyIGCMessage AcceptMessage() => _messages.Dequeue();

        /// <inheritdoc/>
        public void SetMessageCallback(string callbackName = "") { }

        /// <inheritdoc/>
        public void DisableMessageCallback() { }

        internal FakeBroadcastListener(string tag) { Tag = tag; }

        internal void Enqueue(MyIGCMessage message) => _messages.Enqueue(message);

        /// <summary>
        /// Disables this listener, preventing it from receiving any future messages. Messages 
        /// already enqueued will still be delivered, but no new messages will be added 
        /// to the queue after this is called.
        /// </summary>
        internal void Disable() => IsActive = false;

        internal void Enable() => IsActive = true;
    }
}
