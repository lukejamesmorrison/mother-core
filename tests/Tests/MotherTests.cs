using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System;

namespace MotherCore.Tests.Tests
{
    public class MotherTests
    {
        private Program _program;

        [SetUp]
        public void Setup()
        {
            _program = Gateway.CreateProgram<Program>().Build();
        }

        [Test]
        public void Mother_Can_Be_Created_With_A_Program_Instance()
        {
            Mother mother = new Mother(_program);

            Assert.That(mother.Program, Is.SameAs(_program));
            Assert.That(mother.IGC, Is.SameAs(_program.IGC));
            Assert.That(mother.GridTerminalSystem, Is.SameAs(_program.GridTerminalSystem));
            Assert.That(mother.Runtime, Is.SameAs(_program.Runtime));
            Assert.That(mother.ProgrammableBlock, Is.SameAs(_program.Me));
            Assert.That(mother.CubeGrid, Is.SameAs(_program.Me.CubeGrid));
            Assert.That(mother.Id, Is.EqualTo(_program.IGC.Me));
            Assert.That(mother.Name, Is.SameAs(_program.Me.CubeGrid.CustomName));
        }
    }
}
