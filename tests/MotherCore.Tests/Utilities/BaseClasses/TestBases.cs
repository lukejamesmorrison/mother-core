using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Common test base that exposes fluent script factory helpers without per-file imports.
    /// </summary>
    public abstract class TestBase
    {
        protected static WorldFactory WorldFactory()
        {
            return new WorldFactory();
        }

        protected static ScriptBuilder<CoreTestProgram> ScriptFactory(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
        {
            return ScriptFactories.ScriptFactory(gridName, primaryGrid);
        }

        protected static ScriptBuilder<CoreTestProgram> ScriptFactory(
            IMyCubeGrid primaryGrid,
            string gridName = null)
        {
            return ScriptFactories.ScriptFactory(primaryGrid, gridName);
        }

        protected static ScriptBuilder<TProgram> ScriptFactory<TProgram>(
            string gridName = null,
            IMyCubeGrid primaryGrid = null)
            where TProgram : MyGridProgram, new()
        {
            return ScriptFactories.ScriptFactory<TProgram>(gridName, primaryGrid);
        }

        protected static ScriptBuilder<TProgram> ScriptFactory<TProgram>(
            IMyCubeGrid primaryGrid,
            string gridName = null)
            where TProgram : MyGridProgram, new()
        {
            return ScriptFactories.ScriptFactory<TProgram>(primaryGrid, gridName);
        }
    }

    /// <summary>
    /// Base class for integration tests that exercise a single booted script.
    /// Boots a fresh <see cref="Script{TProgram}"/> before each test and exposes
    /// <see cref="Mother"/> for direct access.
    /// </summary>
    /// <remarks>
    /// Use this base for Tests/Integration/ tests that exercise module
    /// behaviour, event firing, and command handling within one script instance.
    /// <code>
    /// public class MyModuleTests : ScriptTestBase&lt;CoreTestProgram&gt;
    /// {
    ///     [Test]
    ///     public void SomeCommand_DoesExpectedThing()
    ///     {
    ///         Bus.RunTerminalCommand("someCmd");
    ///         Clock.RunToIdle();
    ///
    ///         Assert.That(...);
    ///     }
    /// }
    /// </code>
    /// Override <see cref="SetUp"/> (calling <c>base.SetUp()</c>) to inject
    /// custom data, extra commands, or other per-test configuration.
    /// </remarks>
    /// <typeparam name="TProgram">
    /// The script's <c>Program</c> type. Use <see cref="CoreTestProgram"/> for
    /// MotherCore-only tests; use a real script's <c>Program</c> for targeted
    /// module tests inside that script.
    /// </typeparam>
    public abstract class ScriptTestBase<TProgram> : TestBase
        where TProgram : MyGridProgram, new()
    {
        /// <summary>The booted script. Available after <see cref="SetUp"/>.</summary>
        protected Script<TProgram> Script { get; private set; }

        /// <summary>The booted <see cref="Mother"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected Mother Mother => Script.Mother;

        /// <summary>The booted <typeparamref name="TProgram"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected TProgram Program => Script.Program;

        /// <summary>Boots a fresh script before each test.</summary>
        [SetUp]
        public virtual void SetUp()
        {
            Script = ScriptFactory<TProgram>().Boot();
        }
    }

    /// <summary>
    /// Base class for integration tests that exercise one real script instance
    /// and assert on its printed output. Boots a fresh <see cref="Script{TProgram}"/>
    /// before each test and wires up <see cref="Echo"/> capture automatically.
    /// </summary>
    /// <remarks>
    /// Use this base for Tests/Integration/ tests that need to assert on
    /// terminal output (e.g. help text, status messages).
    /// <code>
    /// public class MyFeatureTests : ScriptFeatureTestBase&lt;CoreTestProgram&gt;
    /// {
    ///     [Test]
    ///     public void Command_PrintsExpectedOutput()
    ///     {
    ///         Script.Run(UpdateType.Terminal, "help");
    ///
    ///         Echo.AssertPrinted("Available commands");
    ///     }
    /// }
    /// </code>
    /// </remarks>
    /// <typeparam name="TProgram">
    /// The script's <c>Program</c> type. Use <see cref="CoreTestProgram"/> for
    /// MotherCore-only tests; use a real script's <c>Program</c> for feature tests.
    /// </typeparam>
    public abstract class ScriptFeatureTestBase<TProgram> : TestBase
        where TProgram : MyGridProgram, new()
    {
        /// <summary>The booted script. Available after <see cref="SetUp"/>.</summary>
        protected Script<TProgram> Script { get; private set; }

        /// <summary>The booted <see cref="Mother"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected Mother Mother => Script.Mother;

        /// <summary>The booted <typeparamref name="TProgram"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected TProgram Program => Script.Program;

        /// <summary>
        /// Echo capture for the booted script. Available after <see cref="SetUp"/>.
        /// Use <c>Echo.Contains("...")</c> or <c>Echo.AssertPrinted("...")</c>
        /// to assert on output.
        /// </summary>
        protected PrintCapture Echo { get; private set; }

        /// <summary>Boots a fresh script before each test and exposes its baked-in echo capture.</summary>
        [SetUp]
        public virtual void SetUp()
        {
            Script = ScriptFactory<TProgram>().Boot();
            Echo = Script.CaptureEcho();
        }
    }

    /// <summary>
    /// Base class for integration tests that involve multiple scripts running inside
    /// one shared <see cref="World"/>. Creates a fresh world before each test.
    /// </summary>
    /// <remarks>
    /// Use this base for Tests/Integration/World/ tests that exercise cross-script
    /// communication via the <see cref="IntergridMessageService"/>.
    /// <code>
    /// public class MultiScriptTests : WorldTestBase
    /// {
    ///     [Test]
    ///     public void ShipA_Can_Send_Command_To_ShipB()
    ///     {
    ///         var shipA = World.CreateScript&lt;CoreTestProgram&gt;("ShipA").Boot();
    ///         var shipB = World.CreateScript&lt;CoreTestProgram&gt;("ShipB").Boot();
    ///
    ///         shipA.Bus.RunTerminalCommand("@ShipB help");
    ///         shipA.Clock.RunToIdle();
    ///         World.DispatchIgc();
    ///         World.RunIGC();
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public abstract class WorldTestBase : TestBase
    {
        /// <summary>The shared test world. Available after <see cref="SetUp"/>.</summary>
        protected World World { get; private set; }

        /// <summary>Creates a fresh <see cref="World"/> before each test.</summary>
        [SetUp]
        public virtual void SetUp()
        {
            World = WorldFactory().Boot();
        }
    }

    /// <summary>
    /// Base class for module-layer tests that exercise a concrete booted module.
    /// </summary>
    /// <typeparam name="TProgram">The script Program type used for boot.</typeparam>
    /// <typeparam name="TModule">The concrete module under test.</typeparam>
    public abstract class ModuleTestBase<TProgram, TModule> : TestBase
        where TProgram : MyGridProgram, new()
        where TModule : BaseModule
    {
        /// <summary>
        /// The module fixture used to configure script boot inputs.
        /// </summary>
        protected Module<TModule, TProgram> ModuleFixture { get; private set; }

        /// <summary>
        /// The booted concrete module under test.
        /// </summary>
        protected TModule ModuleUnderTest { get; private set; }

        /// <summary>
        /// The booted script fixture backing this module test.
        /// </summary>
        protected Script<TProgram> Script => ModuleFixture.Script;

        /// <summary>
        /// The booted Mother instance backing this module test.
        /// </summary>
        protected Mother Mother => Script.Mother;

        /// <summary>
        /// The booted Program instance backing this module test.
        /// </summary>
        protected TProgram Program => Script.Program;

        /// <summary>
        /// Boots a fresh module fixture before each test.
        /// </summary>
        [SetUp]
        public virtual void SetUp()
        {
            ModuleFixture = ConfigureModule(new Module<TModule, TProgram>());
            ModuleUnderTest = ModuleFixture.Boot();
        }

        /// <summary>
        /// Override to customize module fixture inputs before boot.
        /// </summary>
        protected virtual Module<TModule, TProgram> ConfigureModule(Module<TModule, TProgram> module)
        {
            return module;
        }
    }

    /// <summary>
    /// Base class for command-layer tests that execute commands against one booted script.
    /// </summary>
    /// <typeparam name="TProgram">The script Program type used for boot.</typeparam>
    public abstract class CommandTestBase<TProgram> : TestBase
        where TProgram : MyGridProgram, new()
    {
        /// <summary>
        /// The booted script used for command execution.
        /// </summary>
        protected Script<TProgram> Script { get; private set; }

        /// <summary>
        /// The booted Mother instance backing this command test.
        /// </summary>
        protected Mother Mother => Script.Mother;

        /// <summary>
        /// The booted Program instance backing this command test.
        /// </summary>
        protected TProgram Program => Script.Program;

        /// <summary>
        /// Boots a fresh script fixture before each test.
        /// </summary>
        [SetUp]
        public virtual void SetUp()
        {
            Script = ConfigureScript(ScriptFactory<TProgram>().Create()).Boot();
        }

        /// <summary>
        /// Override to customize script inputs before boot.
        /// </summary>
        protected virtual Script<TProgram> ConfigureScript(Script<TProgram> script)
        {
            return script;
        }

        /// <summary>
        /// Creates a typed command fixture bound to this base's booted script.
        /// </summary>
        protected Command<TCommand, TProgram> Command<TCommand>()
            where TCommand : BaseModuleCommand
        {
            return new Command<TCommand, TProgram>(Script).Boot();
        }
    }
}
