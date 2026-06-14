using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests.Integration
{
    [Category("Layer:Module")]
    public class ActivityMonitorTests : ScriptTestBase<CoreTestProgram>
    {
        [Test]
        public void Constructor_Initializes_ActiveBlocks_As_Empty()
        {
            var monitor = new ActivityMonitor(Mother);

            Assert.That(monitor.ActiveBlocks, Is.Not.Null);
            Assert.That(monitor.ActiveBlocks, Is.Empty);
        }

        [Test]
        public void RegisterBlock_Adds_A_New_Block_To_ActiveBlocks()
        {
            var monitor = new ActivityMonitor(Mother);
            var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Docking Rotor");

            monitor.RegisterBlock(block, _ => false, _ => { });

            Assert.That(monitor.ActiveBlocks.ContainsKey(block), Is.True);
        }

        [Test]
        public void RegisterBlock_Overwrites_An_Existing_Block_Registration()
        {
            var monitor = new ActivityMonitor(Mother);
            var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Docking Rotor");

            bool firstCallbackInvoked = false;
            bool secondCallbackInvoked = false;

            monitor.RegisterBlock(block, _ => true, _ => firstCallbackInvoked = true);
            monitor.RegisterBlock(block, _ => true, _ => secondCallbackInvoked = true);

            monitor.Run();

            Assert.That(firstCallbackInvoked, Is.False);
            Assert.That(secondCallbackInvoked, Is.True);
        }

        [Test]
        public void UnregisterBlock_Removes_A_Registered_Block()
        {
            var monitor = new ActivityMonitor(Mother);
            var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Docking Rotor");

            monitor.RegisterBlock(block, _ => false, _ => { });
            monitor.UnregisterBlock(block);

            Assert.That(monitor.ActiveBlocks.ContainsKey(block), Is.False);
        }

        [Test]
        public void UnregisterBlock_For_Missing_Block_Does_Not_Throw()
        {
            var monitor = new ActivityMonitor(Mother);
            var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Docking Rotor");

            Assert.DoesNotThrow(() => monitor.UnregisterBlock(block));
            Assert.That(monitor.ActiveBlocks, Is.Empty);
        }

        [Test]
        public void Run_When_Condition_Is_False_Does_Not_Invoke_Callback_Or_Remove_Block()
        {
            var monitor = new ActivityMonitor(Mother);
            var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Docking Rotor");
            bool callbackInvoked = false;

            monitor.RegisterBlock(block, _ => false, _ => callbackInvoked = true);

            monitor.Run();

            Assert.That(callbackInvoked, Is.False);
            Assert.That(monitor.ActiveBlocks.ContainsKey(block), Is.True);
        }

        [Test]
        public void Run_When_Condition_Is_True_Invokes_Callback_And_Unregisters_Block()
        {
            var monitor = new ActivityMonitor(Mother);
            var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Docking Rotor");
            int callbackCount = 0;
            IMyTerminalBlock callbackBlock = null;

            monitor.RegisterBlock(block, _ => true, b =>
            {
                callbackCount++;
                callbackBlock = b;
            });

            monitor.Run();

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackBlock, Is.SameAs(block));
            Assert.That(monitor.ActiveBlocks.ContainsKey(block), Is.False);
        }

        [Test]
        public void Run_When_Callback_Is_Null_Still_Unregisters_Block_At_Terminal_State()
        {
            var monitor = new ActivityMonitor(Mother);
            var block = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Docking Rotor");

            monitor.RegisterBlock(block, _ => true, null);

            Assert.DoesNotThrow(() => monitor.Run());
            Assert.That(monitor.ActiveBlocks.ContainsKey(block), Is.False);
        }

        [Test]
        public void Run_Processes_Multiple_Blocks_And_Removes_Only_Completed_Ones()
        {
            var monitor = new ActivityMonitor(Mother);
            var completedBlock = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Completed");
            var pendingBlock = TerminalBlockFactory.Create<IMyTerminalBlock>(customName: "Pending");
            int callbackCount = 0;

            monitor.RegisterBlock(completedBlock, _ => true, _ => callbackCount++);
            monitor.RegisterBlock(pendingBlock, _ => false, _ => callbackCount++);

            monitor.Run();

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(monitor.ActiveBlocks.ContainsKey(completedBlock), Is.False);
            Assert.That(monitor.ActiveBlocks.ContainsKey(pendingBlock), Is.True);
        }
    }
}