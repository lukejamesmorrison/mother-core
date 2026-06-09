using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Reflection;
using System.Net.NetworkInformation;
//using System.Collections.Generic;

namespace MotherCore.Tests.TestUtilities
{
    public class BaseModuleTests
    {
        public TestProgram _program;
        public Mother _mother;

        [SetUp]
        public void Setup()
        {
            var session = new Script().Boot();
            _program = session.Program;
            _mother = session.Mother;
        }
    }
}
