using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
//using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class BaseModuleTests
    {
        public Program _program;
        public Mother _mother;

        [SetUp]
        public void Setup()
        {
            _program = Gateway.CreateProgram<Program>().Build();
            _mother = new Mother(_program);
        }
    }
}
