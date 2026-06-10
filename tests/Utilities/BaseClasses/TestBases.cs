using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Base class for integration tests that exercise a single booted script.
    /// Boots a fresh <see cref="Script{TProgram}"/> before each test and exposes
    /// <see cref="Mother"/>, <see cref="Bus"/>, and <see cref="Clock"/> for direct access.
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
    public abstract class ScriptTestBase<TProgram>
        where TProgram : MyGridProgram, new()
    {
        /// <summary>The booted script. Available after <see cref="SetUp"/>.</summary>
        protected Script<TProgram> Script { get; private set; }

        /// <summary>The booted <see cref="Mother"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected Mother Mother => Script.Mother;

        /// <summary>The booted <typeparamref name="TProgram"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected TProgram Program => Script.Program;

        /// <summary>The <see cref="CommandBus"/> for the booted script. Available after <see cref="SetUp"/>.</summary>
        protected CommandBus Bus => Script.Bus;

        /// <summary>The <see cref="ClockDriver"/> for the booted script. Available after <see cref="SetUp"/>.</summary>
        protected ClockDriver Clock => Script.Clock;

        /// <summary>Boots a fresh script before each test.</summary>
        [SetUp]
        public virtual void SetUp()
        {
            Script = new Script<TProgram>().Boot();
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
    public abstract class ScriptFeatureTestBase<TProgram>
        where TProgram : MyGridProgram, new()
    {
        /// <summary>The booted script. Available after <see cref="SetUp"/>.</summary>
        protected Script<TProgram> Script { get; private set; }

        /// <summary>The booted <see cref="Mother"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected Mother Mother => Script.Mother;

        /// <summary>The booted <typeparamref name="TProgram"/> instance. Available after <see cref="SetUp"/>.</summary>
        protected TProgram Program => Script.Program;

        /// <summary>The <see cref="CommandBus"/> for the booted script. Available after <see cref="SetUp"/>.</summary>
        protected CommandBus Bus => Script.Bus;

        /// <summary>The <see cref="ClockDriver"/> for the booted script. Available after <see cref="SetUp"/>.</summary>
        protected ClockDriver Clock => Script.Clock;

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
            Script = new Script<TProgram>().Boot();
            Echo = Script.CaptureEcho();
        }
    }

    /// <summary>
    /// Base class for integration tests that involve multiple scripts running inside
    /// one shared <see cref="TestWorld"/>. Creates a fresh world before each test.
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
    public abstract class WorldTestBase
    {
        /// <summary>The shared test world. Available after <see cref="SetUp"/>.</summary>
        protected TestWorld World { get; private set; }

        /// <summary>Creates a fresh <see cref="TestWorld"/> before each test.</summary>
        [SetUp]
        public virtual void SetUp()
        {
            World = new TestWorld();
        }
    }
}
