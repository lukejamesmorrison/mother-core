using IngameScript;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace MotherCore.Tests.TestUtilities
{
    /// <summary>
    /// Orchestrates a complete MotherCore boot cycle for use in tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TestSession</c> is self-contained — it creates its own <c>Program</c> and
    /// <see cref="Mother"/> during <see cref="Boot"/>. The zero-configuration case is
    /// one line:
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
    /// To register project-specific modules (MotherOS, MotherGUI, etc.) subclass
    /// <c>TestSession</c> and override <see cref="OnBeforeBoot"/>:
    /// </para>
    /// <code>
    /// public class MotherOSTestSession : TestSession
    /// {
    ///     protected override void OnBeforeBoot(Mother mother)
    ///     {
    ///         new DoorModule(mother);
    ///         new LightModule(mother);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class TestSession
    {
        Mother _mother;
        string _customData;
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
        /// Joins this session to a <see cref="MockIGCNetwork"/>. A <see cref="MockIGC"/>
        /// will be allocated from the network and injected into this session's
        /// <c>Program</c> during <see cref="Boot"/>. After boot, the session is
        /// automatically cross-registered in every other booted session's Almanac
        /// under the grid name supplied to the constructor (or the derived fallback).
        /// </summary>
        /// <param name="network">The network to join.</param>
        public TestSession OnNetwork(MockIGCNetwork network)
        {
            _network = network;
            return this;
        }

        /// <summary>
        /// Sets the programmable block's <c>CustomData</c> before boot.
        /// Use <see cref="CustomDataBuilder"/> to construct the INI string.
        /// </summary>
        public TestSession WithCustomData(string customData)
        {
            _customData = customData;
            return this;
        }

        /// <summary>
        /// Registers one or more commands with the <see cref="CommandBus"/> after boot.
        /// </summary>
        public TestSession WithCommands(params BaseModuleCommand[] commands)
        {
            _commands.AddRange(commands);
            return this;
        }

        /// <summary>
        /// Override in a subclass to register project-specific modules before the boot
        /// sequence runs. Called after <c>CustomData</c> is applied but before any module
        /// is booted. Use <see cref="Mother.RegisterCoreModule"/> for core modules and
        /// <see cref="Mother.RegisterModule"/> for extension modules — both will be picked
        /// up automatically by the boot loop.
        /// </summary>
        protected virtual void OnBeforeBoot(Mother mother) { }

        /// <summary>
        /// Creates the <c>Program</c> and <see cref="Mother"/> (injecting a
        /// <see cref="MockIGC"/> when joined to a network), applies <c>CustomData</c>,
        /// calls <see cref="OnBeforeBoot"/>, boots <see cref="Configuration"/> and
        /// <see cref="CommandBus"/>, registers any additional commands, resets the clock,
        /// and — if on a network — cross-registers this session in every other booted
        /// session's Almanac.
        /// Returns <c>this</c> so the call can be chained inline.
        /// </summary>
        public TestSession Boot()
        {
            Program program;

            if (_network != null)
            {
                NetworkIGC = _network.AllocateEndpoint();
                program = Gateway.CreateProgram<Program>().WithIgc(NetworkIGC).Build();
            }
            else
            {
                program = Gateway.CreateProgram<Program>().Build();
            }

            _mother = new Mother(program);

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
}
