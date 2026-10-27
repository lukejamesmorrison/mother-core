using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Tests.Module
{
    /// <summary>
    /// Conditions and "when" rules loaded from [rules].
    /// </summary>
    [Category(TestCategories.LayerModule)]
    public class ConditionModuleTests : TestBase
    {
        class RecordingCommand : BaseModuleCommand
        {
            public readonly List<string> Calls = new List<string>();

            public override string Name => "rec";

            public override string Execute(TerminalCommand command)
            {
                Calls.Add(command.CommandString);
                return string.Empty;
            }
        }

        /// <summary>
        /// A config with no scheduled pass, so tests decide when rules are evaluated.
        /// </summary>
        static CustomDataComposer Data()
        {
            return new CustomDataComposer().With("general", "ruleInterval", "100000");
        }

        static Script<CoreTestProgram> Boot(RecordingCommand rec, CustomDataComposer data, params IMyTerminalBlock[] blocks)
        {
            var script = ScriptFactory()
                .WithMother()
                .WithModules(m => new FakeConditionVocabulary(m))
                .WithCustomData(data.Build())
                .WithCommands(rec)
                .WithBlocks(blocks)
                .Boot();

            Settle(script);

            return script;
        }

        /// <summary>
        /// Lets the rule rebuild finish. It loads one rule per cycle, and no
        /// time passes for the rules while it does.
        /// </summary>
        static void Settle(Script<CoreTestProgram> script)
        {
            var runtime = (FakeGridProgramRuntimeInfo)script.Mother.Program.Runtime;
            var last = runtime.TimeSinceLastRun;
            runtime.TimeSinceLastRun = TimeSpan.Zero;

            for (int i = 0; i < 50; i++)
                script.Mother.GetModule<Clock>().Run();

            runtime.TimeSinceLastRun = last;
        }

        static void Evaluate(Script<CoreTestProgram> script)
        {
            Settle(script);
            script.Mother.GetModule<ConditionModule>().EvaluateRules();
            script.RunToIdle();
        }

        static string Printed(Script<CoreTestProgram> script)
        {
            return string.Join("\n", script.Mother.GetModule<Log>().Records);
        }

        static FakeBatteryBlock Battery(string name, double charge, string customData = "")
        {
            return new FakeBatteryBlock(customName: name, customData: customData)
            {
                CurrentStoredPower = (float)(charge * 100),
                MaxStoredPower = 100,
            };
        }

        static void Charge(FakeBatteryBlock battery, double charge)
        {
            battery.CurrentStoredPower = (float)(charge * 100);
        }

        // --- Loader ---

        [Test]
        public void A_Rule_Runs_Then_On_The_Rising_Edge_And_Else_On_The_Falling_Edge()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.5);
            var script = Boot(rec, Data().With("rules", "lowBattery", "when \"Bat\" low; rec A; else; rec B"), battery);

            Evaluate(script);
            Assert.That(rec.Calls, Is.Empty, "A rule that starts false does not run its else");

            Charge(battery, 0.1);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));

            Charge(battery, 0.5);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec B" }));
        }

        [Test]
        public void A_Rule_Without_Else_Only_Runs_Then()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "lowBattery", "when \"Bat\" low; rec A"), battery);

            Evaluate(script);
            Charge(battery, 0.5);
            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        [Test]
        public void A_Multi_Line_Rule_Loads()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var data = Data().With("rules", "lowBattery", "\n| when \"Bat\" low;\n| rec A;\n| else;\n| rec B;");
            var script = Boot(rec, data, battery);

            Evaluate(script);
            Charge(battery, 0.5);
            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec B" }));
        }

        [Test]
        public void An_Expression_And_A_Predicate_Give_The_Same_Result()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var data = Data()
                .With("rules", "named", "when \"Bat\" low; rec NAMED")
                .With("rules", "expression", "when \"Bat\" charge < 20%; rec EXPR");
            var script = Boot(rec, data, battery);

            Evaluate(script);

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec NAMED", "rec EXPR" }));
        }

        [Test]
        public void A_Variable_Works_In_The_Target_And_The_Value()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Main Battery", 0.3);
            var data = Data()
                .WithVariable("BAT", "Main Battery")
                .WithVariable("LIMIT", "40%")
                .With("rules", "low", "when \"$BAT\" charge < $LIMIT; rec A");
            var script = Boot(rec, data, battery);

            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        [Test]
        public void This_In_A_Block_Rule_Means_That_Block()
        {
            var rec = new RecordingCommand();
            var other = Battery("Other", 0.1);
            var battery = Battery("Own", 0.5, new CustomDataComposer().With("rules", "low", "when this low; rec THIS").Build());
            var script = Boot(rec, Data(), other, battery);

            Evaluate(script);
            Assert.That(rec.Calls, Is.Empty);

            Charge(battery, 0.1);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec THIS" }));
        }

        [Test]
        public void A_Quoted_Name_Containing_A_Dot_Resolves()
        {
            var rec = new RecordingCommand();
            var battery = Battery("BLD - Battery 1.5", 0.1);
            var script = Boot(rec, Data().With("rules", "low", "when \"BLD - Battery 1.5\" low; rec A"), battery);

            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        [Test]
        public void Rules_And_Hooks_Load_Independently()
        {
            var rec = new RecordingCommand();
            var gear = new FakeLandingGear(customName: "Gear", customData: new CustomDataComposer().With("hooks", "onLock", "rec OWN").Build());
            var other = new FakeLandingGear(customName: "Other Gear");
            var data = Data()
                .With("hooks", "\"Other Gear\".onUnlock", "rec CENTRAL")
                .With("rules", "locked", "when \"Gear\" locked; rec RULE");
            var script = Boot(rec, data, gear, other);
            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            catalogue.RunHook(gear, "onLock");
            catalogue.RunHook(other, "onUnlock");
            catalogue.RunHook(gear, "locked");
            script.RunToIdle();

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec OWN", "rec CENTRAL" }), "A rule name is not a hook");

            gear.LockMode = LandingGearMode.Locked;
            Evaluate(script);

            Assert.That(rec.Calls.Last(), Is.EqualTo("rec RULE"));
        }

        // --- Edge trigger ---

        [Test]
        public void A_Rule_Fires_Once_While_The_Condition_Stays_True_And_Re_Arms_After_False()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "low", "when \"Bat\" low; rec A; else; rec B"), battery);

            Evaluate(script);
            Evaluate(script);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));

            Charge(battery, 0.5);
            Evaluate(script);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec B" }));

            Charge(battery, 0.1);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec B", "rec A" }));
        }

        [Test]
        public void The_Scheduled_Pass_Evaluates_Rules()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.5);
            var data = new CustomDataComposer()
                .With("general", "ruleInterval", "0")
                .With("rules", "low", "when \"Bat\" low; rec A");
            var script = Boot(rec, data, battery);

            Charge(battery, 0.1);
            script.Run();
            script.Run();
            script.Run();

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        // --- Errors ---

        [Test]
        public void A_Bad_Rule_Is_Disabled_And_Reported_Once_Without_Stopping_The_Others()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var data = Data()
                .With("rules", "badProperty", "when \"Bat\" bogus < 1; rec BAD")
                .With("rules", "badPredicate", "when \"Bat\" bogus; rec BAD")
                .With("rules", "badOperator", "when \"Bat\" charge ~ 1; rec BAD")
                .With("rules", "badValue", "when \"Bat\" charge < lots; rec BAD")
                .With("rules", "badTarget", "when \"Nope\" low; rec BAD")
                .With("rules", "badOption", "when \"Bat\" low --for=abc; rec BAD")
                .With("rules", "badBand", "when \"Bat\" low --band=wide; rec BAD")
                .With("rules", "badEvery", "when \"Bat\" low --every=-1; rec BAD")
                .With("rules", "badCooldown", "when \"Bat\" low --cooldown; rec BAD")
                .With("rules", "badAt", "when \"Bat\" charge < 20% --at=25%; rec BAD")
                .With("rules", "unknownOption", "when \"Bat\" low --bogus=3; rec BAD")
                .With("rules", "badFirstStep", "rec BAD")
                .With("rules", "good", "when \"Bat\" low; rec GOOD");
            var script = Boot(rec, data, battery);

            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec GOOD" }));

            // A rebuild reports nothing new
            script.Mother.GetModule<ConditionModule>().HandleEvent(new ConstructRefreshedEvent(), null);

            var printed = Printed(script);
            Assert.That(printed, Does.Contain("Rule badProperty disabled: unknown property: bogus"));
            Assert.That(printed, Does.Contain("Rule badPredicate disabled: unknown predicate: bogus"));
            Assert.That(printed, Does.Contain("Rule badOperator disabled: unknown operator: ~"));
            Assert.That(printed, Does.Contain("Rule badValue disabled: bad value: lots"));
            Assert.That(printed, Does.Contain("Rule badTarget disabled: no target: Nope"));
            Assert.That(printed, Does.Contain("Rule badOption disabled: bad option: --for"));
            Assert.That(printed, Does.Contain("Rule badBand disabled: bad option: --band"));
            Assert.That(printed, Does.Contain("Rule badEvery disabled: bad option: --every"));
            Assert.That(printed, Does.Contain("Rule badCooldown disabled: bad option: --cooldown"));
            Assert.That(printed, Does.Contain("Rule badAt disabled: --at needs a predicate"));
            Assert.That(printed, Does.Contain("Rule unknownOption disabled: bad syntax"));
            Assert.That(printed, Does.Contain("Rule badFirstStep disabled: needs when"));
            Assert.That(Logged(script, "Rule badProperty disabled"), Is.EqualTo(1));
        }

        [Test]
        public void A_Rule_With_No_Steps_Warns_Once()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, Data().With("rules", "empty", "when \"Bat\" low"), Battery("Bat", 0.5));

            script.Mother.GetModule<ConditionModule>().HandleEvent(new ConstructRefreshedEvent(), null);

            Assert.That(Logged(script, "Rule empty has no steps"), Is.EqualTo(1));
        }

        [Test]
        public void A_Rule_Whose_Routine_Fails_Does_Not_Stop_The_Others()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var data = Data()
                .With("rules", "a", "when \"Bat\" low; nothing/here")
                .With("rules", "b", "when \"Bat\" low; rec B");
            var script = Boot(rec, data, battery);

            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec B" }));
        }

        // --- Properties and aggregation ---

        [Test]
        public void A_Group_Is_Aggregated_By_Average_And_Fires_Once()
        {
            var rec = new RecordingCommand();
            var a = Battery("A", 0.1);
            var b = Battery("B", 0.5);
            var script = ScriptFactory()
                .WithMother()
                .WithModules(m => new FakeConditionVocabulary(m))
                .WithCustomData(Data().With("rules", "low", "when \"Bats\" low; rec A").Build())
                .WithCommands(rec)
                .WithBlocks(a, b)
                .WithBlockGroup("Bats", a, b)
                .Boot();

            Evaluate(script);
            Assert.That(rec.Calls, Is.Empty, "The average is 30%");

            Charge(b, 0.1);
            Evaluate(script);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        [Test]
        public void A_Mixed_Group_Skips_Blocks_That_Do_Not_Have_The_Property()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Door");
            var script = ScriptFactory()
                .WithMother()
                .WithModules(m => new FakeConditionVocabulary(m))
                .WithCustomData(Data().With("rules", "low", "when \"Mixed\" low; rec A").Build())
                .WithCommands(rec)
                .WithBlocks(battery, door)
                .WithBlockGroup("Mixed", battery, door)
                .Boot();

            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        [Test]
        public void A_Condition_No_Block_Can_Answer_Is_False()
        {
            var rec = new RecordingCommand();
            var door = TerminalBlockFactory.Create<IMyDoor>(customName: "Door");
            var script = Boot(rec, Data().With("rules", "low", "when \"Door\" low; rec A"), door);

            Evaluate(script);

            Assert.That(rec.Calls, Is.Empty);
            Assert.That(Printed(script), Does.Not.Contain("disabled"));
        }

        [Test]
        public void Enabled_And_Working_Predicates_Read_The_Block()
        {
            var rec = new RecordingCommand();
            var light = TerminalBlockFactory.Create<IMyLightingBlock>(customName: "Light");
            var data = Data()
                .With("rules", "off", "when \"Light\" off; rec OFF; else; rec ON")
                .With("rules", "broken", "when \"Light\" broken; rec BROKEN");
            var script = Boot(rec, data, light);

            ((FakeTerminalBlock)light).Enabled = false;
            ((FakeTerminalBlock)light).IsWorking = false;
            Evaluate(script);

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec OFF", "rec BROKEN" }));

            ((FakeTerminalBlock)light).Enabled = true;
            Evaluate(script);

            Assert.That(rec.Calls.Last(), Is.EqualTo("rec ON"));
        }

        [Test]
        public void Status_Compares_Enums_By_Name()
        {
            var rec = new RecordingCommand();
            var gear = new FakeLandingGear(customName: "Gear");
            var data = Data()
                .With("rules", "gear", "when \"Gear\" locked; rec GEAR; else; rec UNGEAR")
                .With("rules", "ready", "when \"Gear\" status == readytolock; rec READY");
            var script = Boot(rec, data, gear);

            gear.LockMode = LandingGearMode.ReadyToLock;
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec READY" }));

            gear.LockMode = LandingGearMode.Locked;
            Evaluate(script);
            Assert.That(rec.Calls.Skip(1), Is.EquivalentTo(new[] { "rec GEAR" }));

            gear.LockMode = LandingGearMode.Unlocked;
            Evaluate(script);
            Assert.That(rec.Calls.Skip(2), Is.EquivalentTo(new[] { "rec UNGEAR" }));
        }

        [Test]
        public void An_Extension_Can_Register_A_Property_And_A_Predicate()
        {
            var rec = new RecordingCommand();
            var light = TerminalBlockFactory.Create<IMyLightingBlock>(customName: "Light");
            var data = Data()
                .With("rules", "expr", "when \"Light\" mine > 1; rec EXPR")
                .With("rules", "pred", "when \"Light\" huge; rec PRED");
            var script = Boot(rec, data, light);
            var module = script.Mother.GetModule<ConditionModule>();

            module.RegisterProperty<IMyTerminalBlock>("mine", b => 2);
            module.RegisterPredicate("huge", "mine > 1");
            module.HandleEvent(new ConstructRefreshedEvent(), null);
            Evaluate(script);

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec EXPR", "rec PRED" }));
        }

        [Test]
        public void Modules_Add_Rows_To_The_Same_Predicate_And_Properties_For_Their_Own_Blocks()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var light = TerminalBlockFactory.Create<IMyLightingBlock>(customName: "Light");
            var data = Data()
                .With("rules", "bat", "when \"Bat\" tiny; rec BAT")
                .With("rules", "light", "when \"Light\" tiny; rec LIGHT");
            var script = Boot(rec, data, battery, light);
            var module = script.Mother.GetModule<ConditionModule>();

            module.RegisterPredicate("tiny", "charge < 50%");
            module.RegisterPredicate("tiny", "glow < 50%");
            module.RegisterPredicate("tiny", "glow < 50%");
            module.RegisterProperty<IMyLightingBlock>("glow", b => 0.2);
            module.RegisterProperty<IMyLightingBlock>("glow", b => 0.9);
            module.HandleEvent(new ConstructRefreshedEvent(), null);
            Evaluate(script);

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec BAT", "rec LIGHT" }), "A repeated registration is ignored");
        }

        [Test]
        public void The_Programmable_Block_Predicates_Add_And_Replace_Predicates_And_Bands()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.3);
            var data = Data()
                .With("predicates", "low", "charge < 40%")
                .With("predicates", "low.band", "10%")
                .With("predicates", "half", "charge < 60% | charge < 80%")
                .With("rules", "low", "when \"Bat\" low; rec LOW; else; rec OK")
                .With("rules", "half", "when \"Bat\" half; rec HALF");
            var script = Boot(rec, data, battery);

            Evaluate(script);
            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec LOW", "rec HALF" }), "low is now 40%");

            Charge(battery, 0.45);
            Evaluate(script);
            Assert.That(rec.Calls.Count, Is.EqualTo(2), "The 10% band keeps it low");

            Charge(battery, 0.55);
            Evaluate(script);
            Assert.That(rec.Calls.Last(), Is.EqualTo("rec OK"));
        }

        [Test]
        public void A_Reboot_Restores_A_Registered_Predicate_After_Its_Programmable_Block_Predicate_Is_Removed()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, Data().With("predicates", "low", "charge < 40%"), Battery("Bat", 0.3));

            Assert.That(If(script, "\"Bat\" low"), Is.True);

            script.Mother.ProgrammableBlock.CustomData = Data().Build();
            script.Mother.GetModule<Configuration>().Reload();
            script.Mother.Boot();
            script.RunToIdle();

            Assert.That(If(script, "\"Bat\" low"), Is.False, "The registered low is 20%");
        }

        [Test]
        public void A_Reboot_Registers_The_Vocabulary_Again_Without_Doubling_It()
        {
            var script = Boot(new RecordingCommand(), Data(), Battery("Bat", 0.1));

            script.Mother.Boot();
            script.Mother.Boot();
            script.RunToIdle();

            Assert.That(If(script, "\"Bat\" low"), Is.True);
            Assert.That(If(script, "\"Bat\" locked"), Is.False);
        }

        // --- Reload ---

        [Test]
        public void Changing_The_Programmable_Block_Rules_Reloads_Them_Without_Firing_Again()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "first", "when \"Bat\" low; rec FIRST"), battery);

            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec FIRST" }));

            script.Mother.ProgrammableBlock.CustomData = Data()
                .With("rules", "first", "when \"Bat\" low; rec FIRST")
                .With("rules", "second", "when \"Bat\" low; rec SECOND")
                .Build();
            script.Mother.GetModule<Configuration>().Reload();
            script.Mother.GetModule<ConditionModule>().HandleEvent(new SystemConfigChangedEvent(), null);
            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec FIRST", "rec SECOND" }));
        }

        [Test]
        public void A_Rebuild_Loads_One_Rule_Per_Cycle_While_The_Old_Rules_Keep_Running()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data()
                .With("rules", "first", "when \"Bat\" low; rec FIRST")
                .With("rules", "second", "when \"Bat\" low; rec SECOND"), battery);

            Evaluate(script);
            rec.Calls.Clear();

            script.Mother.ProgrammableBlock.CustomData = Data()
                .With("rules", "first", "when \"Bat\" low; rec FIRST; else; rec NEW")
                .With("rules", "second", "when \"Bat\" low; rec SECOND")
                .Build();
            script.Mother.GetModule<Configuration>().Reload();

            var condition = script.Mother.GetModule<ConditionModule>();

            // Changes that arrive during a rebuild share it
            condition.HandleEvent(new SystemConfigChangedEvent(), null);
            condition.HandleEvent(new SystemConfigChangedEvent(), null);
            Advance(script, 0);

            Charge(battery, 0.5);
            condition.EvaluateRules();
            Assert.That(rec.Calls, Is.Empty, "One cycle in, the old rules still run, and they have no else");

            Settle(script);
            Charge(battery, 0.1);
            condition.EvaluateRules();
            script.RunToIdle();
            Charge(battery, 0.5);
            condition.EvaluateRules();
            script.RunToIdle();

            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec FIRST", "rec SECOND", "rec NEW" }));
            Assert.That(rec.Calls.Last(), Is.EqualTo("rec NEW"));
        }

        [Test]
        public void Changing_A_Block_Custom_Data_Reloads_Its_Rules()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data(), battery);

            Evaluate(script);
            Assert.That(rec.Calls, Is.Empty);

            battery.CustomData = new CustomDataComposer().With("rules", "low", "when this low; rec OWN").Build();

            // The catalogue notices the change on its next refresh
            for (int i = 0; i < 12; i++)
                script.Run();

            Evaluate(script);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec OWN" }));
        }

        [Test]
        public void Rules_Follow_Their_Block_In_And_Out_Of_The_Construct()
        {
            var rec = new RecordingCommand();
            var cargoGrid = GridFactory.Create("Cargo Pod");
            var battery = Battery("Cargo Battery", 0.1);
            var script = ScriptFactory()
                .WithMother()
                .WithModules(m => new FakeConditionVocabulary(m))
                .WithCustomData(Data().With("rules", "low", "when \"Cargo Battery\" low; rec A; else; rec B").Build())
                .WithCommands(rec)
                .Create();
            var mergeBlock = script.AddUnmergedMergeBlockPair(script.PrimaryGrid, cargoGrid);

            script.WithBlock(battery, cargoGrid).Boot();

            Evaluate(script);
            Assert.That(rec.Calls, Is.Empty, "The battery is not on the construct yet");
            Assert.That(Logged(script, "no target: Cargo Battery"), Is.EqualTo(1));

            var mergeModule = script.Mother.GetModule<MergeBlockModule>();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            mergeModule.LockMergeBlock(mergeBlock);
            catalogue.Run();
            script.RunToIdle();
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }), "The rule loads once the battery joins");

            mergeModule.UnlockMergeBlock(mergeBlock);
            catalogue.Run();
            script.RunToIdle();

            Charge(battery, 0.5);
            Evaluate(script);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }), "The rule no longer sees the battery");
        }

        // --- Stability (Phase 2) ---

        /// <summary>
        /// Moves the module clock on without running a rule pass.
        /// </summary>
        static void Advance(Script<CoreTestProgram> script, double seconds)
        {
            var runtime = (FakeGridProgramRuntimeInfo)script.Mother.Program.Runtime;
            runtime.TimeSinceLastRun = TimeSpan.FromSeconds(seconds);
            script.Mother.GetModule<Clock>().Run();
        }

        /// <summary>
        /// Lets time pass, then runs a rule pass and the routines it started.
        /// </summary>
        static void Step(Script<CoreTestProgram> script, double seconds)
        {
            Advance(script, seconds);
            script.Mother.GetModule<ConditionModule>().EvaluateRules();

            for (int i = 0; i < 3; i++)
                Advance(script, 0);
        }

        static void Settle(Script<CoreTestProgram> script, double seconds)
        {
            Advance(script, seconds);

            for (int i = 0; i < 3; i++)
                Advance(script, 0);
        }

        static bool If(Script<CoreTestProgram> script, string condition)
        {
            string error;
            bool result = script.Mother.GetModule<ConditionModule>().Evaluate(new TerminalCommand("if " + condition), out error);

            Assert.That(error, Is.Null);
            return result;
        }

        [Test]
        public void Less_Or_Equal_And_Greater_Or_Equal_Include_The_Limit()
        {
            var script = Boot(new RecordingCommand(), Data(), Battery("Bat", 0.5));

            Assert.That(If(script, "\"Bat\" charge <= 50%"), Is.True);
            Assert.That(If(script, "\"Bat\" charge >= 50%"), Is.True);
            Assert.That(If(script, "\"Bat\" charge < 50%"), Is.False);
            Assert.That(If(script, "\"Bat\" charge > 50%"), Is.False);
        }

        [Test]
        public void Band_Keeps_A_Rule_Tripped_Until_The_Value_Passes_The_Reset_Threshold()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.5);
            var script = Boot(rec, Data().With("rules", "gen", "when \"Bat\" charge < 20% --band=10; rec ON; else; rec OFF"), battery);

            Charge(battery, 0.19);
            Step(script, 1);
            Charge(battery, 0.25);
            Step(script, 1);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec ON" }), "25% is inside the band");

            Charge(battery, 0.31);
            Step(script, 1);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec ON", "rec OFF" }));

            Charge(battery, 0.25);
            Step(script, 1);
            Assert.That(rec.Calls.Count, Is.EqualTo(2), "It does not trip again above 20%");

            Charge(battery, 0.15);
            Step(script, 1);
            Assert.That(rec.Calls.Last(), Is.EqualTo("rec ON"));
        }

        [Test]
        public void Generators_On_At_20_And_Off_At_80_With_A_Band_Of_60()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.5);
            var script = Boot(rec, Data().With("rules", "gen", "when \"Bat\" charge < 20% --band=60; rec ON; else; rec OFF"), battery);

            foreach (var charge in new[] { 0.5, 0.15, 0.4, 0.7, 0.79, 0.85, 0.5, 0.3, 0.1 })
            {
                Charge(battery, charge);
                Step(script, 1);
            }

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec ON", "rec OFF", "rec ON" }));
        }

        [Test]
        [TestCase("charge < 20%", 0.21, false, Description = "The implicit 2% band")]
        [TestCase("charge < 20% --band=0", 0.21, true, Description = "A band of 0 turns it off")]
        [TestCase("low", 0.24, false, Description = "low has a 5% band")]
        [TestCase("low --band=1%", 0.24, true, Description = "--band overrides the predicate band")]
        [TestCase("charge < 20% --band=5%", 0.24, false, Description = "A percent band")]
        public void The_Band_Defaults_And_Overrides_Apply_After_The_Rule_Trips(string condition, double reset, bool resets)
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" " + condition + "; rec ON; else; rec OFF"), battery);

            Step(script, 1);
            Charge(battery, reset);
            Step(script, 1);

            Assert.That(rec.Calls.Contains("rec OFF"), Is.EqualTo(resets));
        }

        [Test]
        public void A_Greater_Than_Band_Resets_Below_The_Threshold()
        {
            var rec = new RecordingCommand();
            var cockpit = TerminalBlockFactory.Create<IMyCockpit>(customName: "Cockpit");
            var script = Boot(rec, Data().With("rules", "fast", "when \"Cockpit\" speed > 30; rec FAST; else; rec SLOW"), cockpit);

            ((FakeCockpit)cockpit).ShipSpeed = 31;
            Step(script, 1);
            ((FakeCockpit)cockpit).ShipSpeed = 29.5;
            Step(script, 1);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec FAST" }), "The implicit band is 1 unit");

            ((FakeCockpit)cockpit).ShipSpeed = 28;
            Step(script, 1);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec FAST", "rec SLOW" }));
        }

        [Test]
        public void For_Waits_Until_The_Condition_Has_Held_Long_Enough()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.5);
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" low --for=5; rec A; else; rec B"), battery);

            Charge(battery, 0.1);
            Step(script, 0);
            Step(script, 2);
            Step(script, 2);
            Assert.That(rec.Calls, Is.Empty, "Held for 4 seconds");

            Step(script, 2);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));

            Charge(battery, 0.5);
            Step(script, 0);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec B" }), "Releasing is not debounced");
        }

        [Test]
        public void For_Starts_Over_When_The_Condition_Drops()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" low --for=5; rec A; else; rec B"), battery);

            Step(script, 0);
            Step(script, 4);
            Charge(battery, 0.5);
            Step(script, 1);
            Charge(battery, 0.1);
            Step(script, 0);
            Step(script, 4);
            Assert.That(rec.Calls, Is.Empty, "The 4 seconds before the drop do not count");

            Step(script, 2);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        [Test]
        public void A_Flapping_Input_Fires_At_Most_Once_With_Band_And_For()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" charge < 20% --band=5% --for=3; rec A; else; rec B"), battery);

            Step(script, 0);

            for (int i = 0; i < 12; i++)
            {
                Charge(battery, i % 2 == 0 ? 0.19 : 0.21);
                Step(script, 1);
            }

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        [Test]
        public void Without_A_Band_The_Same_Flapping_Input_Never_Holds_Long_Enough()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" charge < 20% --band=0 --for=3; rec A; else; rec B"), battery);

            for (int i = 0; i < 12; i++)
            {
                Charge(battery, i % 2 == 0 ? 0.19 : 0.21);
                Step(script, 1);
            }

            Assert.That(rec.Calls, Is.Empty);
        }

        [Test]
        public void Cooldown_Holds_Back_The_Next_Firing()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" low --cooldown=10; rec A; else; rec B"), battery);

            Step(script, 0);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));

            Charge(battery, 0.5);
            Step(script, 3);
            Step(script, 3);
            Step(script, 3);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }), "9 seconds since it fired");

            Step(script, 2);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec B" }));
        }

        [Test]
        public void A_Rule_Without_Cooldown_Fires_On_Every_Change()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.1);
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" low; rec A; else; rec B"), battery);

            Step(script, 0);
            Charge(battery, 0.5);
            Step(script, 0);
            Charge(battery, 0.1);
            Step(script, 0);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A", "rec B", "rec A" }));
        }

        [Test]
        public void Every_Gives_A_Rule_Its_Own_Interval()
        {
            var rec = new RecordingCommand();
            var data = new CustomDataComposer()
                .With("general", "ruleInterval", "1")
                .With("rules", "plain", "when \"Plain\" low; rec PLAIN")
                .With("rules", "slow", "when \"Slow\" low --every=5; rec SLOW");
            var script = Boot(rec, data, Battery("Plain", 0.1), Battery("Slow", 0.1));

            Settle(script, 1.5);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec PLAIN" }), "The shared interval is 1 second");

            Settle(script, 2);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec PLAIN" }));

            Settle(script, 2);
            Assert.That(rec.Calls, Is.EquivalentTo(new[] { "rec PLAIN", "rec SLOW" }));
        }

        [Test]
        public void Every_Can_Be_Shorter_Than_The_Shared_Interval()
        {
            var rec = new RecordingCommand();
            var data = Data().With("rules", "fast", "when \"Bat\" low --every=1; rec FAST");
            var script = Boot(rec, data, Battery("Bat", 0.1));

            Settle(script, 0.5);
            Assert.That(rec.Calls, Is.Empty);

            Settle(script, 1);
            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec FAST" }), "The shared interval is 100000 seconds");
        }

        [Test]
        public void At_Overrides_A_Predicate_Threshold_On_A_Rule()
        {
            var rec = new RecordingCommand();
            var battery = Battery("Bat", 0.22);
            var data = Data()
                .With("rules", "default", "when \"Bat\" low; rec DEFAULT")
                .With("rules", "at", "when \"Bat\" low --at=25%; rec AT");
            var script = Boot(rec, data, battery);

            Step(script, 1);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec AT" }));
        }

        [Test]
        public void A_Rebuild_Keeps_The_Timers_Of_A_Rule()
        {
            var rec = new RecordingCommand();
            var script = Boot(rec, Data().With("rules", "r", "when \"Bat\" low --for=5; rec A"), Battery("Bat", 0.1));

            Step(script, 0);
            Step(script, 4);
            script.Mother.GetModule<ConditionModule>().HandleEvent(new ConstructRefreshedEvent(), null);
            Step(script, 2);

            Assert.That(rec.Calls, Is.EqualTo(new[] { "rec A" }));
        }

        /// <summary>
        /// How many times a message was printed. The terminal repeats its lines on
        /// every refresh, so the log is the record of what was printed.
        /// </summary>
        static int Logged(Script<CoreTestProgram> script, string fragment)
        {
            return script.Mother.GetModule<Log>().Records.Count(record => record.Contains(fragment));
        }
    }
}
