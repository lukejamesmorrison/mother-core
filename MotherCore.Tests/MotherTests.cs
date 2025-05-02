using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;

namespace MotherCore.Tests
{
    public class MotherTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            var program = Gateway.CreateProgram<Program>().Build();

            //Mother mother = program.mother;

            Assert.Pass();
        }
    }
}
