using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using Sandbox.ModAPI.Ingame;
using System;

namespace MotherCore.Tests.Tests.Unit
{
    [Category(TestCategories.LayerUnit)]
    public class MotherTests
    {
        [Test]
        public void Mother_Can_Be_Created_With_A_Program_Instance()
        {
            var program = ProgramFactory.CreateProgram<CoreTestProgram>().Build();
            Mother mother = new Mother(program);

            Assert.That(mother.Program, Is.SameAs(program));
            Assert.That(mother.IGC, Is.SameAs(program.IGC));
            Assert.That(mother.GridTerminalSystem, Is.SameAs(program.GridTerminalSystem));
            Assert.That(mother.Runtime, Is.SameAs(program.Runtime));
            Assert.That(mother.ProgrammableBlock, Is.SameAs(program.Me));
            Assert.That(mother.CubeGrid, Is.SameAs(program.Me.CubeGrid));
            Assert.That(mother.Id, Is.EqualTo(program.IGC.Me));
            Assert.That(mother.Name, Is.SameAs(program.Me.CubeGrid.CustomName));
        }
    }
}


