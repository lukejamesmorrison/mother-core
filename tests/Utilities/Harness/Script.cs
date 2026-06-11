using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Reflection;
using VRage.Game.ModAPI.Ingame;
using System.Linq;
using VRage.Game.ModAPI.Ingame.Utilities;

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
        FakeIgcNetwork _defaultNetwork;
        readonly string _gridName;
        readonly FakeGridTerminalSystem _gridTerminalSystem;
        PrintCapture _printCapture;
        bool _ensureDefaultPublicChannel;

        const string ChannelsSectionName = "channels";
        const string PublicChannelName = "*";
        /// <param name="gridName">
        /// Optional grid name for this script. Sets <see cref="Mother.Name"/> before boot
        /// and is used as the address other scripts use to reach this one on a
        /// <see cref="FakeIgcNetwork"/>. When omitted, falls back to the cube grid's
        /// <c>CustomName</c> and then to <c>"grid-{Mother.Id}"</c>.
        /// </param>
        public Script(string gridName = null)
        {
            _gridName = gridName;
            _gridTerminalSystem = new FakeGridTerminalSystem(gridName ?? "Test Grid");
        }

        /// <summary>
        /// Initializes a new script harness bound to a specific primary grid.
        /// The harness will attach the programmable block to this grid before boot.
        /// </summary>
        /// <param name="primaryGrid">The grid that should own the programmable block.</param>
        /// <param name="gridName">
        /// Optional logical script name used for Mother.Name and network addressing.
        /// When omitted, the supplied grid's CustomName remains the natural label.
        /// </param>
        public Script(IMyCubeGrid primaryGrid, string gridName = null)
        {
            if (primaryGrid == null)
                throw new ArgumentNullException(nameof(primaryGrid));

            _gridName = gridName;
            _gridTerminalSystem = new FakeGridTerminalSystem(primaryGrid);
        }

        internal Script(FakeGridTerminalSystem gridTerminalSystem, string gridName = null)
        {
            _gridTerminalSystem = gridTerminalSystem
                ?? throw new ArgumentNullException(nameof(gridTerminalSystem));

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
        /// The script-local grid terminal system used during boot.
        /// Tests can populate it before boot with block and group fixtures.
        /// </summary>
        public IMyGridTerminalSystem GridTerminalSystem => _gridTerminalSystem;

        /// <summary>
        /// The primary grid that owns the programmable block for this script.
        /// Additional grids created through <see cref="CreateGrid"/> are automatically
        /// connected to this grid as part of the same construct.
        /// </summary>
        public IMyCubeGrid PrimaryGrid => _gridTerminalSystem.PrimaryGrid;

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

        internal Script<TProgram> WithDefaultNetwork(FakeIgcNetwork network)
        {
            _defaultNetwork = network;
            return this;
        }

        /// <summary>
        /// Joins this script to the default network when available.
        /// If no default network is configured (for example outside <see cref="TestWorld"/>),
        /// a private network is created.
        /// </summary>
        public Script<TProgram> OnNetwork()
        {
            return OnNetwork(_defaultNetwork ?? new FakeIgcNetwork());
        }

        /// <summary>
        /// Joins this script to a <see cref="FakeIgcNetwork"/>. A <see cref="FakeIgc"/>
        /// will be allocated from the network and injected into this script's
        /// <c>Program</c> during <see cref="Boot"/>. After boot, the script is
        /// automatically cross-registered in every other booted script's Almanac
        /// under the grid name supplied to the constructor (or the derived fallback).
        ///
        /// When no explicit channel configuration is supplied via
        /// <see cref="WithCustomData"/>, the harness ensures a default public channel
        /// entry (<c>[channels]</c> / <c>*=</c>) so scripts can communicate immediately.
        /// </summary>
        public Script<TProgram> OnNetwork(FakeIgcNetwork network)
        {
            _network = network;
            _ensureDefaultPublicChannel = true;
            return this;
        }

        string BuildEffectiveCustomData()
        {
            if (!_ensureDefaultPublicChannel)
                return _customData;

            var ini = new MyIni();

            if (!string.IsNullOrWhiteSpace(_customData))
                ini.TryParse(_customData);

            var channelKeys = new List<MyIniKey>();
            ini.GetKeys(ChannelsSectionName, channelKeys);

            if (channelKeys.Count == 0)
                ini.Set(ChannelsSectionName, PublicChannelName, string.Empty);

            return ini.ToString();
        }

        void SyncConstructCommandsOnBoot()
        {
            if (_network == null)
                return;

            var selfBus = _mother.GetModule<CommandBus>();
            var selfCommands = selfBus.GetSelfCommandNames();

            foreach (var session in _network.Scripts)
            {
                if (ReferenceEquals(session, this))
                    continue;

                if (!session.Mother.CubeGrid.IsSameConstructAs(_mother.CubeGrid))
                    continue;

                var remoteBus = session.Mother.GetModule<CommandBus>();
                var remoteCommands = remoteBus.GetSelfCommandNames();

                // Mirror construct sync results immediately after boot so tests
                // start from a construct-aware state without requiring manual
                // scheduler priming.
                selfBus.RegisterRemoteCommands(session.Mother.Id, remoteCommands);
                remoteBus.RegisterRemoteCommands(_mother.Id, selfCommands);
            }
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
        /// Creates an additional grid for this script's construct and automatically
        /// links it back to the primary grid through a synthetic mechanical block.
        /// </summary>
        public IMyCubeGrid CreateGrid(
            string gridName = null,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            return _gridTerminalSystem.CreateGrid(gridName, entityId, connectionKind);
        }

        /// <summary>
        /// Creates and connects an additional grid for this script's construct.
        /// Use this overload when the test only cares that the subgrid exists.
        /// </summary>
        public Script<TProgram> WithGrid(
            string gridName,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            CreateGrid(gridName, entityId, connectionKind);
            return this;
        }

        /// <summary>
        /// Connects an existing grid instance into this script's construct.
        /// Use this overload when the test wants to keep a grid variable for later assertions.
        /// </summary>
        public Script<TProgram> WithGrid(
            IMyCubeGrid grid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            ConnectGrids(PrimaryGrid, grid, connectionKind);
            return this;
        }

        /// <summary>
        /// Explicitly connects two grids in this script's construct through a
        /// fake rotor, hinge, or piston base block.
        /// </summary>
        public IMyMechanicalConnectionBlock ConnectGrids(
            IMyCubeGrid baseGrid,
            IMyCubeGrid topGrid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            return _gridTerminalSystem.ConnectGrids(baseGrid, topGrid, connectionKind);
        }

        /// <summary>
        /// Connects two grids with a paired connector link without making them the same construct.
        /// </summary>
        public IMyShipConnector ConnectGridsViaConnector(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            string baseConnectorName = null,
            string otherConnectorName = null,
            MyShipConnectorStatus initialStatus = MyShipConnectorStatus.Connected)
        {
            return _gridTerminalSystem.ConnectGridsViaConnector(
                baseGrid,
                otherGrid,
                baseConnectorName,
                otherConnectorName,
                initialStatus);
        }

        /// <summary>
        /// Connects two grids with a paired merge-block link.
        /// Locking the returned merge block rewrites the absorbed side's block grid
        /// references onto the surviving grid; unlocking restores them.
        /// </summary>
        public IMyShipMergeBlock ConnectGridsViaMergeBlock(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            string baseMergeBlockName = null,
            string otherMergeBlockName = null,
            MergeState initialState = MergeState.None)
        {
            return _gridTerminalSystem.ConnectGridsViaMergeBlock(
                baseGrid,
                otherGrid,
                baseMergeBlockName,
                otherMergeBlockName,
                initialState);
        }

        /// <summary>
        /// Forces a paired merge-block link into the merged state.
        /// This simulates both sides being powered and close enough to lock.
        /// </summary>
        public Script<TProgram> MergeBlocks(IMyShipMergeBlock mergeBlock)
        {
            _gridTerminalSystem.MergeBlocks(mergeBlock);
            return this;
        }

        /// <summary>
        /// Forces a paired merge-block link out of the merged state by turning off
        /// the supplied side of the pair.
        /// </summary>
        public Script<TProgram> UnmergeBlocks(IMyShipMergeBlock mergeBlock)
        {
            _gridTerminalSystem.UnmergeBlocks(mergeBlock);
            return this;
        }

        /// <summary>
        /// Finds the mechanical connection whose top grid matches <paramref name="topGrid"/>.
        /// This keeps individual tests from querying the grid terminal system directly.
        /// </summary>
        public IMyMechanicalConnectionBlock GetMechanicalConnectionTo(IMyCubeGrid topGrid)
        {
            if (topGrid == null)
                throw new ArgumentNullException(nameof(topGrid));

            var mechanicalBlocks = new List<IMyMechanicalConnectionBlock>();
            _gridTerminalSystem.GetBlocksOfType(mechanicalBlocks);

            return mechanicalBlocks.Single(block =>
                block.TopGrid != null && block.TopGrid.EntityId == topGrid.EntityId);
        }

        /// <summary>
        /// Detaches the mechanical connection whose top grid matches <paramref name="topGrid"/>.
        /// Returns <c>this</c> for fluent test arrangement.
        /// </summary>
        public Script<TProgram> DetachGrid(IMyCubeGrid topGrid)
        {
            GetMechanicalConnectionTo(topGrid).Detach();
            return this;
        }

        /// <summary>
        /// Registers a block on the script's primary grid.
        /// </summary>
        public Script<TProgram> WithBlock(IMyTerminalBlock block)
        {
            return WithBlock(block, PrimaryGrid);
        }

        /// <summary>
        /// Registers a block on a specific grid within the script's construct.
        /// </summary>
        public Script<TProgram> WithBlock(IMyTerminalBlock block, IMyCubeGrid grid)
        {
            _gridTerminalSystem.AddBlock(block, grid);
            return this;
        }

        /// <summary>
        /// Creates and registers a block using the harness factory so tests can
        /// arrange script-local topology without a separate factory call.
        /// </summary>
        public TBlock WithBlock<TBlock>(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid grid = null,
            Action<TBlock> configure = null)
            where TBlock : class, IMyTerminalBlock
        {
            var block = TerminalBlockFactory.Create(
                customName: customName,
                customData: customData,
                entityId: entityId,
                grid: grid,
                configure: configure);

            WithBlock(block, grid);
            return block;
        }

        /// <summary>
        /// Registers multiple blocks on the script's primary grid.
        /// </summary>
        public Script<TProgram> WithBlocks(params IMyTerminalBlock[] blocks)
        {
            if (blocks == null)
                throw new ArgumentNullException(nameof(blocks));

            foreach (var block in blocks)
                WithBlock(block);

            return this;
        }

        /// <summary>
        /// Registers a named block group in the script-local terminal system.
        /// </summary>
        public Script<TProgram> WithBlockGroup(string groupName, params IMyTerminalBlock[] blocks)
        {
            _gridTerminalSystem.AddBlockGroup(groupName, blocks);
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
        /// Runs one <c>Mother.Run</c> cycle using argument-first ordering,
        /// which reads closer to user-entered command flow.
        /// Returns <c>this</c> for chaining.
        /// </summary>
        public Script<TProgram> Run(string argument, UpdateType updateType)
        {
            return Run(updateType, argument);
        }

        /// <summary>
        /// Runs one terminal update cycle as if a player entered
        /// <paramref name="argument"/> in the PB terminal.
        /// </summary>
        public Script<TProgram> RunTerminal(string argument = "")
        {
            return Run(UpdateType.Terminal, argument);
        }

        /// <summary>
        /// Runs one trigger update cycle as if a button/action executed this PB.
        /// </summary>
        public Script<TProgram> RunTrigger(string argument = "")
        {
            return Run(UpdateType.Trigger, argument);
        }

        /// <summary>
        /// Advances this script's clock by one cycle.
        /// </summary>
        public Script<TProgram> Tick()
        {
            Clock.Tick();
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        void IScript.Tick()
        {
            Tick();
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
        /// Asserts that the script printed a line containing <paramref name="fragment"/>.
        /// Echo capture is provisioned automatically during boot.
        /// </summary>
        public void AssertPrinted(string fragment)
        {
            CaptureEcho().AssertPrinted(fragment);
        }

        /// <summary>
        /// Asserts that this script reached the WORKING state after boot.
        /// </summary>
        public void ShouldBeWorking()
        {
            Assert.That(_mother, Is.Not.Null, "Expected a booted script, but Mother was null.");
            Assert.That(_mother.SystemState, Is.EqualTo(Mother.SystemStates.WORKING),
                $"Expected script '{_mother.Name}' to be WORKING, but state was '{_mother.SystemState}'.");
        }

        /// <summary>
        /// Asserts that this script's runtime name matches <paramref name="expected"/>.
        /// </summary>
        public void ShouldHaveName(string expected)
        {
            Assert.That(_mother, Is.Not.Null, "Expected a booted script, but Mother was null.");
            Assert.That(_mother.Name, Is.EqualTo(expected),
                $"Expected script name '{expected}', but was '{_mother.Name}'.");
        }

        /// <summary>
        /// Asserts that the command bus executed a command with the expected outcome count.
        /// </summary>
        public void ShouldHaveExecuted(
            string commandName,
            CommandExecutionOutcome outcome = CommandExecutionOutcome.ModuleExecuted,
            int count = 1)
        {
            AssertCommandExecuted(commandName, count, outcome);
        }

        /// <summary>
        /// Asserts that the captured echo output contains <paramref name="fragment"/>.
        /// </summary>
        public void ShouldHavePrinted(string fragment)
        {
            CaptureEcho().AssertPrinted(fragment);
        }

        /// <summary>
        /// Asserts that the captured echo output does not contain <paramref name="fragment"/>.
        /// </summary>
        public void ShouldNotHavePrinted(string fragment)
        {
            Assert.That(CaptureEcho().Contains(fragment), Is.False,
                $"Expected output for script '{_mother?.Name ?? _gridName ?? "Unknown"}' to not contain '{fragment}', but it was found.");
        }

        /// <summary>
        /// Asserts that this script's Almanac knows a grid by the given name.
        /// </summary>
        public void ShouldKnowGrid(string gridName)
        {
            Assert.That(_mother, Is.Not.Null, "Expected a booted script, but Mother was null.");

            var almanac = _mother.GetModule<Almanac>();

            Assert.That(almanac, Is.Not.Null,
                $"Expected script '{_mother.Name}' to have Almanac loaded, but Almanac was null.");
            Assert.That(almanac.GetRecord(gridName), Is.Not.Null,
                $"Expected script '{_mother.Name}' to know grid '{gridName}' in Almanac, but no record was found.");
        }

        /// <summary>
        /// Looks up a registered block by custom or display name.
        /// </summary>
        public IMyTerminalBlock GetBlock(string blockName)
        {
            return _gridTerminalSystem.GetBlockWithName(blockName);
        }

        /// <summary>
        /// Looks up a strongly typed registered block by custom or display name.
        /// </summary>
        public TBlock GetBlock<TBlock>(string blockName)
            where TBlock : class, IMyTerminalBlock
        {
            return GetBlock(blockName) as TBlock;
        }

        /// <summary>
        /// Reports whether a block with the supplied name is registered on this script's terminal system.
        /// </summary>
        public bool ContainsBlock(string blockName)
        {
            return GetBlock(blockName) != null;
        }

        /// <summary>
        /// Assertion helper for common script-local block registration checks.
        /// </summary>
        public void AssertHasBlock(string blockName)
        {
            Assert.That(ContainsBlock(blockName), Is.True,
                $"Expected script '{_mother?.Name ?? _gridName ?? PrimaryGrid?.CustomName ?? "Unknown"}' to contain block '{blockName}', but it was not registered.");
        }

        /// <summary>
        /// Asserts that a block group with the given name exists.
        /// </summary>
        public void ShouldContainGroup(string groupName)
        {
            var group = GridTerminalSystem.GetBlockGroupWithName(groupName);

            Assert.That(group, Is.Not.Null,
                $"Expected script '{_mother?.Name ?? _gridName ?? "Unknown"}' to contain block group '{groupName}', but it was not found.");
        }

        /// <summary>
        /// Asserts that a named block group contains the supplied block names.
        /// </summary>
        public void ShouldGroupContainBlocks(string groupName, params string[] blockNames)
        {
            var group = GridTerminalSystem.GetBlockGroupWithName(groupName);

            Assert.That(group, Is.Not.Null,
                $"Expected block group '{groupName}' to exist, but it was not found.");

            var groupBlocks = new List<IMyTerminalBlock>();
            group.GetBlocks(groupBlocks);

            foreach (var blockName in blockNames ?? new string[0])
            {
                Assert.That(
                    groupBlocks.Any(block =>
                        string.Equals(block.CustomName, blockName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(block.DisplayNameText, blockName, StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"Expected block group '{groupName}' to contain block '{blockName}', but it was not present.");
            }
        }

        /// <summary>
        /// Asserts that two named blocks are part of the same construct.
        /// </summary>
        public void ShouldBeSameConstruct(string firstBlockName, string secondBlockName)
        {
            var first = GetBlock(firstBlockName);
            var second = GetBlock(secondBlockName);

            Assert.That(first, Is.Not.Null,
                $"Expected block '{firstBlockName}' to exist, but it was not found.");
            Assert.That(second, Is.Not.Null,
                $"Expected block '{secondBlockName}' to exist, but it was not found.");
            Assert.That(first.IsSameConstructAs(second), Is.True,
                $"Expected blocks '{firstBlockName}' and '{secondBlockName}' to be on the same construct, but they were not.");
        }

        /// <summary>
        /// Asserts that two named blocks are not part of the same construct.
        /// </summary>
        public void ShouldNotBeSameConstruct(string firstBlockName, string secondBlockName)
        {
            var first = GetBlock(firstBlockName);
            var second = GetBlock(secondBlockName);

            Assert.That(first, Is.Not.Null,
                $"Expected block '{firstBlockName}' to exist, but it was not found.");
            Assert.That(second, Is.Not.Null,
                $"Expected block '{secondBlockName}' to exist, but it was not found.");
            Assert.That(first.IsSameConstructAs(second), Is.False,
                $"Expected blocks '{firstBlockName}' and '{secondBlockName}' to be on separate constructs, but they were on the same construct.");
        }

        /// <summary>
        /// Asserts that a named merge block is in the expected merge state.
        /// </summary>
        public void ShouldHaveMergeState(string mergeBlockName, MergeState expectedState)
        {
            var mergeBlock = GetBlock<IMyShipMergeBlock>(mergeBlockName);

            Assert.That(mergeBlock, Is.Not.Null,
                $"Expected merge block '{mergeBlockName}' to exist, but it was not found.");
            Assert.That(mergeBlock.State, Is.EqualTo(expectedState),
                $"Expected merge block '{mergeBlockName}' to have state '{expectedState}', but was '{mergeBlock.State}'.");
        }

        /// <summary>
        /// Asserts that a named connector reports the expected connection status.
        /// </summary>
        public void ShouldHaveConnectorStatus(string connectorName, MyShipConnectorStatus expectedStatus)
        {
            var connector = GetBlock<IMyShipConnector>(connectorName);

            Assert.That(connector, Is.Not.Null,
                $"Expected connector '{connectorName}' to exist, but it was not found.");
            Assert.That(connector.Status, Is.EqualTo(expectedStatus),
                $"Expected connector '{connectorName}' to have status '{expectedStatus}', but was '{connector.Status}'.");
        }

        /// <summary>
        /// Asserts that an event of type <typeparamref name="TEvent"/> was emitted.
        /// </summary>
        public void AssertEventEmitted<TEvent>(int expectedCount = 1)
            where TEvent : IEvent
        {
            AssertEventEmitted<TEvent>(null, expectedCount);
        }

        /// <summary>
        /// Asserts that an event of type <typeparamref name="TEvent"/> was emitted
        /// and delivered to the supplied module.
        /// </summary>
        public void AssertEventEmitted<TEvent>(IModule module, int expectedCount = 1)
            where TEvent : IEvent
        {
            var eventBus = _mother.GetModule<EventBus>();
            var emissionCount = eventBus.Emissions.Count(emission =>
                emission.Event is TEvent
                && (module == null || emission.Recipients.Contains(module)));

            var expectation = module == null
                ? $"Expected {typeof(TEvent).Name} to be emitted {expectedCount} time(s)"
                : $"Expected {typeof(TEvent).Name} to be emitted to {module.GetModuleName()} {expectedCount} time(s)";

            Assert.That(emissionCount, Is.EqualTo(expectedCount),
                $"{expectation}, but saw {emissionCount}.");
        }

        /// <summary>
        /// Asserts that the booted <see cref="CommandBus"/> processed a concrete command
        /// with the expected outcome the specified number of times.
        /// </summary>
        public void AssertCommandExecuted(
            string commandName,
            int expectedCount = 1,
            CommandExecutionOutcome outcome = CommandExecutionOutcome.ModuleExecuted)
        {
            Assert.That(Bus, Is.Not.Null);

            var matchCount = Bus.GetExecutionCount(commandName, outcome);

            Assert.That(matchCount, Is.EqualTo(expectedCount),
                $"Expected command '{commandName}' with outcome '{outcome}' to appear {expectedCount} time(s), but saw {matchCount}.");
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
            var programmableBlock = ProgrammableBlockFactory.Create(cubeGrid: PrimaryGrid);

            _gridTerminalSystem.AddBlock(programmableBlock, PrimaryGrid);
            builder = builder
                .WithMe(programmableBlock)
                .WithGridTerminalSystem(_gridTerminalSystem);

            if (_network != null)
            {
                NetworkIGC = _network.CreateNetworkEndpoint();
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
            _printCapture = new PrintCapture(this);

            var effectiveCustomData = BuildEffectiveCustomData();
            if (effectiveCustomData != null)
                _mother.ProgrammableBlock.CustomData = effectiveCustomData;

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

            // Start each test with a clean echo buffer while still capturing
            // anything printed after boot without extra setup.
            _printCapture.Clear();

            _network?.RegisterScript(this, _mother.Name);
            SyncConstructCommandsOnBoot();

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

        /// <inheritdoc cref="Script{TProgram}.Script(IMyCubeGrid, string)"/>
        public Script(IMyCubeGrid primaryGrid, string gridName = null) : base(primaryGrid, gridName) { }

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

        /// <inheritdoc cref="Script{TProgram}.OnNetwork()"/>
        public new Script OnNetwork()
        {
            base.OnNetwork();
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

        /// <inheritdoc cref="Script{TProgram}.CreateGrid"/>
        public new IMyCubeGrid CreateGrid(
            string gridName = null,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            return base.CreateGrid(gridName, entityId, connectionKind);
        }

        /// <inheritdoc cref="Script{TProgram}.WithGrid(string, long?, MechanicalConnectionKind)"/>
        public new Script WithGrid(
            string gridName,
            long? entityId = null,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            base.WithGrid(gridName, entityId, connectionKind);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithGrid(IMyCubeGrid, MechanicalConnectionKind)"/>
        public new Script WithGrid(
            IMyCubeGrid grid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            base.WithGrid(grid, connectionKind);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.ConnectGrids"/>
        public new IMyMechanicalConnectionBlock ConnectGrids(
            IMyCubeGrid baseGrid,
            IMyCubeGrid topGrid,
            MechanicalConnectionKind connectionKind = MechanicalConnectionKind.Rotor)
        {
            return base.ConnectGrids(baseGrid, topGrid, connectionKind);
        }

        /// <inheritdoc cref="Script{TProgram}.ConnectGridsViaConnector"/>
        public new IMyShipConnector ConnectGridsViaConnector(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            string baseConnectorName = null,
            string otherConnectorName = null,
            MyShipConnectorStatus initialStatus = MyShipConnectorStatus.Connected)
        {
            return base.ConnectGridsViaConnector(
                baseGrid,
                otherGrid,
                baseConnectorName,
                otherConnectorName,
                initialStatus);
        }

        /// <inheritdoc cref="Script{TProgram}.ConnectGridsViaMergeBlock"/>
        public new IMyShipMergeBlock ConnectGridsViaMergeBlock(
            IMyCubeGrid baseGrid,
            IMyCubeGrid otherGrid,
            string baseMergeBlockName = null,
            string otherMergeBlockName = null,
            MergeState initialState = MergeState.None)
        {
            return base.ConnectGridsViaMergeBlock(
                baseGrid,
                otherGrid,
                baseMergeBlockName,
                otherMergeBlockName,
                initialState);
        }

        /// <inheritdoc cref="Script{TProgram}.MergeBlocks"/>
        public new Script MergeBlocks(IMyShipMergeBlock mergeBlock)
        {
            base.MergeBlocks(mergeBlock);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.UnmergeBlocks"/>
        public new Script UnmergeBlocks(IMyShipMergeBlock mergeBlock)
        {
            base.UnmergeBlocks(mergeBlock);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.GetMechanicalConnectionTo"/>
        public new IMyMechanicalConnectionBlock GetMechanicalConnectionTo(IMyCubeGrid topGrid)
        {
            return base.GetMechanicalConnectionTo(topGrid);
        }

        /// <inheritdoc cref="Script{TProgram}.DetachGrid"/>
        public new Script DetachGrid(IMyCubeGrid topGrid)
        {
            base.DetachGrid(topGrid);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithBlock(IMyTerminalBlock)"/>
        public new Script WithBlock(IMyTerminalBlock block)
        {
            base.WithBlock(block);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithBlock(IMyTerminalBlock, IMyCubeGrid)"/>
        public new Script WithBlock(IMyTerminalBlock block, IMyCubeGrid grid)
        {
            base.WithBlock(block, grid);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithBlock{TBlock}(string, string, long?, IMyCubeGrid, Action{TBlock})"/>
        public new TBlock WithBlock<TBlock>(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid grid = null,
            Action<TBlock> configure = null)
            where TBlock : class, IMyTerminalBlock
        {
            return base.WithBlock(customName, customData, entityId, grid, configure);
        }

        /// <inheritdoc cref="Script{TProgram}.WithBlocks"/>
        public new Script WithBlocks(params IMyTerminalBlock[] blocks)
        {
            base.WithBlocks(blocks);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.WithBlockGroup"/>
        public new Script WithBlockGroup(string groupName, params IMyTerminalBlock[] blocks)
        {
            base.WithBlockGroup(groupName, blocks);
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

        /// <inheritdoc cref="Script{TProgram}.Run(string, UpdateType)"/>
        public new Script Run(string argument, UpdateType updateType)
        {
            base.Run(argument, updateType);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.RunTerminal(string)"/>
        public new Script RunTerminal(string argument = "")
        {
            base.RunTerminal(argument);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.RunTrigger(string)"/>
        public new Script RunTrigger(string argument = "")
        {
            base.RunTrigger(argument);
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.Tick"/>
        public new Script Tick()
        {
            base.Tick();
            return this;
        }

        /// <inheritdoc cref="Script{TProgram}.AssertEventEmitted{TEvent}(int)"/>
        public new void AssertEventEmitted<TEvent>(int expectedCount = 1)
            where TEvent : IEvent
        {
            base.AssertEventEmitted<TEvent>(expectedCount);
        }

        /// <inheritdoc cref="Script{TProgram}.AssertEventEmitted{TEvent}(IModule, int)"/>
        public new void AssertEventEmitted<TEvent>(IModule module, int expectedCount = 1)
            where TEvent : IEvent
        {
            base.AssertEventEmitted<TEvent>(module, expectedCount);
        }

        /// <inheritdoc cref="Script{TProgram}.AssertCommandExecuted(string, int, CommandExecutionOutcome)"/>
        public new void AssertCommandExecuted(
            string commandName,
            int expectedCount = 1,
            CommandExecutionOutcome outcome = CommandExecutionOutcome.ModuleExecuted)
        {
            base.AssertCommandExecuted(commandName, expectedCount, outcome);
        }

        /// <inheritdoc cref="Script{TProgram}.AssertPrinted"/>
        public new void AssertPrinted(string fragment)
        {
            base.AssertPrinted(fragment);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldBeWorking"/>
        public new void ShouldBeWorking()
        {
            base.ShouldBeWorking();
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldHaveName"/>
        public new void ShouldHaveName(string expected)
        {
            base.ShouldHaveName(expected);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldHaveExecuted"/>
        public new void ShouldHaveExecuted(
            string commandName,
            CommandExecutionOutcome outcome = CommandExecutionOutcome.ModuleExecuted,
            int count = 1)
        {
            base.ShouldHaveExecuted(commandName, outcome, count);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldHavePrinted"/>
        public new void ShouldHavePrinted(string fragment)
        {
            base.ShouldHavePrinted(fragment);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldNotHavePrinted"/>
        public new void ShouldNotHavePrinted(string fragment)
        {
            base.ShouldNotHavePrinted(fragment);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldKnowGrid"/>
        public new void ShouldKnowGrid(string gridName)
        {
            base.ShouldKnowGrid(gridName);
        }

        /// <inheritdoc cref="Script{TProgram}.GetBlock(string)"/>
        public new IMyTerminalBlock GetBlock(string blockName)
        {
            return base.GetBlock(blockName);
        }

        /// <inheritdoc cref="Script{TProgram}.GetBlock{TBlock}(string)"/>
        public new TBlock GetBlock<TBlock>(string blockName)
            where TBlock : class, IMyTerminalBlock
        {
            return base.GetBlock<TBlock>(blockName);
        }

        /// <inheritdoc cref="Script{TProgram}.ContainsBlock(string)"/>
        public new bool ContainsBlock(string blockName)
        {
            return base.ContainsBlock(blockName);
        }

        /// <inheritdoc cref="Script{TProgram}.AssertHasBlock(string)"/>
        public new void AssertHasBlock(string blockName)
        {
            base.AssertHasBlock(blockName);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldContainGroup"/>
        public new void ShouldContainGroup(string groupName)
        {
            base.ShouldContainGroup(groupName);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldGroupContainBlocks"/>
        public new void ShouldGroupContainBlocks(string groupName, params string[] blockNames)
        {
            base.ShouldGroupContainBlocks(groupName, blockNames);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldBeSameConstruct"/>
        public new void ShouldBeSameConstruct(string firstBlockName, string secondBlockName)
        {
            base.ShouldBeSameConstruct(firstBlockName, secondBlockName);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldNotBeSameConstruct"/>
        public new void ShouldNotBeSameConstruct(string firstBlockName, string secondBlockName)
        {
            base.ShouldNotBeSameConstruct(firstBlockName, secondBlockName);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldHaveMergeState"/>
        public new void ShouldHaveMergeState(string mergeBlockName, MergeState expectedState)
        {
            base.ShouldHaveMergeState(mergeBlockName, expectedState);
        }

        /// <inheritdoc cref="Script{TProgram}.ShouldHaveConnectorStatus"/>
        public new void ShouldHaveConnectorStatus(string connectorName, MyShipConnectorStatus expectedStatus)
        {
            base.ShouldHaveConnectorStatus(connectorName, expectedStatus);
        }

    }
}
