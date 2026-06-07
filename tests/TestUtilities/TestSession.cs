using IngameScript;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Reflection;

namespace MotherCore.Tests.TestUtilities
{
    /// <summary>
    /// Orchestrates a complete Mother script boot cycle for use in tests.
    /// </summary>
    /// <typeparam name="TProgram">
    /// The script's <c>Program</c> type. Must be a <see cref="MyGridProgram"/> subclass
    /// with a parameterless constructor. No extra interface is required — the session
    /// locates the <see cref="Mother"/> instance via reflection after construction.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Zero-configuration MotherCore test (uses the built-in <see cref="Program"/>):
    /// </para>
    /// <code>
    /// var session = new TestSession().Boot();
    /// session.Bus.RunTerminalCommand("help");
    /// </code>
    /// <para>
    /// Layer in only what each test needs:
    /// </para>
    /// <code>
    /// var session = new TestSession()
    ///     .WithCustomData(new CustomDataBuilder()
    ///         .WithCommand("openDoor", "door/open AirlockDoor")
    ///         .Build())
    ///     .WithCommands(tracker)
    ///     .Boot();
    ///
    /// session.Bus.RunTerminalCommand("openDoor");
    /// session.Clock.Tick();
    /// session.Clock.RunToIdle();
    /// </code>
    /// <para>
    /// To test a real script (MotherOS, MAPS, …) pass its <c>Program</c> type.
    /// The Program constructor runs normally, registering all its modules; the
    /// session then boots them and wires up the same helpers:
    /// </para>
    /// <code>
    /// // In a MotherOS test project — Program must implement IMotherProgram
    /// var session = new TestSession&lt;MotherOS.Program&gt;().Boot();
    /// </code>
    /// <para>
    /// For multi-script tests, join a <see cref="MockIGCNetwork"/>. The network
    /// automatically cross-registers every booted session in every other session's
    /// Almanac, so grids can discover each other by name without any manual wiring:
    /// </para>
    /// <code>
    /// var network = new MockIGCNetwork();
    ///
    /// var shipA = new TestSession("ShipA").OnNetwork(network).Boot();
    /// var shipB = new TestSession("ShipB").OnNetwork(network).Boot();
    ///
    /// // ShipA already knows "ShipB" and vice versa — no RegisterInAlmanac call needed.
    /// shipA.Bus.RunTerminalCommand("@ShipB weapons/fire");
    /// network.Deliver();
    /// </code>
    /// <para>
    /// <see cref="OnBeforeBoot"/> lets you inject extra test-only modules or commands
    /// after the Program constructor has run but before any module is booted:
    /// </para>
    /// <code>
    /// public class MyTestSession : TestSession
    /// {
    ///     protected override void OnBeforeBoot(Mother mother)
    ///     {
    ///         new MyExtraModule(mother);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class TestSession<TProgram> : ITestSession
        where TProgram : MyGridProgram, new()
    {
        Mother _mother;
        string _customData;
        IMyIntergridCommunicationSystem _igc;
        readonly List<BaseModuleCommand> _commands = new List<BaseModuleCommand>();
        MockIGCNetwork _network;
        readonly string _gridName;

        /// <param name="gridName">
        /// Optional grid name for this session. Sets <see cref="Mother.Name"/> before boot
        /// and is used as the address other sessions use to reach this one on a
        /// <see cref="MockIGCNetwork"/>. When omitted, falls back to the cube grid's
        /// <c>CustomName</c> and then to <c>"grid-{Mother.Id}"</c>.
        /// </param>
        public TestSession(string gridName = null)
        {
            _gridName = gridName;
        }

        /// <summary>The booted <see cref="CommandBus"/>. Available after <see cref="Boot"/> is called.</summary>
        public CommandBus Bus { get; private set; }

        /// <summary>
        /// A <see cref="ClockDriver"/> wrapping the system clock.
        /// Available after <see cref="Boot"/> is called.
        /// </summary>
        public ClockDriver Clock { get; private set; }

        /// <summary>The booted <see cref="Configuration"/>. Available after <see cref="Boot"/> is called.</summary>
        public Configuration Config { get; private set; }

        /// <summary>
        /// The underlying <see cref="Mother"/> instance.
        /// Useful for accessing modules not directly exposed by the session.
        /// </summary>
        public Mother Mother => _mother;

        /// <summary>
        /// The booted <typeparamref name="TProgram"/> instance.
        /// Use this to access script-specific state after boot.
        /// Available after <see cref="Boot"/> is called.
        /// </summary>
        public TProgram Program { get; private set; }

        /// <summary>
        /// The IGC in use by this session's program.
        /// When joined to a <see cref="MockIGCNetwork"/> this is the session's
        /// <see cref="MockIGC"/>; use <see cref="NetworkIGC"/> for the typed reference.
        /// </summary>
        public IMyIntergridCommunicationSystem IGC => _mother.IGC;

        /// <summary>
        /// The typed <see cref="MockIGC"/> for this session.
        /// Non-null only when the session was joined to a <see cref="MockIGCNetwork"/>
        /// via <see cref="OnNetwork"/>.
        /// </summary>
        public MockIGC NetworkIGC { get; private set; }

        /// <summary>
        /// Injects a pre-created IGC into this session's program. Use this when you
        /// need an explicit <see cref="MockIGC"/> reference before calling
        /// <see cref="Boot"/>. When also joined to a <see cref="MockIGCNetwork"/> via
        /// <see cref="OnNetwork"/>, the network-allocated IGC takes precedence.
        /// </summary>
        public TestSession<TProgram> WithIGC(IMyIntergridCommunicationSystem igc)
        {
            _igc = igc;
            return this;
        }

        /// <summary>
        /// Joins this session to a <see cref="MockIGCNetwork"/>. A <see cref="MockIGC"/>
        /// will be allocated from the network and injected into this session's
        /// <c>Program</c> during <see cref="Boot"/>. After boot, the session is
        /// automatically cross-registered in every other booted session's Almanac
        /// under the grid name supplied to the constructor (or the derived fallback).
        /// </summary>
        public TestSession<TProgram> OnNetwork(MockIGCNetwork network)
        {
            _network = network;
            return this;
        }

        /// <summary>
        /// Sets the programmable block's <c>CustomData</c> before boot.
        /// Use <see cref="CustomDataBuilder"/> to construct the INI string.
        /// </summary>
        public TestSession<TProgram> WithCustomData(string customData)
        {
            _customData = customData;
            return this;
        }

        /// <summary>
        /// Registers one or more commands with the <see cref="CommandBus"/> after boot.
        /// </summary>
        public TestSession<TProgram> WithCommands(params BaseModuleCommand[] commands)
        {
            _commands.AddRange(commands);
            return this;
        }

        /// <summary>
        /// Called after the <typeparamref name="TProgram"/> constructor has run
        /// (so all script modules are already registered) but before any module
        /// is booted. Override to inject test-only modules or perform pre-boot setup.
        /// </summary>
        protected virtual void OnBeforeBoot(Mother mother) { }

        /// <summary>
        /// Locates the <see cref="Mother"/> instance created by the Program constructor
        /// by scanning instance fields for a field of type <see cref="Mother"/>.
        /// Walks the type hierarchy so scripts that use partial classes or base classes
        /// are handled transparently.
        /// </summary>
        static Mother FindMother(MyGridProgram program)
        {
            var type = program.GetType();
            while (type != null && type != typeof(object))
            {
                foreach (var field in type.GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(Mother))
                        return (Mother)field.GetValue(program);
                }
                type = type.BaseType;
            }
            throw new System.InvalidOperationException(
                $"No field of type Mother was found on {program.GetType().Name}. " +
                "Ensure the Program constructor creates a Mother instance and stores it in a field.");
        }

        /// <summary>
        /// Constructs the <typeparamref name="TProgram"/> (injecting a
        /// <see cref="MockIGC"/> when joined to a network), extracts the
        /// <see cref="Mother"/> the constructor created, applies
        /// <c>CustomData</c>, calls <see cref="OnBeforeBoot"/>, boots all
        /// registered modules, and — if on a network — cross-registers this
        /// session in every other booted session's Almanac.
        /// Returns <c>this</c> so the call can be chained inline.
        /// </summary>
        public TestSession<TProgram> Boot()
        {
            TProgram program;

            if (_network != null)
            {
                NetworkIGC = _network.AllocateEndpoint();
                program = Gateway.CreateProgram<TProgram>().WithIgc(NetworkIGC).Build();
            }
            else if (_igc != null)
            {
                program = Gateway.CreateProgram<TProgram>().WithIgc(_igc).Build();
            }
            else
            {
                program = Gateway.CreateProgram<TProgram>().Build();
            }

            Program = program;
            _mother = FindMother(program);

            if (_customData != null)
                _mother.ProgrammableBlock.CustomData = _customData;

            OnBeforeBoot(_mother);

            foreach (var module in _mother.CoreModules.Values)
                module.Boot();

            foreach (var module in _mother.ExtensionModules.Values)
                module.Boot();

            if (!string.IsNullOrEmpty(_gridName))
                _mother.Name = _gridName;
            else if (string.IsNullOrEmpty(_mother.Name))
                _mother.Name = $"grid-{_mother.Id}";

            foreach (var command in _commands)
                _mother.GetModule<CommandBus>().RegisterCommand(command);

            Config = _mother.GetModule<Configuration>();
            Bus = _mother.GetModule<CommandBus>();

            var clock = _mother.GetModule<Clock>();
            clock.Reset();
            Clock = new ClockDriver(clock);

            if (_network != null)
                _network.RegisterSession(this, _mother.Name);

            return this;
        }
    }

    /// <summary>
    /// Convenience alias for <see cref="TestSession{TProgram}"/> that targets the
    /// built-in MotherCore test <see cref="Program"/>. All existing MotherCore tests
    /// continue to work without any changes.
    /// </summary>
    /// <remarks>
    /// All fluent methods are overridden here to return <c>TestSession</c> so that
    /// the compiler infers the correct type when chaining (e.g.
    /// <c>new TestSession().WithCustomData(...).Boot()</c>).
    /// </remarks>
    public class TestSession : TestSession<Program>
    {
        /// <inheritdoc cref="TestSession{TProgram}(string)"/>
        public TestSession(string gridName = null) : base(gridName) { }

        /// <inheritdoc cref="TestSession{TProgram}.WithIGC"/>
        public new TestSession WithIGC(IMyIntergridCommunicationSystem igc) { base.WithIGC(igc); return this; }

        /// <inheritdoc cref="TestSession{TProgram}.OnNetwork"/>
        public new TestSession OnNetwork(MockIGCNetwork network) { base.OnNetwork(network); return this; }

        /// <inheritdoc cref="TestSession{TProgram}.WithCustomData"/>
        public new TestSession WithCustomData(string customData) { base.WithCustomData(customData); return this; }

        /// <inheritdoc cref="TestSession{TProgram}.WithCommands"/>
        public new TestSession WithCommands(params BaseModuleCommand[] commands) { base.WithCommands(commands); return this; }

        /// <inheritdoc cref="TestSession{TProgram}.Boot"/>
        public new TestSession Boot() { base.Boot(); return this; }
    }
}
