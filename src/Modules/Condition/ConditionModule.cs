using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    /// <summary>
    /// Evaluates conditions of the form "target predicate" or
    /// "target property operator value". One condition grammar serves two
    /// bindings: polled "when" rules loaded from [rules], and the inline "if"
    /// step handled by the CommandBus (see Evaluate).
    ///
    /// Core only knows the engine and the vocabulary every block shares
    /// (enabled, working). Block specific properties, predicates and state
    /// words are registered by the extension module that owns the block, see
    /// BaseExtensionModule, and players can add or tune predicates in the
    /// [predicates] section of the Programmable Block's Custom Data.
    /// </summary>
    public class ConditionModule : BaseCoreModule
    {
        /// <summary>
        /// A comparison of one property against a value.
        /// </summary>
        class Term
        {
            public List<Func<IMyTerminalBlock, double>> Read;
            public Func<double, double, bool> Cmp;
            public bool Any, Ratio;

            /// <summary>
            /// Val is the trip threshold. Once tripped the term only resets past
            /// Val + Back, which is the band toward the trip side.
            /// </summary>
            public double Val, Back;
        }

        /// <summary>
        /// A parsed condition, and for a "when" rule its name, routines, options
        /// and state.
        /// </summary>
        class Rule
        {
            public string Name, Then = "", Else = "";
            public List<IMyTerminalBlock> Blocks;
            public readonly List<Term> Terms = new List<Term>();

            /// <summary>
            /// Active is the last fired result. Tripped is the condition after
            /// its band, before the --for debounce.
            /// </summary>
            public bool Active, Tripped;

            /// <summary>
            /// Seconds from --for, --cooldown and --every (0 when not set).
            /// </summary>
            public double For, Cooldown, Every;

            /// <summary>
            /// Times on the module clock: when it tripped, when it last fired
            /// and when --every makes it due next.
            /// </summary>
            public double Since, Due, LastFired = double.NegativeInfinity;

            /// <summary>
            /// Pending is true while the condition names something no script on
            /// the construct defines yet, so the rule is kept but never evaluated.
            /// </summary>
            public bool Pending;

            /// <summary>
            /// The scripts on the construct that define the name the condition is
            /// built on, by id, or null. They answer for the blocks this script
            /// has no vocabulary for. Cond is the condition text sent to them.
            /// </summary>
            public List<long> Owners;
            public string Cond;
        }

        /// <summary>
        /// Enum names compared by name. A word's value is its index. Extension
        /// modules register the words of the states they own.
        /// </summary>
        readonly List<string> Words = new List<string>();

        /// <summary>
        /// Options that need a "when" rule because they keep state.
        /// </summary>
        static readonly List<string> Timers = new List<string> { "for", "cooldown", "every" };

        static readonly Dictionary<string, Func<double, double, bool>> Ops = new Dictionary<string, Func<double, double, bool>>
        {
            { "<", (a, b) => a < b },
            { ">", (a, b) => a > b },
            { "<=", (a, b) => a <= b },
            { ">=", (a, b) => a >= b },
            { "==", (a, b) => a == b },
            { "!=", (a, b) => a != b },
        };

        /// <summary>
        /// Properties by name, with one selector per block type that has the
        /// property. A selector returns NaN for blocks that are not of its type,
        /// and those blocks are skipped. The first selector that answers wins.
        /// </summary>
        readonly Dictionary<string, List<Func<IMyTerminalBlock, double>>> Props = new Dictionary<string, List<Func<IMyTerminalBlock, double>>>();

        /// <summary>
        /// The property and block type pairs already registered, so a module that
        /// several modules share can register again harmlessly, and the names
        /// of the properties that aggregate a group with any instead of the
        /// average.
        /// </summary>
        readonly HashSet<string> Registered = new HashSet<string>();

        /// <summary>
        /// Predicates by name. A predicate is one or more "property operator
        /// value" rows joined by |, and the first row that a block in the target
        /// answers decides. Any enum word is also a predicate (status == word).
        /// Modules that register the same predicate add their rows to it. The
        /// default band of a predicate is kept under "name.band". The
        /// [predicates] section of the Custom Data writes here too, replacing
        /// the registered rows until the next boot.
        /// </summary>
        readonly Dictionary<string, string> Preds = new Dictionary<string, string>();

        /// <summary>
        /// Messages already printed, so a bad rule is reported once.
        /// </summary>
        readonly HashSet<string> Logged = new HashSet<string>();

        List<Rule> Rules = new List<Rule>();

        /// <summary>
        /// Conditions waiting to go to the scripts that answer them, by script
        /// id. Each is a "1" or "0" (the sender's tripped state) followed by the
        /// condition, and maps to what to do with its result. Sent in batches by
        /// Flush.
        /// </summary>
        readonly Dictionary<long, Dictionary<string, Action<string>>> Outbox = new Dictionary<long, Dictionary<string, Action<string>>>();

        /// <summary>
        /// The last answer of a script to a condition ("id|condition"): 1, 0 or
        /// -1 when it has no block that answers, and when it was heard. Until
        /// the first answer arrives Heard holds when it was first asked.
        /// </summary>
        readonly Dictionary<string, int> Replies = new Dictionary<string, int>();
        readonly Dictionary<string, double> Heard = new Dictionary<string, double>();

        /// <summary>
        /// Seconds without an answer after which a remote answer is dropped. The
        /// scripts on a construct sync every 5 seconds, so this is 3 syncs.
        /// </summary>
        const double Stale = 15;

        /// <summary>
        /// The most conditions in one request.
        /// </summary>
        const int Batch = 10;

        /// <summary>
        /// Seconds on the module clock, accumulated from Runtime.TimeSinceLastRun.
        /// Next is when the shared pass is due, and Fast is true while any rule
        /// has its own --every and so needs checking between shared passes.
        /// </summary>
        double Now, Next, Interval;
        bool Fast;

        /// <summary>
        /// Seconds an inline if waits for the scripts that answer it, from
        /// [general] condTimeout (default 2).
        /// </summary>
        double Timeout;

        BlockCatalogue BlockCatalogue;
        CommandBus CommandBus;
        Configuration Configuration;
        IntergridMessageService IMS;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public ConditionModule(Mother mother) : base(mother) { }

        /// <summary>
        /// Boot the module. Listens for config and construct changes, and
        /// schedules the rule pass every [general] ruleInterval seconds (default 0.5).
        /// </summary>
        public override void Boot()
        {
            BlockCatalogue = Mother.GetModule<BlockCatalogue>();
            CommandBus = Mother.GetModule<CommandBus>();
            Configuration = Mother.GetModule<Configuration>();
            IMS = Mother.GetModule<IntergridMessageService>();

            Rules = new List<Rule>();
            Logged.Clear();
            Outbox.Clear();
            Replies.Clear();
            Heard.Clear();
            Now = 0;
            Fast = false;

            // Extension modules boot after this one and register their own
            // vocabulary again, so a reboot starts from a clean slate.
            Props.Clear();
            Registered.Clear();
            Preds.Clear();
            Words.Clear();

            RegisterFlag<IMyFunctionalBlock>("enabled", b => b.Enabled, "on", "off");
            RegisterFlag<IMyTerminalBlock>("working", b => b.IsWorking, "working", "broken");

            Subscribe<SystemBootedEvent>();
            Subscribe<SystemConfigChangedEvent>();
            Subscribe<BlockConfigChangedEvent>();
            Subscribe<ConstructRefreshedEvent>();
            Subscribe<ConstructVocabChangedEvent>();

            if (!double.TryParse(Configuration.Get("general.ruleInterval"), out Interval) || Interval < 0)
                Interval = 0.5;

            if (!double.TryParse(Configuration.Get("general.condTimeout"), out Timeout) || Timeout <= 0)
                Timeout = 2;

            Next = Interval;
            Mother.GetModule<Clock>().Schedule(Tick);
        }

        /// <summary>
        /// Advances the module clock every cycle, and runs the shared pass when
        /// it is due. While a rule has its own --every, each cycle also checks
        /// those rules. Conditions queued by inline ifs go out every cycle.
        /// </summary>
        void Tick()
        {
            Now += Mother.Runtime.TimeSinceLastRun.TotalSeconds;
            bool shared = Now >= Next;

            if (shared)
                Next = Now + Interval;

            if (shared || Fast)
                EvaluateRules(shared);
            else
                Flush();
        }

        /// <summary>
        /// Rules are rebuilt whenever the config they come from, or the blocks
        /// they resolve to, may have changed.
        /// </summary>
        public override void HandleEvent(IEvent e, object eventData)
        {
            if (Mother.SystemState == Mother.SystemStates.WORKING)
                Rebuild();
        }

        /// <summary>
        /// Register a property readable in conditions. Several modules can
        /// register the same property for the block types they own, and the
        /// first one that answers for a block is used. Registering a property
        /// again for the same block type has no effect.
        /// </summary>
        /// <typeparam name="T">The block type that has the property, ie. IMyBatteryBlock.</typeparam>
        /// <param name="key">Lower case property name, ie. charge.</param>
        /// <param name="read">Reads the property from a block of type T.</param>
        /// <param name="any">True to aggregate a group with any instead of the average.</param>
        /// <returns>False when the property was already registered for the block type.</returns>
        public bool RegisterProperty<T>(string key, Func<T, double> read, bool any = false) where T : class
        {
            if (!Registered.Add(key + typeof(T)))
                return false;

            List<Func<IMyTerminalBlock, double>> readers;

            if (!Props.TryGetValue(key, out readers))
                Props[key] = readers = new List<Func<IMyTerminalBlock, double>>();

            readers.Add(b =>
            {
                var t = b as T;
                return t == null ? double.NaN : read(t);
            });

            if (any)
                Registered.Add(key);

            return true;
        }

        /// <summary>
        /// Register a predicate. Registering a name that exists adds the rows
        /// to it, so each module can contribute the rows for the blocks it owns.
        /// </summary>
        /// <param name="name">Lower case name, ie. low.</param>
        /// <param name="rows">"property operator value" rows joined by |, ie. charge &lt; 20%|fill &lt; 15%.</param>
        /// <param name="band">Default band, ie. 5%. Empty to leave it as it is.</param>
        public void RegisterPredicate(string name, string rows, string band = "")
        {
            string old;

            Preds[name] = Preds.TryGetValue(name, out old) ? old + "|" + rows : rows;

            if (band != "")
                Preds[name + ".band"] = band;
        }

        /// <summary>
        /// Register a yes or no property (1 or 0), and optionally the predicates
        /// for each answer, ie. <c>("detected", b => b.IsActive, "detected", "clear")</c>.
        /// A group is true when any block is. The predicates are only added the
        /// first time, so a module that several modules share can register them.
        /// </summary>
        /// <param name="yes">Predicate name for true, or null.</param>
        /// <param name="no">Predicate name for false, or null.</param>
        public void RegisterFlag<T>(string key, Func<T, bool> read, string yes = null, string no = null) where T : class
        {
            if (!RegisterProperty<T>(key, b => read(b) ? 1 : 0, true))
                return;

            if (yes != null)
                RegisterPredicate(yes, key + " == 1");

            if (no != null)
                RegisterPredicate(no, key + " == 0");
        }

        /// <summary>
        /// Register an enum state such as a door's status, ie. 
        /// <c>RegisterStatus&lt;IMyDoor, DoorStatus&gt;("status", b => b.Status)</c>.
        /// Its values are compared by name, and each name is also a predicate
        /// (status == word). A group matches when any block does.
        /// </summary>
        public void RegisterStatus<T, E>(string key, Func<T, E> read) where T : class
        {
            // A name that is already a word keeps its first index, which is what both lookups find
            foreach (var word in Enum.GetNames(typeof(E)))
                Words.Add(word.ToLower());

            RegisterProperty<T>(key, b => Words.IndexOf(read(b).ToString().ToLower()), true);
        }

        /// <summary>
        /// The names this script defines, for the sync other scripts on the
        /// construct use to resolve names they do not define.
        /// </summary>
        /// <param name="preds">True for predicates and state words, false for properties.</param>
        /// <returns>Lower case names joined by commas.</returns>
        public string Names(bool preds)
        {
            var names = new List<string>();

            if (preds)
            {
                names.AddRange(Preds.Keys);
                names.RemoveAll(n => n.EndsWith(".band"));
                names.AddRange(Words);
            }
            else
                names.AddRange(Props.Keys);

            return string.Join(",", names);
        }

        /// <summary>
        /// Evaluates an inline condition step once, against the vocabulary this
        /// script defines. Names only other scripts define are not asked of them
        /// here (see <see cref="Check"/>, and "when" rules).
        /// </summary>
        /// <param name="command">ie. "Batteries" low, ship speed > 30.</param>
        /// <param name="error">Why the condition is invalid, or null.</param>
        /// <returns>The result. False when there is an error.</returns>
        public bool Evaluate(TerminalCommand command, out string error)
        {
            return Check(command, false, false, out error) > 0;
        }

        /// <summary>
        /// Starts evaluating an inline condition step. A condition this script
        /// can answer, or that no script can, returns its result at once. When no
        /// block here answers and other scripts define the name, the condition
        /// is queued for them (see <see cref="Flush"/>) and the result comes
        /// from <paramref name="poll"/> once they reply.
        /// </summary>
        /// <param name="command">ie. ship altitude &lt; 100.</param>
        /// <param name="error">Why the condition is invalid, or null.</param>
        /// <param name="poll">Set when the result is 2. Returns null while waiting, then "1" or "0", or why it failed (no answer in time).</param>
        /// <returns>1 when true, 0 when false (or on error), and 2 when it must wait for a reply.</returns>
        public int Begin(TerminalCommand command, out string error, out Func<string> poll)
        {
            var rule = new Rule();
            poll = null;
            error = Parse(command, rule, false, true);

            if (error != null)
                return 0;

            int answer = Probe(rule, false);

            if (answer >= 0 || rule.Owners == null)
                return answer > 0 ? 1 : 0;

            var replies = new Dictionary<long, string>();
            var owners = new List<long>(rule.Owners);
            double end = Now + Timeout;
            string name = command.Arguments[1];

            foreach (var owner in owners)
            {
                long id = owner;
                Ask(id, rule.Cond, false, r => replies[id] = r);
            }

            // The lowest script id that has a block that answers decides, as for a rule
            poll = () =>
            {
                foreach (var owner in owners)
                {
                    string r;

                    if (!replies.TryGetValue(owner, out r))
                        return Now >= end ? $"no answer from script {owner} for {name}" : null;

                    if (r == "1" || r == "0")
                        return r;
                }

                return "0";
            };

            return 2;
        }

        /// <summary>
        /// Evaluates a condition, telling apart a false from a condition that no
        /// block answers. Names that this script does not define are not asked
        /// of other scripts.
        /// </summary>
        /// <param name="command">ie. "Batteries" low.</param>
        /// <param name="stateful">True to allow the --band option, as the script that holds a rule sends it.</param>
        /// <param name="tripped">True when the rule is already tripped, so its band applies.</param>
        /// <param name="error">Why the condition is invalid, or null.</param>
        /// <returns>1 when true, 0 when false and -1 when no block answers.</returns>
        public int Check(TerminalCommand command, bool stateful, bool tripped, out string error)
        {
            var rule = new Rule();
            error = Parse(command, rule, stateful, false);

            return error == null ? Probe(rule, tripped) : 0;
        }

        /// <summary>
        /// Answers a condition for another script on the construct.
        /// </summary>
        /// <param name="condition">The condition text, ie. ship altitude &lt; 100.</param>
        /// <param name="tripped">True when the asking rule is tripped, so its band applies.</param>
        /// <returns>"1", "0", "?" when no block here answers, or "!" and why the condition is invalid.</returns>
        public string Answer(string condition, bool tripped)
        {
            string error;

            try
            {
                int result = Check(new TerminalCommand("if " + condition), true, tripped, out error);

                return error != null 
                    ? "!" + error 
                    : result < 0 
                        ? "?" 
                        : result + "";
            }
            catch (Exception e)
            {
                return "!" + e.Message;
            }
        }

        /// <summary>
        /// Queues a condition for a script on the construct. The queue is sent
        /// by Flush, one request per script for up to 10 conditions, and the same
        /// condition queued twice is sent once.
        /// </summary>
        /// <param name="owner">The script id.</param>
        /// <param name="condition">The condition text.</param>
        /// <param name="tripped">True when the asking rule is tripped, so its band applies.</param>
        /// <param name="onResult">Receives "1", "0", "?" or "!" and why the condition is invalid, or null.</param>
        public void Ask(long owner, string condition, bool tripped, Action<string> onResult = null)
        {
            Dictionary<string, Action<string>> queue;
            string key = (tripped ? "1" : "0") + condition;

            if (!Outbox.TryGetValue(owner, out queue))
                Outbox[owner] = queue = new Dictionary<string, Action<string>>();

            Action<string> calls;
            queue.TryGetValue(key, out calls);
            queue[key] = calls + onResult;
        }

        /// <summary>
        /// Sends the queued conditions: one request for each script, split into
        /// groups of 10. The condition and tripped state are sent as C0, T0, C1,
        /// T1 and so on, and the results come back as R0, R1 in the same order.
        /// </summary>
        public void Flush()
        {
            foreach (var owner in Outbox)
            {
                var keys = new List<string>(owner.Value.Keys);

                for (int i = 0; i < keys.Count; i += Batch)
                {
                    var body = new Dictionary<string, object>();
                    var group = keys.GetRange(i, Math.Min(Batch, keys.Count - i));
                    long id = owner.Key;
                    var calls = owner.Value;

                    for (int j = 0; j < group.Count; j++)
                    {
                        body["C" + j] = group[j].Substring(1);
                        body["T" + j] = group[j][0] + "";
                    }

                    IMS.SendAwaitedRequest(id, IMS.CreateRequest("cond", body), response =>
                    {
                        if (response.HString("Status") != "200")
                            return;

                        for (int j = 0; j < group.Count; j++)
                        {
                            string result = response.BString("R" + j), text = group[j].Substring(1), key = id + "|" + text;

                            if (result == "")
                                continue;

                            Replies[key] = result == "1" ? 1 : result == "0" ? 0 : -1;
                            Heard[key] = Now;

                            if (result[0] == '!')
                                Log($"Script {id} cannot answer {text}: {result.Substring(1)}");

                            calls[group[j]]?.Invoke(result);

                            // A rule acts on its answer as it arrives, not a pass later
                            foreach (var rule in Rules)
                                if (rule.Cond == text && !rule.Pending && rule.Owners != null && rule.Owners.Contains(id))
                                    Run(rule, false);
                        }
                    });
                }
            }

            Outbox.Clear();
        }

        /// <summary>
        /// The answer of the scripts that define a rule's name, for a condition
        /// no block on this script answers. It queues the condition and uses
        /// the last answer received, which is false until the first arrives. The
        /// lowest script id that has a block that answers decides, and an owner
        /// that left the construct or has not answered for a while is skipped.
        /// </summary>
        bool Remote(Rule rule, bool tripped, bool ask = true)
        {
            if (rule.Owners == null)
                return false;

            // A rule's answer is as old as the pass that asked for it
            double limit = Stale + (rule.Every > 0 ? rule.Every : Interval);
            var asked = new List<long>();

            foreach (var owner in rule.Owners)
            {
                if (IMS.ConstructVocab.ContainsKey(owner))
                {
                    string key = owner + "|" + rule.Cond;

                    if (ask)
                        Ask(owner, rule.Cond, tripped);

                    asked.Add(owner);

                    if (!Heard.ContainsKey(key))
                        Heard[key] = Now;
                }
                else if (ask)
                    Log($"Rule {rule.Name}: script {owner} left the construct");
            }

            foreach (var owner in asked)
            {
                string key = owner + "|" + rule.Cond;
                int result;

                if (Now - Heard[key] > limit)
                {
                    Log($"Rule {rule.Name}: no answer from script {owner}");
                    continue;
                }

                // A lower script that has not answered yet decides, so wait for it
                if (!Replies.TryGetValue(key, out result))
                    return false;

                if (result >= 0)
                    return result == 1;
            }

            return false;
        }

        /// <summary>
        /// One pass over the rules. A rule trips when its condition is true
        /// (past its band) and fires after --for seconds. A false to true
        /// change runs the then routine, true to false runs the else routine,
        /// and --cooldown holds either back. A failing rule never stops the
        /// others.
        /// </summary>
        /// <param name="shared">False when only rules with their own --every are due.</param>
        public void EvaluateRules(bool shared = true)
        {
            foreach (var rule in Rules)
            {
                if (rule.Pending || (rule.Every > 0 ? Now < rule.Due : !shared))
                    continue;

                rule.Due = Now + rule.Every;
                Run(rule, true);
            }

            Flush();
        }

        /// <summary>
        /// Evaluates one rule and runs its then or else routine on a change.
        /// </summary>
        /// <param name="ask">False when re-evaluating on an answer that just arrived, so nothing is asked again.</param>
        void Run(Rule rule, bool ask)
        {
            try
            {
                int answer = Probe(rule, rule.Tripped);
                bool trip = answer > 0 || answer < 0 && Remote(rule, rule.Tripped, ask);

                if (trip != rule.Tripped)
                {
                    rule.Tripped = trip;
                    rule.Since = Now;
                }

                bool now = trip && Now - rule.Since >= rule.For;

                if (now == rule.Active || Now - rule.LastFired < rule.Cooldown)
                    return;

                rule.Active = now;
                rule.LastFired = Now;
                CommandBus.RunTerminalCommand(now ? rule.Then : rule.Else);
            }
            catch (Exception e)
            {
                Log($"Rule {rule.Name}: {e.Message}");
            }
        }

        /// <summary>
        /// Parses a value: an enum word, a number, or a ratio like 20%.
        /// </summary>
        bool Val(string text, out double value)
        {
            value = Words.IndexOf(text.ToLower());

            return value >= 0 || Pct(text, out value);
        }

        /// <summary>
        /// Parses a number or a ratio like 20%.
        /// </summary>
        static bool Pct(string text, out double value)
        {
            if (!double.TryParse(text.TrimEnd('%'), out value))
                return false;

            if (text.EndsWith("%"))
                value /= 100;

            return true;
        }

        /// <summary>
        /// Parses a condition into a rule and resolves its target. The options
        /// that keep state (--band, --for, --cooldown, --every) are only valid
        /// for a "when" rule.
        /// </summary>
        /// <param name="ask">True to let scripts on the construct answer for names this script does not define.</param>
        /// <returns>Why the condition is invalid, or null.</returns>
        string Parse(TerminalCommand command, Rule rule, bool stateful, bool ask)
        {
            var args = command.Arguments;

            if (args.Count != 2 && args.Count != 4)
                return "bad syntax";

            string at, band, pband = null;
            double n, atVal = 0;

            foreach (var key in command.Options.Keys)
            {
                if (key != "at" && key != "band" && !Timers.Contains(key))
                    return "bad syntax";

                if (!stateful && key != "at")
                    return $"--{key} only works on a when rule";
            }

            command.Options.TryGetValue("at", out at);
            command.Options.TryGetValue("band", out band);

            if (at != null && (args.Count == 4 || !Val(at, out atVal)))
                return args.Count == 4 ? "--at needs a predicate" : "bad option: --at";

            if (band != null && !(Pct(band, out n) && n >= 0))
                return "bad option: --band";

            if (stateful)
            {
                foreach (var key in Timers)
                {
                    string text;
                    n = 0;

                    if (command.Options.TryGetValue(key, out text) && !(double.TryParse(text, out n) && n >= 0))
                        return "bad option: --" + key;

                    if (key == "for")
                        rule.For = n;
                    else if (key == "cooldown")
                        rule.Cooldown = n;
                    else
                        rule.Every = n;
                }
            }

            // An unquoted ship is the grid's controller, a quoted one is a block
            bool ship = args[0] == "ship" && !command.CommandString.Contains("\"ship\"");

            rule.Blocks = ship ? new List<IMyTerminalBlock>() : BlockCatalogue.GetBlocksByName<IMyTerminalBlock>(args[0]);

            if (ship && Mother.ShipController != null)
                rule.Blocks.Add(Mother.ShipController);

            // The name the condition is built on: a property, or else a predicate
            string name = args[1].ToLower();
            bool known = args.Count == 4 ? Props.ContainsKey(name) : Preds.ContainsKey(name) || Words.Contains(name);

            if (ask)
                rule.Owners = OwnersOf(name);

            // A name only another script defines is evaluated there, and the ship
            // is the controller that script sees. Any other target must exist here too,
            // which also catches a $VARIABLE that only another script defines.
            if (rule.Blocks.Count == 0 && !(ship && rule.Owners != null && !known))
                return "no target: " + args[0];

            rule.Pending = ask && !known && rule.Owners == null;
            if (rule.Owners != null)
            {
                rule.Cond = (ship ? "ship" : "\"" + args[0] + "\"") + " " + string.Join(" ", args.GetRange(1, args.Count - 1))
                    + (at == null ? "" : " --at=" + at) + (band == null ? "" : " --band=" + band);

                if (!known)
                    return rule.Cond.IndexOf('$') >= 0 ? "unknown variable in: " + rule.Cond : null;
            }

            string rows = args.Count == 4 ? $"{args[1]} {args[2]} {args[3]}" : null;

            if (rows == null)
            {
                if (Preds.TryGetValue(name, out rows))
                    Preds.TryGetValue(name + ".band", out pband);
                else
                    rows = Words.Contains(name) ? "status == " + name : null;

                if (rows == null)
                    return "unknown predicate: " + args[1];
            }

            foreach (var row in rows.Split('|'))
            {
                var part = row.Split(' ');
                var term = new Term();
                string key;

                if (part.Length != 3)
                    return "bad condition: " + row;

                key = part[0].ToLower();

                if (!Props.TryGetValue(key, out term.Read))
                    return "unknown property: " + part[0];

                if (!Ops.TryGetValue(part[1], out term.Cmp))
                    return "unknown operator: " + part[1];

                if (!Val(part[2], out term.Val))
                    return "bad value: " + part[2];

                if (at != null)
                {
                    term.Val = atVal;
                    part[2] = at;
                }

                term.Ratio = part[2].EndsWith("%");
                term.Any = Registered.Contains(key);

                // An explicit band, else the predicate's, else 2% of a ratio or 1 unit
                string b = band ?? pband;
                double w = term.Ratio ? 0.02 : 1;

                if (!string.IsNullOrEmpty(b) && Pct(b, out w) && term.Ratio && !b.EndsWith("%"))
                    w /= 100;

                term.Back = part[1][0] == '<' ? w : part[1][0] == '>' ? -w : 0;
                rule.Terms.Add(term);
            }

            return null;
        }

        /// <summary>
        /// Tests a condition against its blocks. The first term that any block
        /// answers decides. Blocks that do not support it (NaN) are skipped.
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="tripped">True when the rule is already tripped, so its band applies.</param>
        /// <returns>1 when true, 0 when false, and -1 when no block answers any term.</returns>
        int Probe(Rule rule, bool tripped)
        {
            foreach (var term in rule.Terms)
            {
                double sum = 0, limit = tripped ? term.Val + term.Back : term.Val;
                int count = 0;

                foreach (var block in rule.Blocks)
                {
                    double v = double.NaN;

                    foreach (var read in term.Read)
                    {
                        v = read(block);

                        if (!double.IsNaN(v))
                            break;
                    }

                    if (double.IsNaN(v))
                        continue;

                    if (term.Any && term.Cmp(v, limit))
                        return 1;

                    sum += v;
                    count++;
                }

                if (count > 0)
                    return !term.Any && term.Cmp(sum / count, limit) ? 1 : 0;
            }

            return -1;
        }

        /// <summary>
        /// The ids of the scripts on the construct that define a name.
        /// </summary>
        /// <returns>The ids, or null when no script does.</returns>
        List<long> OwnersOf(string name)
        {
            var owners = new List<long>();

            foreach (var script in IMS.ConstructVocab)
                if (script.Value.Contains(name))
                    owners.Add(script.Key);

            owners.Sort();

            return owners.Count > 0 ? owners : null;
        }

        /// <summary>
        /// Rebuilds every rule from the Programmable Block's [rules] and each
        /// block's own [rules]. A rule keeps its last result, so a rebuild
        /// never fires a rule again.
        /// </summary>
        void Rebuild()
        {
            var rules = new List<Rule>();
            Fast = false;

            LoadPredicates();
            Load(rules, Configuration.Ini, null);

            foreach (var config in BlockCatalogue.BlockConfigs)
                if (config.Key.EntityId != Mother.ProgrammableBlock.EntityId)
                    Load(rules, config.Value, config.Key);

            foreach (var rule in rules)
            {
                foreach (var old in Rules)
                {
                    if (old.Name != rule.Name)
                        continue;

                    rule.Active = old.Active;
                    rule.Tripped = old.Tripped;
                    rule.Since = old.Since;
                    rule.LastFired = old.LastFired;
                    rule.Due = old.Due;
                }

                Fast |= rule.Every > 0;
            }

            Rules = rules;
        }

        /// <summary>
        /// Loads the Programmable Block's [predicates]: "name = rows" adds or
        /// replaces a predicate, and "name.band = 5%" sets its default band.
        /// </summary>
        void LoadPredicates()
        {
            var keys = new List<MyIniKey>();
            Configuration.Ini.GetKeys("predicates", keys);

            foreach (var key in keys)
                Preds[key.Name.ToLower()] = $"{Configuration.Ini.Get("predicates", key.Name)}".Replace(" | ", "|");
        }

        /// <summary>
        /// Loads the rules of one [rules] section. In a block's own section,
        /// "this" is that block.
        /// </summary>
        void Load(List<Rule> rules, MyIni ini, IMyTerminalBlock owner)
        {
            var keys = new List<MyIniKey>();
            ini.GetKeys("rules", keys);

            foreach (var key in keys)
            {
                var rule = new Rule { Name = (owner == null ? "" : owner.CustomName + " ") + key.Name };
                string text = $"{ini.Get("rules", key.Name)}".Replace("\n", " "), error;

                if (owner != null)
                    text = BlockCatalogue.ReplaceThisKeywordWithBlockName(owner, text);

                try
                {
                    error = Fill(rule, text);
                }
                catch (Exception e)
                {
                    error = e.Message;
                }

                if (error == null || rule.Pending)
                    rules.Add(rule);

                if (error != null)
                    Log($"Rule {rule.Name} disabled: {error}" + (rule.Pending && IMS.ConstructVocab.Count > 0 ? " (no script on the construct has it)" : ""));
            }
        }

        /// <summary>
        /// Fills a rule from its text: the "when" step, then the steps that run
        /// on the false to true change, and after a top level else the steps
        /// that run on the true to false change.
        /// </summary>
        /// <returns>Why the rule is invalid, or null.</returns>
        string Fill(Rule rule, string text)
        {
            var steps = new TerminalRoutine(text).Commands;
            string cond, error;

            if (steps.Count == 0 || steps[0].Name != "when")
                return "needs when";

            if (!Mother.TrySubstituteVariables(steps[0].CommandString, out cond, out error))
                return error;

            error = Parse(new TerminalCommand(cond), rule, true, true);

            if (error != null)
                return error;

            rule.Due = Now + rule.Every;

            int depth = 0;
            bool inElse = false;

            for (int i = 1; i < steps.Count; i++)
            {
                string name = steps[i].Name;

                // else only splits the rule outside of an inline if block
                if (depth == 0 && name == "else" && !inElse)
                {
                    inElse = true;
                    continue;
                }

                depth += name == "if" ? 1 : name == "end" && depth > 0 ? -1 : 0;

                if (inElse)
                    rule.Else += steps[i].CommandString + ";";
                else
                    rule.Then += steps[i].CommandString + ";";
            }

            if (steps.Count == 1)
                Log($"Rule {rule.Name} has no steps");

            return null;
        }

        /// <summary>
        /// Prints a message once.
        /// </summary>
        void Log(string message)
        {
            if (Logged.Add(message))
                Mother.Print(message, false);
        }
    }
}
