using IngameScript;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.TestUtilities
{
    /// <summary>
    /// Base class for unit tests that focus on commands and module methods inside
    /// a single script. Boots a fresh <see cref="Script{TProgram}"/> before each test.
    /// </summary>
    /// <remarks>
    /// Usage:
    /// <code>
    /// public class MyCommandTests : ModuleUnitTestBase&lt;TestProgram&gt;
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
    /// Override <see cref="SetUp"/> (calling <c>base.SetUp()</c>) to add
    /// custom data, extra commands, or other per-test configuration.
    /// </remarks>
    /// <typeparam name="TProgram">
    /// The script's <c>Program</c> type. Use <see cref="TestProgram"/> for
    /// MotherCore-only tests; use a real script's <c>Program</c> for targeted
    /// module tests inside that script.
    /// </typeparam>
    public abstract class ModuleUnitTestBase<TProgram>
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
    /// Base class for integration tests that exercise one real script instance.
    /// Boots a fresh <see cref="Script{TProgram}"/> before each test and wires
    /// up echo capture automatically.
    /// </summary>
    /// <remarks>
    /// Usage:
    /// <code>
    /// public class MyFeatureTests : ProgramFeatureTestBase&lt;TestProgram&gt;
    /// {
    ///     [Test]
    ///     public void Command_PrintsExpectedOutput()
    ///     {
    ///         Script.Run(UpdateType.Terminal, "help");
    ///
    ///         Echo.ShouldHavePrinted("Available commands");
    ///     }
    /// }
    /// </code>
    /// </remarks>
    /// <typeparam name="TProgram">
    /// The script's <c>Program</c> type. Use <see cref="TestProgram"/> for
    /// MotherCore-only tests; use a real script's <c>Program</c> for feature tests.
    /// </typeparam>
    public abstract class ProgramFeatureTestBase<TProgram>
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
        /// Use <c>Echo.Contains("...")</c> or <c>Echo.ShouldHavePrinted("...")</c>
        /// to assert on output.
        /// </summary>
        protected PrintCapture Echo { get; private set; }

        /// <summary>Boots a fresh script and wires echo capture before each test.</summary>
        [SetUp]
        public virtual void SetUp()
        {
            Script = new Script<TProgram>().Boot();
            Echo = Script.CaptureEcho();
        }
    }

    /// <summary>
    /// Base class for tests that involve multiple scripts running inside one shared
    /// <see cref="TestWorld"/>. Creates a fresh world before each test.
    /// </summary>
    /// <remarks>
    /// Usage:
    /// <code>
    /// public class MultiScriptTests : MultiProgramFeatureTestBase
    /// {
    ///     [Test]
    ///     public void ShipA_Can_Send_Command_To_ShipB()
    ///     {
    ///         var shipA = World.CreateScript&lt;Program&gt;("ShipA").Boot();
    ///         var shipB = World.CreateScript&lt;Program&gt;("ShipB").Boot();
    ///
    ///         shipA.Bus.RunTerminalCommand("@ShipB help");
    ///         shipA.Clock.RunToIdle();
    ///         World.DispatchIgc();
    ///         World.RunIGC();
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public abstract class MultiProgramFeatureTestBase
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
