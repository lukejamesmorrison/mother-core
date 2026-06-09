using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Reflection;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Orchestrates a complete Mother script boot cycle for use in tests.
    /// </summary>
    /// <typeparam name="TProgram">
    /// The script's <c>Program</c> type. Must be a <see cref="MyGridProgram"/> subclass
    /// with a parameterless constructor. No extra interface is required — the script
    /// locates the <see cref="Mother"/> instance via reflection after construction.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Zero-configuration MotherCore test (uses the built-in <see cref="Program"/>):
    /// </para>
    /// <code>
    /// var script = new Script().Boot();
    /// script.Bus.RunTerminalCommand("help");
    /// </code>
    /// <para>
    /// Layer in only what each test needs:
    /// </para>
    /// <code>
    /// var script = new Script()
    ///     .WithCustomData(new CustomDataComposer()
    ///         .WithCommand("openDoor", "door/open AirlockDoor")
    ///         .Build())
    ///     .WithCommands(tracker)
    ///     .Boot();
    ///
    /// script.Bus.RunTerminalCommand("openDoor");
    /// script.Clock.Tick();
    /// script.Clock.RunToIdle();
    /// </code>
    /// <para>
    /// To test a real script (MotherOS, MAPS, …) pass its <c>Program</c> type.
    /// The Program constructor runs normally, registering all its modules; the
    /// script then boots them and wires up the same helpers:
    /// </para>
    /// <code>
    /// // In a MotherOS test project — Program must implement IMotherProgram
    /// var script = new Script&lt;MotherOS.Program&gt;().Boot();
    /// </code>
    /// <para>
    /// For multi-script tests, join a <see cref="FakeIgcNetwork"/>. The network
    /// automatically cross-registers every booted script in every other script's
    /// Almanac, so grids can discover each other by name without any manual wiring:
    /// </para>
    /// <code>
    /// var network = new FakeIgcNetwork();
    ///
    /// var shipA = new Script("ShipA").OnNetwork(network).Boot();
    /// var shipB = new Script("ShipB").OnNetwork(network).Boot();
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
    /// public class MyScript : Script
    /// {
    ///     protected override void OnBeforeBoot(Mother mother)
    ///     {
    ///         new MyExtraModule(mother);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class Script<TProgram> : IScript
        where TProgram : MyGridProgram, new()
    {
        Mother _mother;
        string _customData;
        string _storage;
        IMyIntergridCommunicationSystem _igc;
        readonly List<BaseModuleCommand> _commands = new List<BaseModuleCommand>();
        FakeIgcNetwork _network;
        readonly string _gridName;
        PrintCapture _printCapture;

        /// <param name="gridName">
        /// Optional grid name for this script. Sets <see cref="Mother.Name"/> before boot
        /// and is used as the address other scripts use to reach this one on a
        /// <see cref="FakeIgcNetwork"/>. When omitted, falls back to the cube grid's
        /// <c>CustomName</c> and then to <c>"grid-{Mother.Id}"</c>.
        /// </param>
        public Script(string gridName = null)
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
        /// Useful for accessing modules not directly exposed by the script.
        /// </summary>
        public Mother Mother => _mother;

        /// <summary>
        /// The booted <typeparamref name="TProgram"/> instance.
        /// Use this to access script-specific state after boot.
        /// Available after <see cref="Boot"/> is called.
        /// </summary>
        public TProgram Program { get; private set; }

        /// <summary>
        /// The IGC in use by this script's program.
        /// When joined to a <see cref="FakeIgcNetwork"/> this is the script's
        /// <see cref="FakeIgc"/>; use <see cref="NetworkIGC"/> for the typed reference.
        /// </summary>
        public IMyIntergridCommunicationSystem IGC => _mother.IGC;

        /// <summary>
        /// The typed <see cref="FakeIgc"/> for this script.
        /// Non-null only when the script was joined to a <see cref="FakeIgcNetwork"/>
        /// via <see cref="OnNetwork"/>.
        /// </summary>
        public FakeIgc NetworkIGC { get; private set; }

        /// <summary>
        /// Injects a pre-created IGC into this script's program. Use this when you
        /// need an explicit <see cref="FakeIgc"/> reference before calling
        /// <see cref="Boot"/>. When also joined to a <see cref="FakeIgcNetwork"/> via
        /// <see cref="OnNetwork"/>, the network-allocated IGC takes precedence.
        /// </summary>
        public Script<TProgram> WithIGC(IMyIntergridCommunicationSystem igc)
        {
            _igc = igc;
            return this;
        }

        /// <summary>
        /// Joins this script to a <see cref="FakeIgcNetwork"/>. A <see cref="FakeIgc"/>
        /// will be allocated from the network and injected into this script's
        /// <c>Program</c> during <see cref="Boot"/>. After boot, the script is
        /// automatically cross-registered in every other booted script's Almanac
        /// under the grid name supplied to the constructor (or the derived fallback).
        /// </summary>
        public Script<TProgram> OnNetwork(FakeIgcNetwork network)
        {
            _network = network;
            return this;
        }

        /// <summary>
        /// Sets the programmable block's <c>CustomData</c> before boot.
        /// Use <see cref="CustomDataComposer"/> to construct the INI string.
        /// </summary>
        public Script<TProgram> WithCustomData(string customData)
        {
            _customData = customData;
            return this;
        }

        /// <summary>
        /// Sets the programmable block's <c>Storage</c> before boot.
        /// Use this to verify persistence and module boot behavior that depends on
        /// previously saved state.
        /// </summary>
        public Script<TProgram> WithStorage(string storage)
        {
            _storage = storage;
            return this;
        }

        /// <summary>
        /// Registers one or more commands with the <see cref="CommandBus"/> after boot.
        /// </summary>
        public Script<TProgram> WithCommands(params BaseModuleCommand[] commands)
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
        /// Runs one <c>Mother.Run</c> cycle, mirroring a real
        /// <c>Program.Main(argument, updateType)</c> call.
        /// Returns <c>this</c> for chaining. Must be called after <see cref="Boot"/>.
        /// </summary>
        public Script<TProgram> Run(UpdateType updateType, string argument = "")
        {
            _mother.Run(argument, updateType);
            return this;
        }

        /// <summary>
        /// Starts capturing output written via <c>Program.Echo</c>.
        /// Returns the <see cref="PrintCapture"/> so assertions can be made against it.
        /// Subsequent calls return the same capture instance.
        /// Must be called after <see cref="Boot"/>.
        /// </summary>
        public PrintCapture CaptureEcho()
        {
            if (_printCapture == null)
                _printCapture = new PrintCapture(this);
            return _printCapture;
        }

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
        /// <see cref="FakeIgc"/> when joined to a network), extracts the
        /// <see cref="Mother"/> the constructor created, applies
        /// <c>CustomData</c>, calls <see cref="OnBeforeBoot"/>, boots all
        /// registered modules, and — if on a network — cross-registers this
        /// script in every other booted script's Almanac.
        /// Returns <c>this</c> so the call can be chained inline.
        /// </summary>
        public Script<TProgram> Boot()
        {
            var builder = ProgramFactory.CreateProgram<TProgram>();

            if (_network != null)
            {
                NetworkIGC = _network.AllocateEndpoint();
                builder = builder.WithIgc(NetworkIGC);
            }
            else if (_igc != null)
            {
                builder = builder.WithIgc(_igc);
            }

            if (_storage != null)
                builder = builder.WithStorage(_storage);

            TProgram program = builder.Build();

            Program = program;
            _mother = FindMother(program);

            if (_customData != null)
                _mother.ProgrammableBlock.CustomData = _customData;

            OnBeforeBoot(_mother);

            // Delegate to Mother's own boot sequence, then drive the clock tick by tick
            // until the system reaches WORKING state. We stop at WORKING rather than
            // RunToIdle() so persistent module coroutines (e.g. BlockCatalogue refresh)
            // are not over-driven and do not interfere with per-test clock assertions.
            _mother.Boot();

            var clock = _mother.GetModule<Clock>();

            for (int i = 0; i < 500 && _mother.SystemState != Mother.SystemStates.WORKING; i++)
                clock.Run();

            if (!string.IsNullOrEmpty(_gridName))
                _mother.Name = _gridName;

            else if (string.IsNullOrEmpty(_mother.Name))
                _mother.Name = $"grid-{_mother.Id}";

            foreach (var command in _commands)
                _mother.GetModule<CommandBus>().RegisterCommand(command);

            Config = _mother.GetModule<Configuration>();
            Bus = _mother.GetModule<CommandBus>();
            Clock = new ClockDriver(clock);

            _network?.RegisterSession(this, _mother.Name);

            return this;
        }
    }

    /// <summary>
    /// Convenience alias for <see cref="Script{TProgram}"/> that targets the
    /// built-in MotherCore test <see cref="CoreTestProgram"/>.
    /// </summary>
    public class Script : Script<CoreTestProgram>
    {
        /// <inheritdoc cref="Script{TProgram}(string)"/>
        public Script(string gridName = null) : base(gridName) { }

        /// <inheritdoc cref="Script{TProgram}.WithIGC"/>
        public new Script WithIGC(IMyIntergridCommunicationSystem igc)
        {
            base.WithIGC(igc);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.OnNetwork"/>
        public new Script OnNetwork(FakeIgcNetwork network)
        {
            base.OnNetwork(network);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithCustomData"/>
        public new Script WithCustomData(string customData)
        {
            base.WithCustomData(customData);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithStorage"/>
        public new Script WithStorage(string storage)
        {
            base.WithStorage(storage);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithCommands"/>
        public new Script WithCommands(params BaseModuleCommand[] commands)
        {
            base.WithCommands(commands);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.Boot"/>
        public new Script Boot()
        {
            base.Boot();
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.Run"/>
        public new Script Run(UpdateType updateType, string argument = "")
        {
            base.Run(updateType, argument);
            return this;
        }
    }
}
