using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Harness
{
    /// <summary>
    /// Verifies the <see cref="Script{TProgram}"/> test harness: boot lifecycle,
    /// fluent configuration, <see cref="Script{TProgram}.Run"/>, and
    /// <see cref="Script{TProgram}.CaptureEcho"/> / <see cref="PrintCapture.ShouldHavePrinted"/>.
    /// </summary>
    public class ScriptTests
    {
        // =====================================================================
        // Boot lifecycle
        // =====================================================================

        [Test]
        public void Boot_Exposes_A_Non_Null_CommandBus()
        {
            var script = new Script().Boot();

            Assert.That(script.Bus, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_ClockDriver()
        {
            var script = new Script().Boot();

            Assert.That(script.Clock, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_Configuration()
        {
            var script = new Script().Boot();

            Assert.That(script.Config, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_Program()
        {
            var script = new Script().Boot();

            Assert.That(script.Program, Is.Not.Null);
        }

        [Test]
        public void Boot_Exposes_A_Non_Null_Mother()
        {
            var script = new Script().Boot();

            Assert.That(script.Mother, Is.Not.Null);
        }

        // =====================================================================
        // WithCustomData
        // =====================================================================

        [Test]
        public void WithCustomData_Makes_Config_Command_Available_After_Boot()
        {
            var script = new Script()
                .WithCustomData(new CustomDataComposer()
                    .WithCommand("openDoor", "track")
                    .Build())
                .Boot();

            Assert.That(script.Mother.ConfigCommands.Keys, Contains.Item("openDoor"));
        }

        // =====================================================================
        // WithCommands
        // =====================================================================

        [Test]
        public void WithCommands_Registers_Command_With_Bus()
        {
            var tracker = new CommandSpy("myCmd");
            var script = new Script().WithCommands(tracker).Boot();

            script.Bus.RunTerminalCommand("myCmd");
            script.Clock.Tick(2);

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void WithCommands_Accepts_Multiple_Commands()
        {
            var trackerA = new CommandSpy("cmdA");
            var trackerB = new CommandSpy("cmdB");
            var script = new Script().WithCommands(trackerA, trackerB).Boot();

            script.Bus.RunTerminalCommand("cmdA");
            script.Bus.RunTerminalCommand("cmdB");
            script.Clock.RunToIdle();

            Assert.That(trackerA.ExecutionCount, Is.EqualTo(1));
            Assert.That(trackerB.ExecutionCount, Is.EqualTo(1));
        }

        // =====================================================================
        // Run
        // =====================================================================

        [Test]
        public void Run_Returns_Script_For_Chaining()
        {
            var script = new Script().Boot();

            var returned = script.Run(UpdateType.Update10);

            Assert.That(returned, Is.SameAs(script));
        }

        [Test]
        public void Run_Terminal_Dispatches_Argument_To_CommandBus()
        {
            var tracker = new CommandSpy("go");
            var script = new Script().WithCommands(tracker).Boot();

            script.Run(UpdateType.Terminal, "go");
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void Run_Terminal_Does_Not_Throw_For_Unknown_Command()
        {
            var script = new Script().Boot();

            Assert.DoesNotThrow(() => script.Run(UpdateType.Terminal, "unknowncmd"));
        }

        [Test]
        public void Run_Update10_Does_Not_Throw()
        {
            var script = new Script().Boot();

            Assert.DoesNotThrow(() => script.Run(UpdateType.Update10));
        }

        [Test]
        public void Run_Can_Be_Called_Multiple_Times()
        {
            var tracker = new CommandSpy("go");
            var script = new Script().WithCommands(tracker).Boot();

            script.Run(UpdateType.Terminal, "go");
            script.Clock.RunToIdle();
            script.Run(UpdateType.Terminal, "go");
            script.Clock.RunToIdle();

            Assert.That(tracker.ExecutionCount, Is.EqualTo(2));
        }

        // =====================================================================
        // CaptureEcho and ShouldHavePrinted
        // =====================================================================

        [Test]
        public void CaptureEcho_Returns_A_Non_Null_PrintCapture()
        {
            var script = new Script().Boot();

            Assert.That(script.CaptureEcho(), Is.Not.Null);
        }

        [Test]
        public void CaptureEcho_Called_Twice_Returns_Same_Instance()
        {
            var script = new Script().Boot();

            var first = script.CaptureEcho();
            var second = script.CaptureEcho();

            Assert.That(first, Is.SameAs(second));
        }

        [Test]
        public void CaptureEcho_Captures_Output_Written_Via_Terminal_Echo()
        {
            var script = new Script().Boot();
            var capture = script.CaptureEcho();

            // Terminal.Echo() calls Program.Echo() directly, which PrintCapture intercepts.
            // Mother.Print() routes through Terminal.Print() (a buffer), not through Echo.
            script.Mother.GetModule<Terminal>().Echo("hello from test");

            Assert.That(capture.Contains("hello from test"), Is.True);
        }

        [Test]
        public void CaptureEcho_Does_Not_Contain_Output_Written_Before_Capture_Was_Set_Up()
        {
            var script = new Script().Boot();

            script.Mother.GetModule<Terminal>().Echo("before capture");
            var capture = script.CaptureEcho();

            Assert.That(capture.Lines.Count, Is.EqualTo(0));
        }

        [Test]
        public void ShouldHavePrinted_Passes_When_Fragment_Is_Present()
        {
            var script = new Script().Boot();
            var capture = script.CaptureEcho();

            script.Mother.GetModule<Terminal>().Echo("expected output");

            Assert.DoesNotThrow(() => capture.ShouldHavePrinted("expected output"));
        }

        [Test]
        public void ShouldHavePrinted_Throws_When_Fragment_Is_Absent()
        {
            var script = new Script().Boot();
            var capture = script.CaptureEcho();

            Assert.Throws<AssertionException>(() => capture.ShouldHavePrinted("was never printed"));
        }
    }
}
