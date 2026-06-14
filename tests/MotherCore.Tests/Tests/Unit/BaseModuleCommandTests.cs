using IngameScript;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class BaseModuleCommandTests
    {
        static readonly FakeModuleCommand Command = new FakeModuleCommand();

        [Test]
        public void GetCommandName_Returns_Name_Property()
        {
            Assert.That(Command.GetCommandName(), Is.EqualTo("test/cmd"));
        }

        [Test]
        public void GetIncrementalValue_Throws_For_Invalid_Number()
        {
            var options = new Dictionary<string, string>();

            Assert.That(
                () => Command.ReadIncrementalValue("not-a-number", options),
                Throws.TypeOf<ArgumentException>().With.Message.EqualTo("Invalid numerical value provided."));
        }

        [Test]
        public void GetIncrementalValue_Returns_Zero_When_No_Add_Or_Sub_Option()
        {
            var options = new Dictionary<string, string>();

            Assert.That(Command.ReadIncrementalValue("3.5", options), Is.EqualTo(0f));
        }

        [Test]
        public void GetIncrementalValue_Returns_Positive_Value_When_Add_Option_Is_Set()
        {
            var options = new Dictionary<string, string>
            {
                { "add", "true" }
            };

            Assert.That(Command.ReadIncrementalValue("3.5", options), Is.EqualTo(3.5f));
        }

        [Test]
        public void GetIncrementalValue_Returns_Negative_Value_When_Sub_Option_Is_Set()
        {
            var options = new Dictionary<string, string>
            {
                { "sub", "true" }
            };

            Assert.That(Command.ReadIncrementalValue("3.5", options), Is.EqualTo(-3.5f));
        }

        [Test]
        public void GetIncrementalValue_Prefers_Add_When_Add_And_Sub_Are_Both_Set()
        {
            var options = new Dictionary<string, string>
            {
                { "add", "true" },
                { "sub", "true" }
            };

            Assert.That(Command.ReadIncrementalValue("2", options), Is.EqualTo(2f));
        }

        [Test]
        public void GetDistributedValue_Divides_Total_When_Cumulative_And_Multiple_Blocks()
        {
            Assert.That(Command.ReadDistributedValue(10f, 4, true), Is.EqualTo(2.5f));
        }

        [Test]
        public void GetDistributedValue_Does_Not_Divide_When_Cumulative_And_Single_Block()
        {
            Assert.That(Command.ReadDistributedValue(10f, 1, true), Is.EqualTo(10f));
        }

        [Test]
        public void GetDistributedValue_Does_Not_Divide_When_Not_Cumulative()
        {
            Assert.That(Command.ReadDistributedValue(10f, 4, false), Is.EqualTo(10f));
        }

        [Test]
        public void IsSharedMode_Returns_True_When_Share_Option_Is_Present()
        {
            var options = new Dictionary<string, string>
            {
                { "share", "true" }
            };

            Assert.That(Command.ReadSharedMode(options), Is.True);
        }

        [Test]
        public void IsSharedMode_Returns_False_When_Share_Option_Is_Missing()
        {
            var options = new Dictionary<string, string>();

            Assert.That(Command.ReadSharedMode(options), Is.False);
        }
    }
}

