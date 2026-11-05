using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;
using System.Collections.Generic;

namespace MotherCore.Tests.Module
{
    [Category(TestCategories.LayerModule)]
    public class VariableSubstitutionTests : TestBase
    {
        Mother BootMother(params string[] nameValuePairs)
        {
            var composer = new CustomDataComposer();

            for (int i = 0; i < nameValuePairs.Length; i += 2)
                composer.WithVariable(nameValuePairs[i], nameValuePairs[i + 1]);

            return ScriptFactory()
                .WithMother()
                .WithCustomData(composer.Build())
                .Boot()
                .Mother;
        }

        [Test]
        public void It_Returns_The_Same_Reference_When_Input_Has_No_Dollar()
        {
            var mother = BootMother("A", "1");
            string input = new string("no variables here".ToCharArray());

            Assert.That(ReferenceEquals(mother.SubstituteVariables(input), input), Is.True);
        }

        [Test]
        public void It_Resolves_Variables_With_No_Variables_Defined_Without_Failing()
        {
            var mother = BootMother();

            Assert.That(mother.SubstituteVariables("cost $5"), Is.EqualTo("cost $5"));
        }

        [Test]
        public void Longest_Name_Wins()
        {
            var mother = BootMother(
                "PLAYER", "Luke",
                "PLAYER_NAME", "Skywalker",
                "ZGDTutorialPageNext", "next",
                "ZGDTutorialPageNextProxy", "proxy");

            Assert.That(mother.SubstituteVariables("$PLAYER_NAME and $PLAYER"), Is.EqualTo("Skywalker and Luke"));
            Assert.That(mother.SubstituteVariables("$ZGDTutorialPageNextProxy $ZGDTutorialPageNext"), Is.EqualTo("proxy next"));
        }

        [Test]
        public void A_Name_Is_Matched_As_A_Prefix_Of_Following_Text()
        {
            var mother = BootMother("A", "1");

            Assert.That(mother.SubstituteVariables("$Abc"), Is.EqualTo("1bc"));
        }

        [Test]
        public void Matching_Is_Case_Sensitive()
        {
            var mother = BootMother("PLAYER", "Luke");

            Assert.That(mother.SubstituteVariables("$player"), Is.EqualTo("$player"));
        }

        [Test]
        public void Undefined_Variables_And_Digits_Stay_Literal()
        {
            var mother = BootMother("A", "1");

            Assert.That(mother.SubstituteVariables("$UNKNOWN $5 $ end$"), Is.EqualTo("$UNKNOWN $5 $ end$"));
        }

        [Test]
        public void Double_Dollar_Yields_A_Literal_Dollar()
        {
            var mother = BootMother("X", "value");

            Assert.That(mother.SubstituteVariables("$$X"), Is.EqualTo("$X"));
            Assert.That(mother.SubstituteVariables("$$$X"), Is.EqualTo("$value"));
            Assert.That(mother.SubstituteVariables("$$"), Is.EqualTo("$"));
        }

        [Test]
        public void An_Escaped_Reference_Stored_In_A_Variable_Resolves_When_Used()
        {
            var mother = BootMother("B", "bee");
            var config = mother.GetModule<Configuration>();

            config.SetVariable("A", mother.SubstituteVariables("$$B"));

            Assert.That(mother.ConfigVariables["A"], Is.EqualTo("$B"));
            Assert.That(mother.SubstituteVariables("$A"), Is.EqualTo("bee"));
        }

        [Test]
        public void A_Stored_Double_Dollar_Reads_As_A_Literal_Dollar()
        {
            var mother = BootMother("PRICE", "$$5");

            Assert.That(mother.SubstituteVariables("$PRICE"), Is.EqualTo("$5"));
        }

        [Test]
        public void It_Resolves_Nested_Variables()
        {
            var mother = BootMother("A", "$B", "B", "x");

            Assert.That(mother.SubstituteVariables("$A"), Is.EqualTo("x"));
        }

        [Test]
        public void It_Resolves_Three_Levels_Of_Nesting()
        {
            var mother = BootMother("A", "[$B]", "B", "($C)", "C", "x");

            Assert.That(mother.SubstituteVariables("$A"), Is.EqualTo("[(x)]"));
        }

        [Test]
        public void Nested_Variables_Bind_Late()
        {
            var mother = BootMother("A", "$B", "B", "1");
            var config = mother.GetModule<Configuration>();

            config.SetVariable("B", "2");

            Assert.That(mother.SubstituteVariables("$A"), Is.EqualTo("2"));
        }

        [Test]
        public void A_Cycle_Stops_At_The_Depth_Limit_And_Names_The_Chain()
        {
            var mother = BootMother("A", "$B", "B", "$A");
            string result, error;

            bool ok = mother.TrySubstituteVariables("$A", out result, out error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("A > B > A > B > A"));
            Assert.That(result, Is.EqualTo("$B"));
        }

        [Test]
        public void A_Self_Reference_Stops_At_The_Depth_Limit()
        {
            var mother = BootMother("X", "$X");
            string result, error;

            bool ok = mother.TrySubstituteVariables("$X", out result, out error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("X > X > X > X > X"));
        }

        [Test]
        public void A_Fan_Out_Cycle_Ends_At_The_Depth_Limit()
        {
            var mother = BootMother("A", "$B$B", "B", "$A$A");
            string result, error;

            bool ok = mother.TrySubstituteVariables("$A", out result, out error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("deeper than " + Configuration.MaxVariableDepth));
        }

        [Test]
        public void The_Wrapper_Leaves_The_Name_At_The_Limit_Without_Throwing()
        {
            var mother = BootMother("A", "$B", "B", "$A");

            Assert.That(mother.SubstituteVariables("$A"), Is.EqualTo("$B"));
        }

        [Test]
        public void A_Chain_Exactly_At_The_Limit_Resolves()
        {
            var mother = BootMother("A", "$B", "B", "$C", "C", "$D", "D", "$E", "E", "$F", "F", "end");
            string result, error;

            Assert.That(mother.TrySubstituteVariables("$A", out result, out error), Is.True);
            Assert.That(result, Is.EqualTo("end"));
            Assert.That(error, Is.Null);
        }

        [Test]
        public void It_Handles_Many_Variables_Sharing_First_Characters()
        {
            var pairs = new List<string>();
            for (int i = 0; i < 200; i++)
            {
                pairs.Add("VAR" + i);
                pairs.Add("value" + i);
            }

            var mother = BootMother(pairs.ToArray());

            Assert.That(mother.SubstituteVariables("$VAR0 $VAR1 $VAR19 $VAR199 $VAR200"),
                Is.EqualTo("value0 value1 value19 value199 value20" + "0"));
        }

        [Test]
        public void The_Index_Includes_Variables_Added_After_It_Was_Built()
        {
            var mother = BootMother("A", "1");
            var config = mother.GetModule<Configuration>();

            Assert.That(mother.SubstituteVariables("$NEW"), Is.EqualTo("$NEW"));

            config.SetVariable("NEW", "fresh");

            Assert.That(mother.SubstituteVariables("$NEW"), Is.EqualTo("fresh"));
        }

        [Test]
        public void Changing_An_Existing_Value_Is_Seen_Immediately()
        {
            var mother = BootMother("A", "1");
            var config = mother.GetModule<Configuration>();

            mother.SubstituteVariables("$A");
            config.SetVariable("A", "2");

            Assert.That(mother.SubstituteVariables("$A"), Is.EqualTo("2"));
        }

        [Test]
        public void The_Index_Is_Rebuilt_After_A_Config_Reload()
        {
            var mother = BootMother("A", "1");
            mother.SubstituteVariables("$A");

            mother.ProgrammableBlock.CustomData = new CustomDataComposer()
                .WithVariable("B", "2")
                .Build();
            mother.GetModule<Configuration>().Reload();

            Assert.That(mother.SubstituteVariables("$A $B"), Is.EqualTo("$A 2"));
        }

        [Test]
        public void Save_Writes_The_Raw_Reference_And_It_Survives_A_Reload()
        {
            var mother = BootMother("B", "bee");
            var config = mother.GetModule<Configuration>();

            config.SetVariable("A", "$B", true);

            Assert.That(mother.ProgrammableBlock.CustomData, Does.Contain("$B"));

            mother.GetModule<Configuration>().Reload();

            Assert.That(mother.ConfigVariables["A"], Is.EqualTo("$B"));
            Assert.That(mother.SubstituteVariables("$A"), Is.EqualTo("bee"));
        }

        [Test]
        public void SubstituteCommandParameters_Can_Skip_Variable_Substitution()
        {
            var mother = BootMother("PLAYER", "Luke");
            var options = new Dictionary<string, string> { { "name", "Han" } };

            Assert.That(mother.SubstituteCommandParameters("Hi $PLAYER", null, false), Is.EqualTo("Hi $PLAYER"));
            Assert.That(mother.SubstituteCommandParameters("Hi {{name}} $PLAYER", options, false), Is.EqualTo("Hi Han $PLAYER"));
            Assert.That(mother.SubstituteCommandParameters("Hi {{name}} $PLAYER", options), Is.EqualTo("Hi Han Luke"));
        }
    }
}
