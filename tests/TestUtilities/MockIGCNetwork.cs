using IngameScript;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.TestUtilities
{
    /// <summary>
    /// A simulated intergrid communication network that connects multiple
    /// <see cref="Script"/> instances for multi-script tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each script on the network gets a unique <see cref="MockIGC"/> that routes
    /// outbound messages through the network. Pending messages are buffered until
    /// <see cref="Deliver"/> is called, giving tests precise control over when
    /// each message is processed.
    /// </para>
    /// <code>
    /// var network = new MockIGCNetwork();
    ///
    /// var shipA = new Script("ShipA")
    ///     .OnNetwork(network)
    ///     .WithCustomData(new CustomDataBuilder().WithCommand("attack", "@ShipB weapons/fire").Build())
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
    public class MockIGCNetwork
    {
        static long _nextId = 100_000_000_000L;

        readonly List<MockIGC> _endpoints = new List<MockIGC>();
        readonly List<(IScript Session, string GridName)> _sessions
            = new List<(IScript, string)>();
        readonly List<PendingDelivery> _pending = new List<PendingDelivery>();

        /// <summary>
        /// All messages sent through the network since the last <see cref="ClearSentMessages"/>
        /// call. Use this for lightweight assertions without needing full delivery.
        /// </summary>
        public List<SentMessage> SentMessages { get; } = new List<SentMessage>();

        /// <summary>
        /// Allocates a new <see cref="MockIGC"/> endpoint on this network.
        /// Called internally by <see cref="Script.Boot"/> when the script
        /// has been joined via <see cref="Script.OnNetwork"/>.
        /// </summary>
        public MockIGC AllocateEndpoint()
        {
            var igc = new MockIGC(this, _nextId++);
            _endpoints.Add(igc);
            return igc;
        }

        /// <summary>
        /// Registers a booted script on the network and cross-populates every other
        /// booted script's Almanac with this script's grid name, and vice versa.
        /// Called automatically by <see cref="Script{TProgram}.Boot"/> - no
        /// manual call required.
        /// </summary>
        internal void RegisterSession(IScript session, string gridName)
        {
            foreach (var (existing, existingName) in _sessions)
            {
                SyncToAlmanac(session, existing, existingName);
                SyncToAlmanac(existing, session, gridName);
            }

            _sessions.Add((session, gridName));
        }

        static void SyncToAlmanac(IScript recipient, IScript subject, string subjectName)
        {
            var almanac = recipient.Mother.GetModule<Almanac>();
            if (almanac == null) return;

            almanac.UpdateOrCreateFromMessage(
                subjectName, subject.IGC.Me, subjectName,
                new VRageMath.Vector3D(0, 0, 0), 0f,
                new HashSet<string>(), true, null, null);
        }

        /// <summary>
        /// Routes all pending messages to their recipients, then calls
        /// <c>HandleIncomingIGCMessages</c> on every script that has pending input.
        /// Returns <c>this</c> for chaining.
        /// </summary>
        public MockIGCNetwork Deliver()
        {
            foreach (var delivery in _pending.ToList())
            {
                var target = _endpoints.FirstOrDefault(e => e.Me == delivery.TargetId);
                if (target == null) continue;

                var msg = new MyIGCMessage(delivery.Data, delivery.Tag, delivery.SourceId);
                if (delivery.IsBroadcast)
                    target.EnqueueBroadcast(delivery.Tag, msg);
                else
                    target.EnqueueUnicast(msg);
            }
            _pending.Clear();

            foreach (var (session, _) in _sessions)
            {
                if (session.NetworkIGC?.HasPendingMessages == true)
                    session.Mother.GetModule<IntergridMessageService>()?.HandleIncomingIGCMessages();
            }

            return this;
        }

        /// <summary>Clears the <see cref="SentMessages"/> capture list.</summary>
        public MockIGCNetwork ClearSentMessages()
        {
            SentMessages.Clear();
            return this;
        }

        internal bool HasEndpoint(long id) => _endpoints.Any(e => e.Me == id);

        internal void EnqueueUnicast(long targetId, string tag, object data, long sourceId)
        {
            SentMessages.Add(new SentMessage(sourceId, targetId, tag, data, isBroadcast: false));
            _pending.Add(new PendingDelivery(targetId, tag, data, sourceId, isBroadcast: false));
        }

        internal void EnqueueBroadcast(string tag, object data, long sourceId)
        {
            SentMessages.Add(new SentMessage(sourceId, targetId: -1, tag, data, isBroadcast: true));
            foreach (var endpoint in _endpoints)
                if (endpoint.Me != sourceId)
                    _pending.Add(new PendingDelivery(endpoint.Me, tag, data, sourceId, isBroadcast: true));
        }

        // =====================================================================
        // Captured message record
        // =====================================================================

        /// <summary>A message that was sent through the network.</summary>
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

            internal SentMessage(long sourceId, long targetId, string tag, object data, bool isBroadcast)
            {
                SourceId = sourceId;
                TargetId = targetId;
                Tag = tag;
                Data = data;
                IsBroadcast = isBroadcast;
            }
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
    // MockIGC — IMyIntergridCommunicationSystem implementation
    // =========================================================================

    /// <summary>
    /// A test double for <see cref="IMyIntergridCommunicationSystem"/> that routes
    /// messages through a <see cref="MockIGCNetwork"/> rather than the game engine.
    /// </summary>
    public class MockIGC : IMyIntergridCommunicationSystem
    {
        readonly MockIGCNetwork _network;
        readonly MockUnicastListener _unicastListener;
        readonly Dictionary<string, MockBroadcastListener> _broadcastListeners
            = new Dictionary<string, MockBroadcastListener>();

        /// <inheritdoc/>
        public long Me { get; }

        /// <inheritdoc/>
        public IMyUnicastListener UnicastListener => _unicastListener;

        internal MockIGC(MockIGCNetwork network, long id)
        {
            _network = network;
            Me = id;
            _unicastListener = new MockUnicastListener();
        }

        /// <inheritdoc/>
        public IMyBroadcastListener RegisterBroadcastListener(string tag)
        {
            if (!_broadcastListeners.ContainsKey(tag))
                _broadcastListeners[tag] = new MockBroadcastListener(tag);
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
            if (listener is MockBroadcastListener mockListener) mockListener.Disable();
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
                listener.Enqueue(message);
        }

        internal bool HasPendingMessages =>
            _unicastListener.HasPendingMessage ||
            _broadcastListeners.Values.Any(l => l.HasPendingMessage);
    }

    // =========================================================================
    // MockUnicastListener
    // =========================================================================

    /// <summary>Test double for <see cref="IMyUnicastListener"/>.</summary>
    public class MockUnicastListener : IMyUnicastListener
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
    // MockBroadcastListener
    // =========================================================================

    /// <summary>Test double for <see cref="IMyBroadcastListener"/>.</summary>
    public class MockBroadcastListener : IMyBroadcastListener
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

        internal MockBroadcastListener(string tag) { Tag = tag; }

        internal void Enqueue(MyIGCMessage message) => _messages.Enqueue(message);

        internal void Disable() => IsActive = false;
    }
}
