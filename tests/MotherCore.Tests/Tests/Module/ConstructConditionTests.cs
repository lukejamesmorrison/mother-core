using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using TestWorld = MotherCore.Tests.Utilities.World;

namespace MotherCore.Tests.Tests.Module
{
    /// <summary>
    /// Condition names shared between the scripts on a construct: advertising
    /// them in the sync, resolving names a script does not define, asking the
    /// owner to evaluate conditions, and "when" rules that use them.
    ///
    /// The scripts of a World construct share one terminal system, so every
    /// script sees every block, as it does in the game. What differs between
    /// them is the vocabulary each script registers.
    /// </summary>
    [Category(TestCategories.LayerModule)]
    public class ConstructConditionTests : TestBase
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
        /// What a property reads from a battery, NaN for a battery it cannot answer for.
        /// </summary>
        class Reading
        {
            public double Value = 5;
        }

        /// <summary>
        /// Defines properties that read a value from a battery, and a "probe"
        /// property that counts how often it is read.
        /// </summary>
        class NumbersVocabulary : BaseExtensionModule
        {
            public static int Probes;
            readonly string[] Names;
            readonly Reading Read;

            public NumbersVocabulary(Mother mother, Reading read, params string[] names) : base(mother)
            {
                Names = names;
                Read = read;
            }

            public override void Boot()
            {
                foreach (var name in Names)
                    Block<IMyBatteryBlock>().Number(name, b => Read.Value);

                Block<IMyBatteryBlock>().Number("probe", b => { Probes++; return 1; });
            }
        }

        /// <summary>
        /// A script that only owns the tank row of low.
        /// </summary>
        class TankVocabulary : BaseExtensionModule
        {
            public TankVocabulary(Mother mother) : base(mother) { }

            public override void Boot()
            {
                Block<IMyGasTank>().Number("fill", b => b.FilledRatio);
                RegisterPredicate("low", "fill < 15%", "5%");
            }
        }

        /// <summary>
        /// What a script on the construct is made of.
        /// </summary>
        class Spec
        {
            public Func<Mother, IExtensionModule> Vocabulary;
            public IMyTerminalBlock[] Blocks = new IMyTerminalBlock[0];
            public RecordingCommand Rec;
        }

        static Spec Node(Func<Mother, IExtensionModule> vocabulary, params IMyTerminalBlock[] blocks)
        {
            return new Spec { Vocabulary = vocabulary, Blocks = blocks };
        }

        static Func<Mother, IExtensionModule> Fake => m => new FakeConditionVocabulary(m);

        static Func<Mother, IExtensionModule> Tanks => m => new TankVocabulary(m);

        static Func<Mother, IExtensionModule> Numbers(Reading read, params string[] names) => m => new NumbersVocabulary(m, read, names);

        static Func<Mother, IExtensionModule> Numbers(params string[] names) => Numbers(new Reading(), names);

        /// <summary>
        /// Scripts on one construct. Owners define vocabulary, and Asker is the
        /// script whose rules and conditions use names it may not define.
        /// </summary>
        class Construct
        {
            public TestWorld World;
            public Script<CoreTestProgram> Asker;
            public List<Script<CoreTestProgram>> Owners = new List<Script<CoreTestProgram>>();
            public RecordingCommand Rec = new RecordingCommand();
            public ConditionModule AskerCondition => Asker.Mother.GetModule<ConditionModule>();
            public IntergridMessageService AskerService => Asker.Mother.GetModule<IntergridMessageService>();
            public long OwnerId => Owners[0].Mother.Id;
            public IEnumerable<Script<CoreTestProgram>> All => Owners.Concat(new[] { Asker });
            public Script<CoreTestProgram> Lowest => Owners.OrderBy(o => o.Mother.Id).First();
        }

        /// <summary>
        /// A config with no scheduled pass, so tests decide when rules are evaluated.
        /// </summary>
        static CustomDataComposer Data()
        {
            return new CustomDataComposer().With("general", "ruleInterval", "100000");
        }

        static Construct Build(CustomDataComposer askerData, Spec asker, params Spec[] owners)
        {
            var construct = new Construct { World = WorldFactory().Boot() };
            var grid = construct.World.CreateGrid("Construct");

            foreach (var spec in owners)
            {
                var script = construct.World.CreateScript(grid)
                    .WithMother()
                    .WithBlocks(spec.Blocks)
                    .OnNetwork();

                if (spec.Rec != null)
                    script.WithCommands(spec.Rec);

                if (spec.Vocabulary != null)
                    script.WithModules(spec.Vocabulary);

                construct.Owners.Add(script.Boot());
            }

            asker = asker ?? new Spec();

            var builder = construct.World.CreateScript(grid, "Asker")
                .WithMother()
                .WithCustomData((askerData ?? Data()).Build())
                .WithCommands(construct.Rec)
                .WithBlocks(asker.Blocks)
                .OnNetwork();

            if (asker.Vocabulary != null)
                builder.WithModules(asker.Vocabulary);

            construct.Asker = builder.Boot();

            // Each script's catalogue has not seen the programmable blocks of the scripts booted after it
            foreach (var script in construct.All)
            {
                script.Mother.GetModule<BlockCatalogue>().RefreshConstruct();

                for (int i = 0; i < 20; i++)
                    Advance(script, 0);
            }

            return construct;
        }

        static Construct Of(params Spec[] owners) => Build(null, null, owners);

        static void Sync(Construct c)
        {
            foreach (var script in c.All)
                script.Mother.GetModule<IntergridMessageService>().ConstructPing();

            c.World.DeliverMessages();

            // The rules rebuild when the names change, one rule per cycle
            foreach (var script in c.All)
                for (int i = 0; i < 20; i++)
                    Advance(script, 0);
        }

        /// <summary>
        /// Moves a script's module clock on without running a rule pass.
        /// </summary>
        static void Advance(Script<CoreTestProgram> script, double seconds)
        {
            var runtime = (FakeGridProgramRuntimeInfo)script.Mother.Program.Runtime;
            runtime.TimeSinceLastRun = TimeSpan.FromSeconds(seconds);
            script.Mother.GetModule<Clock>().Run();
        }

        /// <summary>
        /// Lets time pass, runs a rule pass on the asker, delivers the requests
        /// and their answers, and runs the routines the pass started.
        /// </summary>
        static void Pass(Construct c, double seconds = 1)
        {
            Advance(c.Asker, seconds);
            c.AskerCondition.EvaluateRules();
            c.World.DispatchIgc();
            c.World.DispatchIgc();

            for (int i = 0; i < 3; i++)
                Advance(c.Asker, 0);
        }

        static void Deliver(Construct c)
        {
            c.World.DispatchIgc();
            c.World.DispatchIgc();
        }

        static List<Request> CondRequests(Construct c, Script<CoreTestProgram> from)
        {
            return c.World.SentMessages
                .Where(m => !m.IsBroadcast && m.SourceId == from.IGC.Me && $"{m.Data}".StartsWith("REQUEST::"))
                .Select(m => Request.Deserialize($"{m.Data}"))
                .Where(r => r.HString("Path") == "cond")
                .ToList();
        }

        static int Asked(Construct c) => CondRequests(c, c.Asker).Count;

        static int Logged(Script<CoreTestProgram> script, string fragment)
        {
            return script.Mother.GetModule<Log>().Records.Count(record => record.Contains(fragment));
        }

        static FakeBatteryBlock Battery(string name, double charge)
        {
            return new FakeBatteryBlock(customName: name) { CurrentStoredPower = (float)(charge * 100), MaxStoredPower = 100 };
        }

        static FakeCockpit Cockpit(double? elevation)
        {
            return new FakeCockpit(customName: "Cockpit") { PlanetElevation = elevation };
        }

        static int EventsOf<T>(Script<CoreTestProgram> script) where T : IEvent
        {
            return script.Mother.GetModule<EventBus>().Emissions.Count(e => e.Event is T);
        }

        // --- Phase 1: advertise names ---

        [Test]
        public void Names_Lists_Properties_Without_Bands()
        {
            var c = Of(Node(Fake));
            var props = c.Owners[0].Mother.GetModule<ConditionModule>().Names(false).Split(',');

            Assert.That(props, Does.Contain("charge"));
            Assert.That(props, Does.Contain("altitude"));
            Assert.That(props, Does.Contain("enabled"));
        }

        [Test]
        public void Names_Lists_Predicates_And_State_Words_Without_Bands()
        {
            var c = Of(Node(Fake));
            var preds = c.Owners[0].Mother.GetModule<ConditionModule>().Names(true).Split(',');

            Assert.That(preds, Does.Contain("low"));
            Assert.That(preds, Does.Contain("on"));
            Assert.That(preds, Does.Contain("locked"));
            Assert.That(preds.Any(n => n.EndsWith(".band")), Is.False);
        }

        [Test]
        public void A_Sync_Gives_Each_Script_The_Names_Of_The_Other()
        {
            var c = Of(Node(Fake));
            var owner = c.Owners[0];

            Sync(c);

            var vocab = c.AskerService.ConstructVocab;

            Assert.That(vocab.ContainsKey(owner.Mother.Id), Is.True);
            Assert.That(vocab[owner.Mother.Id], Does.Contain("altitude"));
            Assert.That(vocab[owner.Mother.Id], Does.Contain("low"));
            Assert.That(vocab[owner.Mother.Id], Does.Contain("locked"));

            // The asker defines nothing beyond the engine's own vocabulary
            Assert.That(owner.Mother.GetModule<IntergridMessageService>().ConstructVocab[c.Asker.Mother.Id], Does.Not.Contain("altitude"));
        }

        [Test]
        public void A_Script_Does_Not_Store_Its_Own_Names()
        {
            var c = Of(Node(Fake));

            Sync(c);

            Assert.That(c.AskerService.ConstructVocab.ContainsKey(c.Asker.Mother.Id), Is.False);
            Assert.That(c.Owners[0].Mother.GetModule<IntergridMessageService>().ConstructVocab.ContainsKey(c.OwnerId), Is.False);
        }

        [Test]
        public void A_Changed_Set_Emits_The_Event_Once_And_An_Unchanged_Set_Emits_Nothing()
        {
            var c = Of(Node(Fake));

            Sync(c);
            Assert.That(EventsOf<ConstructVocabChangedEvent>(c.Asker), Is.EqualTo(1));

            Sync(c);
            Assert.That(EventsOf<ConstructVocabChangedEvent>(c.Asker), Is.EqualTo(1));

            c.Owners[0].Mother.GetModule<ConditionModule>().RegisterPredicate("newname", "charge < 1%");
            Sync(c);

            Assert.That(EventsOf<ConstructVocabChangedEvent>(c.Asker), Is.EqualTo(2));
            Assert.That(c.AskerService.ConstructVocab[c.OwnerId], Does.Contain("newname"));
        }

        [Test]
        public void A_Script_Heard_For_The_First_Time_With_No_Names_Emits_Nothing()
        {
            var c = Of();
            var request = new Request(
                new Dictionary<string, object> { { "Commands", "" } },
                new Dictionary<string, object> { { "Id", "x" }, { "OriginId", "555" }, { "OriginName", "Other" }, { "Path", "sync" } });

            c.AskerService.HandleIncomingIGCMessage(new MyIGCMessage(request.Serialize(), ".construct", 555));

            Assert.That(EventsOf<ConstructVocabChangedEvent>(c.Asker), Is.EqualTo(0));
        }

        // --- Phase 2: resolve remote names ---

        [Test]
        public void A_Rule_On_A_Name_Only_Another_Script_Defines_Is_Reported_Once_And_Works_After_The_Sync()
        {
            var data = Data().With("rules", "approach", "when grid altitude < 100; rec A");
            var c = Build(data, null, Node(Fake, Cockpit(50)));

            Assert.That(Logged(c.Asker, "Rule approach disabled: unknown property: altitude"), Is.EqualTo(1));

            Sync(c);
            Pass(c);
            Pass(c);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec A" }));
            Assert.That(Logged(c.Asker, "Rule approach disabled"), Is.EqualTo(1));
        }

        [Test]
        public void A_Name_No_Script_Defines_Is_Reported_Once_With_Why_And_Nothing_Is_Sent()
        {
            var data = Data().With("rules", "bad", "when grid bogus < 100; rec A");
            var c = Build(data, null, Node(Fake, Cockpit(50)));

            Sync(c);
            Sync(c);
            Pass(c);

            Assert.That(Logged(c.Asker, "unknown property: bogus (no script on the construct has it)"), Is.EqualTo(1));
            Assert.That(c.Rec.Calls, Is.Empty);
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void A_Local_Name_Is_Never_Sent_Out()
        {
            var data = Data().With("rules", "low", "when \"Bat\" charge < 20%; rec A");
            var c = Build(data, Node(Fake, Battery("Bat", 0.1)), Node(Fake));

            Sync(c);
            Pass(c);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec A" }));
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void A_Variable_Only_Another_Script_Defines_Is_Not_Sent_Out()
        {
            var data = Data()
                .With("rules", "weak", "when \"$GONE\" low; rec A")
                .With("rules", "limit", "when grid altitude < $LIMIT; rec B");
            var c = Build(data, null, Node(Fake, Cockpit(50), Battery("Bat", 0.1)));

            Sync(c);
            Pass(c);
            Pass(c);

            Assert.That(Logged(c.Asker, "Rule weak disabled: no target: $GONE"), Is.EqualTo(1));
            Assert.That(Logged(c.Asker, "Rule limit disabled: unknown variable in: grid altitude < $LIMIT"), Is.EqualTo(1));
            Assert.That(c.Rec.Calls, Is.Empty);
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void A_Condition_Is_Unanswered_When_No_Local_Block_Answers_A_Row()
        {
            var tank = new FakeGasTank(customName: "Tank") { FilledRatio = 0.5 };
            var c = Build(null, Node(Tanks, Battery("Bat", 0.1), tank));
            var condition = c.AskerCondition;
            string error;

            // low is only defined for tanks here, so a battery is unanswered
            Assert.That(condition.Check(new TerminalCommand("if \"Bat\" low"), false, false, out error), Is.EqualTo(-1));
            Assert.That(error, Is.Null);

            Assert.That(condition.Check(new TerminalCommand("if \"Tank\" low"), false, false, out error), Is.EqualTo(0));

            tank.FilledRatio = 0.1;
            Assert.That(condition.Check(new TerminalCommand("if \"Tank\" low"), false, false, out error), Is.EqualTo(1));
        }

        [Test]
        public void An_Inline_If_Evaluate_Stays_Local_And_Reports_A_Remote_Name_As_Unknown()
        {
            var c = Build(null, Node(null, Battery("Bat", 0.5)), Node(Fake));
            string error;

            Sync(c);
            c.AskerCondition.Evaluate(new TerminalCommand("if \"Bat\" altitude < 100"), out error);

            Assert.That(error, Is.EqualTo("unknown property: altitude"));
        }

        // --- Phase 3: the cond route and batching ---

        [Test]
        public void Eight_Conditions_For_One_Owner_Are_One_Request()
        {
            var c = Of(Node(Numbers("a")));

            Sync(c);
            c.World.Network.ClearSentMessages();

            for (int i = 0; i < 8; i++)
                c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < " + (10 + i), false);

            c.AskerCondition.Flush();

            var requests = CondRequests(c, c.Asker);

            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].BString("C0"), Is.EqualTo("\"Bat\" a < 10"));
            Assert.That(requests[0].BString("C7"), Is.EqualTo("\"Bat\" a < 17"));
            Assert.That(requests[0].Body.ContainsKey("C8"), Is.False);
        }

        [Test]
        public void Conditions_For_Two_Owners_Are_Two_Requests()
        {
            var c = Of(Node(Numbers("a")), Node(Numbers("b")));

            Sync(c);
            c.World.Network.ClearSentMessages();

            c.AskerCondition.Ask(c.Owners[0].Mother.Id, "\"Bat\" a < 10", false);
            c.AskerCondition.Ask(c.Owners[1].Mother.Id, "\"Bat\" b < 10", false);
            c.AskerCondition.Flush();

            var requests = CondRequests(c, c.Asker);

            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests.Select(r => r.HString("Id")).Distinct().Count(), Is.EqualTo(2), "Requests created together have their own ids");
        }

        [Test]
        public void Twelve_Conditions_Split_Into_Two_Requests()
        {
            var c = Of(Node(Numbers("a")));

            Sync(c);
            c.World.Network.ClearSentMessages();

            for (int i = 0; i < 12; i++)
                c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < " + (10 + i), false);

            c.AskerCondition.Flush();

            var requests = CondRequests(c, c.Asker);

            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[0].Body.ContainsKey("C9"), Is.True);
            Assert.That(requests[0].Body.ContainsKey("C10"), Is.False);
            Assert.That(requests[1].BString("C1"), Is.EqualTo("\"Bat\" a < 21"));
            Assert.That(requests[1].Body.ContainsKey("C2"), Is.False);
        }

        [Test]
        public void An_Identical_Condition_Is_Sent_Once_And_Every_Asker_Gets_The_Result()
        {
            var c = Of(Node(Numbers("a"), Battery("Bat", 0.5)));
            var results = new List<string>();

            Sync(c);
            c.World.Network.ClearSentMessages();

            c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < 10", false, results.Add);
            c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < 10", false, results.Add);
            c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < 10", true);
            c.AskerCondition.Flush();

            var requests = CondRequests(c, c.Asker);

            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Body.ContainsKey("C1"), Is.True, "A different tripped state is a different condition");
            Assert.That(requests[0].Body.ContainsKey("C2"), Is.False);

            Deliver(c);

            Assert.That(results, Is.EqualTo(new[] { "1", "1" }));
        }

        [Test]
        public void The_Owner_Replies_With_One_Result_For_Each_Condition()
        {
            var c = Of(Node(Numbers("a"), Battery("Bat", 0.5)));
            var results = new Dictionary<string, string>();

            Sync(c);

            foreach (var text in new[] { "\"Bat\" a < 10", "\"Bat\" a > 10", "\"Nope\" a < 10", "\"Bat\" bogus < 1", "grid a < 1" })
            {
                var condition = text;
                c.AskerCondition.Ask(c.OwnerId, condition, false, r => results[condition] = r);
            }

            c.AskerCondition.Flush();
            Deliver(c);

            Assert.That(results["\"Bat\" a < 10"], Is.EqualTo("1"));
            Assert.That(results["\"Bat\" a > 10"], Is.EqualTo("0"));
            Assert.That(results["\"Nope\" a < 10"], Is.EqualTo("!no target: Nope"));
            Assert.That(results["\"Bat\" bogus < 1"], Is.EqualTo("!unknown property: bogus"));
            Assert.That(results["grid a < 1"], Is.EqualTo("!no target: grid"));
        }

        [Test]
        public void A_Reply_Is_Not_Lost_When_Another_Message_Is_Handled_First()
        {
            var c = Of(Node(Numbers("a"), Battery("Bat", 0.5)));
            string result = null;

            Sync(c);
            c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < 10", false, r => result = r);
            c.AskerCondition.Flush();
            c.World.DispatchIgc();

            // Handling any incoming message clears the requests that wait for a plain callback
            c.AskerService.HandleIncomingIGCMessages();
            c.World.DispatchIgc();

            Assert.That(result, Is.EqualTo("1"));
        }

        [Test]
        public void A_Request_Nobody_Answers_Is_Dropped_After_Two_Syncs()
        {
            var c = Of(Node(Numbers("a"), Battery("Bat", 0.5)));
            string result = null;

            Sync(c);
            c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < 10", false, r => result = r);
            c.AskerCondition.Flush();
            c.World.DispatchIgc();

            c.AskerService.ConstructPing();
            c.AskerService.ConstructPing();
            c.World.DispatchIgc();

            Assert.That(result, Is.Null);
        }

        [Test]
        public void A_Condition_No_Block_Answers_Replies_With_A_Question_Mark()
        {
            var c = Of(Node(Fake, Cockpit(null)));
            string result = null;

            Sync(c);
            c.AskerCondition.Ask(c.OwnerId, "grid altitude < 100", false, r => result = r);
            c.AskerCondition.Flush();
            Deliver(c);

            Assert.That(result, Is.EqualTo("?"));
        }

        [Test]
        public void The_Band_Applies_On_The_Owner_When_The_Asker_Is_Tripped()
        {
            var c = Of(Node(Fake, Cockpit(110)));
            string free = null, tripped = null;

            Sync(c);
            c.AskerCondition.Ask(c.OwnerId, "grid altitude < 100 --band=20", false, r => free = r);
            c.AskerCondition.Ask(c.OwnerId, "grid altitude < 100 --band=20", true, r => tripped = r);
            c.AskerCondition.Flush();
            Deliver(c);

            Assert.That(free, Is.EqualTo("0"));
            Assert.That(tripped, Is.EqualTo("1"), "110 is between the limit and the band");
        }

        static int RepliesTo(Construct c) => c.World.SentMessages.Count(m => !m.IsBroadcast && m.TargetId == c.Asker.IGC.Me);

        [Test]
        public void A_Request_On_Another_Channel_Gets_No_Reply_And_Is_Not_Evaluated()
        {
            var c = Of(Node(Numbers("a"), Battery("Bat", 0.5)));
            var service = c.Owners[0].Mother.GetModule<IntergridMessageService>();

            Sync(c);
            c.World.Network.ClearSentMessages();
            NumbersVocabulary.Probes = 0;

            var request = c.AskerService.CreateRequest("cond", new Dictionary<string, object> { { "C0", "\"Bat\" probe > 0" }, { "T0", "0" } });

            service.HandleIncomingIGCMessage(new MyIGCMessage(request.Serialize(), "alpha", c.Asker.IGC.Me));

            Assert.That(RepliesTo(c), Is.EqualTo(0));
            Assert.That(NumbersVocabulary.Probes, Is.EqualTo(0));

            service.HandleIncomingIGCMessage(new MyIGCMessage(request.Serialize(), ".construct", c.Asker.IGC.Me));

            Assert.That(RepliesTo(c), Is.EqualTo(1), "The same request on the construct channel is answered");
            Assert.That(NumbersVocabulary.Probes, Is.GreaterThan(0));
        }

        [Test]
        public void A_Request_From_An_Origin_Not_On_The_Construct_Gets_No_Reply_And_Is_Not_Evaluated()
        {
            var c = Of(Node(Numbers("a"), Battery("Bat", 0.5)));
            var service = c.Owners[0].Mother.GetModule<IntergridMessageService>();

            Sync(c);
            c.World.Network.ClearSentMessages();
            NumbersVocabulary.Probes = 0;

            var request = c.AskerService.CreateRequest(
                "cond",
                new Dictionary<string, object> { { "C0", "\"Bat\" probe > 0" }, { "T0", "0" } },
                new Dictionary<string, object> { { "OriginId", "987654321" } });

            service.HandleIncomingIGCMessage(new MyIGCMessage(request.Serialize(), ".construct", 987654321));

            Assert.That(c.World.SentMessages.Count(m => !m.IsBroadcast), Is.EqualTo(0));
            Assert.That(NumbersVocabulary.Probes, Is.EqualTo(0));
        }

        [Test]
        public void A_Request_Never_Runs_A_Command()
        {
            var ownerRec = new RecordingCommand();
            var owner = Node(Numbers("a"), Battery("Bat", 0.5));
            string result = null;

            owner.Rec = ownerRec;

            var c = Of(owner);

            Sync(c);

            c.AskerCondition.Ask(c.OwnerId, "\"Bat\" a < 10; rec A", false, r => result = r);
            c.AskerCondition.Ask(c.OwnerId, "rec A", false);
            c.AskerCondition.Flush();
            Deliver(c);
            c.Owners[0].RunToIdle();

            Assert.That(result, Does.StartWith("!"));
            Assert.That(ownerRec.Calls, Is.Empty);
        }

        // --- Phase 4: remote when rules ---

        const string Approach = "when grid altitude < 100 --band=20 --for=2; rec ON; else; rec OFF";

        static Construct Approaching(double elevation, out FakeCockpit cockpit, string rule = Approach)
        {
            cockpit = Cockpit(elevation);

            return Build(Data().With("rules", "approach", rule), null, Node(Fake, cockpit));
        }

        [Test]
        public void A_Remote_Rule_Fires_When_The_Owner_Answers_And_Holds_For()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit);

            Sync(c);
            Pass(c, 1);
            Assert.That(c.Rec.Calls, Is.Empty, "The first pass has no answer yet");

            Pass(c, 1);
            Pass(c, 1);
            Assert.That(c.Rec.Calls, Is.Empty, "It tripped but --for has not passed");

            Pass(c, 1);
            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }));
        }

        [Test]
        public void A_Remote_Rule_Resets_Above_The_Band_Using_Tripped()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit);

            Sync(c);

            for (int i = 0; i < 5; i++)
                Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }));

            cockpit.PlanetElevation = 110;

            for (int i = 0; i < 3; i++)
                Pass(c, 1);

            var requests = CondRequests(c, c.Asker);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }), "110 is inside the band, and the owner is told the rule is tripped");
            Assert.That(requests.First().BString("T0"), Is.EqualTo("0"));
            Assert.That(requests.Last().BString("T0"), Is.EqualTo("1"));

            cockpit.PlanetElevation = 130;

            for (int i = 0; i < 3; i++)
                Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON", "rec OFF" }));
        }

        [Test]
        public void A_Remote_Request_Leaves_Out_The_Timer_Options_And_Keeps_Band_And_At()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit, "when grid altitude < 100 --band=20 --for=2 --cooldown=1; rec ON");

            Sync(c);
            Pass(c, 1);

            Assert.That(CondRequests(c, c.Asker).Single().BString("C0"), Is.EqualTo("grid altitude < 100 --band=20"));
        }

        [Test]
        public void A_Rule_That_Is_Not_Due_Sends_Nothing()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit, "when grid altitude < 100 --every=60; rec ON");

            Sync(c);
            c.World.Network.ClearSentMessages();

            Pass(c, 1);
            Assert.That(Asked(c), Is.EqualTo(0), "Not due for the first 60 seconds");

            Pass(c, 60);
            Assert.That(Asked(c), Is.EqualTo(1));

            Pass(c, 1);
            Pass(c, 1);
            Assert.That(Asked(c), Is.EqualTo(1), "Not due again for 60 seconds");

            Pass(c, 60);
            Assert.That(Asked(c), Is.EqualTo(2));
        }

        [Test]
        public void Rules_For_One_Owner_Share_A_Request()
        {
            var data = Data();

            for (int i = 0; i < 8; i++)
                data.With("rules", "r" + i, "when \"Bat\" a < " + (10 + i) + "; rec R" + i);

            var c = Build(data, null, Node(Numbers("a"), Battery("Bat", 0.5)));

            Sync(c);
            c.World.Network.ClearSentMessages();
            Pass(c, 1);

            Assert.That(Asked(c), Is.EqualTo(1));

            Pass(c, 1);

            Assert.That(c.Rec.Calls.Count, Is.EqualTo(8));
        }

        [Test]
        public void Another_Scripts_Rules_Do_Not_Run_Here()
        {
            var world = WorldFactory().Boot();
            var grid = world.CreateGrid("Construct");
            var battery = new FakeBatteryBlock(customName: "Bat") { CurrentStoredPower = 10, MaxStoredPower = 100 };
            var ownRec = new RecordingCommand();
            var otherRec = new RecordingCommand();

            // Both scripts see the battery and the other's programmable block
            var owner = world.CreateScript(grid, "Owner")
                .WithMother()
                .WithModules(m => new FakeConditionVocabulary(m))
                .WithCustomData(Data().With("rules", "low", "when \"Bat\" low; rec A").Build())
                .WithCommands(ownRec)
                .WithBlocks(battery)
                .OnNetwork()
                .Boot();
            var other = world.CreateScript(grid, "Other")
                .WithMother()
                .WithModules(m => new FakeConditionVocabulary(m))
                .WithCustomData(Data().Build())
                .WithCommands(otherRec)
                .OnNetwork()
                .Boot();

            foreach (var script in new[] { owner, other })
            {
                script.Mother.GetModule<BlockCatalogue>().RefreshConstruct();

                for (int i = 0; i < 20; i++)
                    Advance(script, 0);
            }

            foreach (var script in new[] { owner, other })
            {
                script.Mother.GetModule<ConditionModule>().EvaluateRules();
                script.RunToIdle();
            }

            Assert.That(ownRec.Calls, Is.EqualTo(new[] { "rec A" }));
            Assert.That(otherRec.Calls, Is.Empty, "Its rules belong to the script that owns the programmable block");
        }

        [Test]
        public void An_Owner_That_Leaves_Makes_The_Rule_False_With_One_Log_Line()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit, "when grid altitude < 100; rec ON; else; rec OFF");

            Sync(c);
            Pass(c, 1);
            Pass(c, 1);
            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }));

            c.AskerService.ConstructVocab.Remove(c.OwnerId);

            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON", "rec OFF" }));
            Assert.That(Logged(c.Asker, "left the construct"), Is.EqualTo(1));
        }

        [Test]
        public void An_Answer_Older_Than_Three_Syncs_Is_False_With_One_Log_Line()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit, "when grid altitude < 100 --every=1; rec ON; else; rec OFF");

            Sync(c);
            Pass(c, 1.1);
            Pass(c, 1.1);
            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }));
            for (int i = 0; i < 20; i++)
            {
                Advance(c.Asker, 1);
                c.AskerCondition.EvaluateRules();
                Advance(c.Asker, 0);
            }

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON", "rec OFF" }));
            Assert.That(Logged(c.Asker, "no answer from script"), Is.EqualTo(1));
        }

        [Test]
        public void Owners_That_Both_Answer_With_A_Question_Mark_Give_False()
        {
            var data = Data().With("rules", "r", "when \"Bat\" p < 100; rec ON");
            var none = new Reading { Value = double.NaN };
            var c = Build(data, null, Node(Numbers(none, "p"), Battery("Bat", 0.5)), Node(Numbers(none, "p")));

            Sync(c);
            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.Empty);
            Assert.That(Asked(c), Is.EqualTo(4), "Both owners are asked in each pass");
        }

        [Test]
        public void The_Lowest_Owner_That_Has_A_Block_That_Answers_Decides()
        {
            var data = Data().With("rules", "r", "when \"Bat\" p < 100; rec ON");
            var first = new Reading { Value = 500 };
            var second = new Reading { Value = 500 };
            var c = Build(data, null, Node(Numbers(first, "p"), Battery("Bat", 0.5)), Node(Numbers(second, "p")));
            var lowest = c.Lowest == c.Owners[0] ? first : second;
            var highest = lowest == first ? second : first;

            highest.Value = 5;
            Sync(c);
            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.Empty, "The lowest owner answers 0 and decides");

            lowest.Value = double.NaN;
            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }), "The lowest owner answers ?, so the next one decides");
        }

        [Test]
        public void A_Split_Predicate_Asks_The_Owner_When_No_Local_Block_Answers()
        {
            var data = Data().With("rules", "lowBattery", "when \"Bat\" low; rec A");
            var c = Build(data, Node(Tanks, Battery("Bat", 0.1)), Node(Fake));

            Sync(c);
            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec A" }));
            Assert.That(CondRequests(c, c.Asker).First().BString("C0"), Is.EqualTo("\"Bat\" low"));
        }

        [Test]
        public void A_Local_Answer_Is_Never_Overridden()
        {
            var data = Data().With("rules", "lowTank", "when \"Tank\" low; rec A");
            var tank = new FakeGasTank(customName: "Tank") { FilledRatio = 0.5 };
            var c = Build(data, Node(Tanks, tank), Node(Fake));

            Sync(c);
            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.Empty);
            Assert.That(Asked(c), Is.EqualTo(0));

            tank.FilledRatio = 0.1;
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec A" }));
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void A_Remote_Rule_Acts_On_The_Answer_As_It_Arrives_Not_A_Pass_Later()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit, "when grid altitude < 100; rec ON; else; rec OFF");

            Sync(c);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }));
            Assert.That(Asked(c), Is.EqualTo(1), "The answer does not ask again");
        }

        [Test]
        public void A_Remote_Rule_Counts_For_From_When_The_Answer_Arrives()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit, "when grid altitude < 100 --for=1.5; rec ON; else; rec OFF");

            Sync(c);
            var seen = new List<int>();

            for (int i = 0; i < 4; i++)
            {
                Pass(c, 1);
                seen.Add(c.Rec.Calls.Count);
            }

            Assert.That(string.Join(",", seen), Is.EqualTo("0,0,1,1"), "It tripped when the first answer arrived, so it holds from then");
        }

        [Test]
        public void A_Remote_Rule_Keeps_Its_State_When_Rules_Are_Rebuilt()
        {
            FakeCockpit cockpit;
            var c = Approaching(50, out cockpit, "when grid altitude < 100; rec ON; else; rec OFF");

            Sync(c);
            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }));

            // A changed set from a script rebuilds the rules
            c.Owners[0].Mother.GetModule<ConditionModule>().RegisterPredicate("newname", "altitude < 1");
            Sync(c);
            Pass(c, 1);
            Pass(c, 1);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec ON" }), "It does not fire again");
        }

        // --- Phase 5: remote if and elseif ---

        /// <summary>
        /// One game cycle on the asker, then the messages in flight are delivered.
        /// </summary>
        static void Cycle(Construct c, double seconds = 0.1)
        {
            Advance(c.Asker, seconds);
            Deliver(c);
        }

        static void Cycles(Construct c, int count, double seconds = 0.1)
        {
            for (int i = 0; i < count; i++)
                Cycle(c, seconds);
        }

        static Construct Altitude(double elevation, CustomDataComposer data = null)
        {
            return Build(data, null, Node(Fake, Cockpit(elevation)));
        }

        /// <summary>
        /// Starts a routine without running a cycle, so several can start in the same one.
        /// </summary>
        static void Start(Construct c, string routine)
        {
            c.Asker.Mother.GetModule<CommandBus>().RunTerminalCommand(routine);
        }

        [Test]
        public void A_Remote_If_Pauses_The_Routine_Then_Runs_The_True_Branch()
        {
            var c = Altitude(50);

            Sync(c);
            c.Asker.RunTerminal("rec BEFORE; if grid altitude < 100; rec T; else; rec F; end; rec AFTER");

            Cycle(c);
            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec BEFORE" }), "Waiting for the owner");

            Cycles(c, 5);
            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec BEFORE", "rec T", "rec AFTER" }));
            Assert.That(Asked(c), Is.EqualTo(1));
        }

        [Test]
        public void A_Remote_If_Runs_The_False_Branch_When_The_Owner_Answers_No()
        {
            var c = Altitude(500);

            Sync(c);
            c.Asker.RunTerminal("if grid altitude < 100; rec T; else; rec F; end; rec AFTER");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec F", "rec AFTER" }));
        }

        [Test]
        public void Steps_After_A_Remote_If_Do_Not_Run_Before_The_Reply()
        {
            var c = Altitude(50);

            Sync(c);
            c.Asker.RunTerminal("if grid altitude < 100; rec T; end; rec AFTER");

            // Time passes but the request never reaches the owner
            for (int i = 0; i < 5; i++)
                Advance(c.Asker, 0.1);

            Assert.That(c.Rec.Calls, Is.Empty);

            Cycles(c, 4);
            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec T", "rec AFTER" }));
        }

        [Test]
        public void A_Remote_Elseif_Runs_When_The_Branch_Before_It_Did_Not()
        {
            var c = Altitude(50);

            Sync(c);
            c.Asker.RunTerminal("if grid altitude > 100; rec A; elseif grid altitude < 100; rec B; else; rec C; end");
            Cycles(c, 10);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec B" }));
            Assert.That(Asked(c), Is.EqualTo(2));
        }

        [Test]
        public void An_Elseif_After_A_Branch_Ran_Sends_No_Request()
        {
            var c = Altitude(50);

            Sync(c);
            c.Asker.RunTerminal("if grid altitude < 100; rec A; elseif grid altitude > 100; rec B; end");
            Cycles(c, 10);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec A" }));
            Assert.That(Asked(c), Is.EqualTo(1));
        }

        [Test]
        public void A_Skipped_If_Sends_No_Request()
        {
            var c = Build(null, Node(Numbers("p"), Battery("Bat", 0.5)), Node(Fake, Cockpit(50)));

            Sync(c);
            c.Asker.RunTerminal("if \"Bat\" p < 1; if grid altitude < 100; rec A; end; end; rec AFTER");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec AFTER" }));
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void Two_Routines_In_One_Cycle_Share_A_Request_And_Each_Gets_Its_Own_Result()
        {
            var c = Altitude(50);

            Sync(c);
            Start(c, "if grid altitude < 100; rec LOW; end");
            Start(c, "if grid altitude > 100; rec HIGH; else; rec NOTHIGH; end");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EquivalentTo(new[] { "rec LOW", "rec NOTHIGH" }));
            Assert.That(Asked(c), Is.EqualTo(1));
            Assert.That(CondRequests(c, c.Asker).First().BString("C1"), Is.Not.Empty);
        }

        [Test]
        public void Identical_Conditions_From_Two_Routines_Are_Sent_Once()
        {
            var c = Altitude(50);

            Sync(c);
            Start(c, "if grid altitude < 100; rec A; end");
            Start(c, "if grid altitude < 100; rec B; end");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EquivalentTo(new[] { "rec A", "rec B" }));
            Assert.That(CondRequests(c, c.Asker).First().BString("C1"), Is.Empty);
        }

        [Test]
        public void A_Remote_If_Fails_The_Step_When_Nobody_Answers_In_Time()
        {
            var c = Altitude(50);

            Sync(c);
            c.Asker.RunTerminal("if grid altitude < 100; rec T; end; rec AFTER");

            // The owner never sees the request
            for (int i = 0; i < 25; i++)
                Advance(c.Asker, 0.1);

            Assert.That(c.Rec.Calls, Is.Empty);
            Assert.That(Logged(c.Asker, "Command failed: if grid altitude < 100 (no answer from script " + c.OwnerId + " for altitude)"), Is.EqualTo(1));
        }

        [Test]
        public void The_Wait_Is_The_Configured_Cond_Timeout()
        {
            var c = Altitude(50, Data().With("general", "condTimeout", "10"));

            Sync(c);
            c.Asker.RunTerminal("if grid altitude < 100; rec T; end");

            for (int i = 0; i < 40; i++)
                Advance(c.Asker, 0.1);

            Assert.That(Logged(c.Asker, "no answer from"), Is.EqualTo(0), "4 seconds is within 10");

            for (int i = 0; i < 70; i++)
                Advance(c.Asker, 0.1);

            Assert.That(Logged(c.Asker, "no answer from"), Is.EqualTo(1));
        }

        [Test]
        public void An_If_On_A_Name_No_Script_Owns_Fails_At_Once_Without_A_Request()
        {
            var c = Build(null, Node(null, Battery("Bat", 0.5)), Node(Fake));

            Sync(c);
            c.Asker.RunTerminal("if \"Bat\" nothing < 1; rec T; end; rec AFTER");
            Cycle(c);

            Assert.That(c.Rec.Calls, Is.Empty);
            Assert.That(Logged(c.Asker, "Command failed: if \"Bat\" nothing < 1 (unknown property: nothing)"), Is.EqualTo(1));
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void A_Local_If_Returns_In_The_Same_Step_With_No_Request()
        {
            var c = Build(null, Node(Fake, Battery("Bat", 0.1)), Node(Fake));

            Sync(c);
            c.Asker.RunTerminal("if \"Bat\" low; rec T; end; rec AFTER");
            Cycle(c);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec T", "rec AFTER" }));
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void A_Split_Predicate_In_An_If_Asks_The_Owner_When_No_Local_Block_Answers()
        {
            var c = Build(null, Node(Tanks, Battery("Bat", 0.1)), Node(Fake));

            Sync(c);
            c.Asker.RunTerminal("if \"Bat\" low; rec T; else; rec F; end");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec T" }));
            Assert.That(CondRequests(c, c.Asker).First().BString("C0"), Is.EqualTo("\"Bat\" low"));
        }

        [Test]
        public void A_Local_Answer_In_An_If_Is_Never_Overridden()
        {
            var tank = new FakeGasTank(customName: "Tank") { FilledRatio = 0.5 };
            var c = Build(null, Node(Tanks, tank), Node(Fake));

            Sync(c);
            c.Asker.RunTerminal("if \"Tank\" low; rec T; else; rec F; end");
            Cycles(c, 3);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec F" }));
            Assert.That(Asked(c), Is.EqualTo(0));
        }

        [Test]
        public void The_Lowest_Owner_That_Has_A_Block_That_Answers_Decides_An_If()
        {
            var first = new Reading { Value = 500 };
            var second = new Reading { Value = 500 };
            var c = Build(null, null, Node(Numbers(first, "p"), Battery("Bat", 0.5)), Node(Numbers(second, "p")));
            var lowest = c.Lowest == c.Owners[0] ? first : second;
            var highest = lowest == first ? second : first;

            lowest.Value = double.NaN;
            highest.Value = 5;
            Sync(c);
            c.Asker.RunTerminal("if \"Bat\" p < 100; rec T; else; rec F; end");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec T" }), "The lowest owner answers ?, so the next one decides");

            c.Rec.Calls.Clear();
            lowest.Value = 500;
            c.Asker.RunTerminal("if \"Bat\" p < 100; rec T; else; rec F; end");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec F" }), "The lowest owner answers 0 and decides");
        }

        [Test]
        public void An_If_Where_Every_Owner_Replies_With_A_Question_Mark_Is_False()
        {
            var c = Build(null, null, Node(Numbers(new Reading { Value = double.NaN }, "p"), Battery("Bat", 0.5)));

            Sync(c);
            c.Asker.RunTerminal("if \"Bat\" p < 100; rec T; else; rec F; end");
            Cycles(c, 6);

            Assert.That(c.Rec.Calls, Is.EqualTo(new[] { "rec F" }));
        }
    }
}
