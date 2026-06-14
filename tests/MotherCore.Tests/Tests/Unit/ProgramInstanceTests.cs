using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using Sandbox.ModAPI.Ingame;
//using static Sandbox.ModAPI.Ingame.MyGridProgram as Program;

namespace MotherCore.Tests.Tests.Unit
{
    /// <summary>
    ///     Sample tests for the <see cref="CoreTestProgram" /> class.
    /// </summary>
    /// <remarks>
    ///     You will need to add a reference to your Space Engineers script project before these tests will pass. You should
    ///     also make sure your Program class is public.
    /// </remarks>
    [TestFixture]
    [Category("Layer:Unit")]
    public class ProgramInstanceTests
    {
        /// <summary>
        ///     Happy-case test for creating a new program instance.
        /// </summary>
        /// <remarks>
        ///     Demonstrates the most basic way to create a new program instance.
        ///     You cannot simply instantiate the program class directly, as it inherits from <see cref="MyGridProgram" />
        ///     and requires specific dependencies to be set up, and a specific way to be instantiated.
        /// </remarks>
        [Test]
        public void NewProgram_WhenCalled_ShouldNotThrow()
        {
            // Act
            var program = ProgramFactory.CreateProgram<CoreTestProgram>()
                .Build();

            // Assert
            Assert.That(program, Is.Not.Null);
        }

        /// <summary>
        ///     An example test showing how to swap out the default Echo method with a custom one.
        /// </summary>
        [Test]
        public void NewProgram_WithCustomEcho_RunsCustomEcho()
        {
            // Arrange
            string echoMessage = null;
            var program = ProgramFactory.CreateProgram<CoreTestProgram>()
                .WithEcho(message => echoMessage = message)
                .Build();

            // Act
            program.Echo("Hello, World!");

            // Assert
            Assert.That(echoMessage, Is.EqualTo("Hello, World!"));
        }

        /// <summary>
        ///     An example test showing how to set the storage value for a program instance.
        /// </summary>
        [Test]
        public void NewProgram_WithCustomPbFake_HasMeInstance()
        {
            // Arrange
            var me = new FakeProgrammableBlock(
                customName: "Test PB",
                gridName: "Grid A");
            me.IsRunning = true;

            // Act
            var program = ProgramFactory.CreateProgram<CoreTestProgram>()
                .WithMe(me)
                .Build();

            // Assert
            Assert.That(program.Me.IsRunning, Is.True);
            Assert.That(program.Me.CustomName, Is.EqualTo("Test PB"));
        }
    }
}

