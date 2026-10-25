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
        /// Seconds on the module clock, accumulated from Runtime.TimeSinceLastRun.
        /// Next is when the shared pass is due, and Fast is true while any rule
        /// has its own --every and so needs checking between shared passes.
        /// </summary>
        double Now, Next, Interval;
        bool Fast;

        BlockCatalogue BlockCatalogue;
        CommandBus CommandBus;
        Configuration Configuration;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public ConditionModule(Mother mother) : base(mother) { }

        /// <summary>
        /// Boot the module. Listens for config and construct changes, and
        /// schedules the rule pass every [general] ruleInterval seconds (default 1.6).
        /// </summary>
        public override void Boot()
        {
            BlockCatalogue = Mother.GetModule<BlockCatalogue>();
            CommandBus = Mother.GetModule<CommandBus>();
            Configuration = Mother.GetModule<Configuration>();

            Rules = new List<Rule>();
            Logged.Clear();
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

            if (!double.TryParse(Configuration.Get("general.ruleInterval"), out Interval) || Interval < 0)
                Interval = 0.5;

            Next = Interval;
            Mother.GetModule<Clock>().Schedule(Tick);
        }

        /// <summary>
        /// Advances the module clock every cycle, and runs the shared pass when
        /// it is due. While a rule has its own --every, each cycle also checks
        /// those rules.
        /// </summary>
        void Tick()
        {
            Now += Mother.Runtime.TimeSinceLastRun.TotalSeconds;
            bool shared = Now >= Next;

            if (shared)
                Next = Now + Interval;

            if (shared || Fast)
                EvaluateRules(shared);
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
        /// Evaluates a condition step once. This is the single entry point for
        /// both "if" and "when".
        /// </summary>
        /// <param name="command">ie. "Batteries" low, ship speed > 30.</param>
        /// <param name="error">Why the condition is invalid, or null.</param>
        /// <returns>The result. False when there is an error.</returns>
        public bool Evaluate(TerminalCommand command, out string error)
        {
            var rule = new Rule();
            error = Parse(command, rule, false);

            return error == null && Test(rule);
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
                try
                {
                    if (rule.Every > 0 ? Now < rule.Due : !shared)
                        continue;

                    rule.Due = Now + rule.Every;

                    bool trip = Test(rule, rule.Tripped);

                    if (trip != rule.Tripped)
                    {
                        rule.Tripped = trip;
                        rule.Since = Now;
                    }

                    bool now = trip && Now - rule.Since >= rule.For;

                    if (now == rule.Active || Now - rule.LastFired < rule.Cooldown)
                        continue;

                    rule.Active = now;
                    rule.LastFired = Now;
                    CommandBus.RunTerminalCommand(now ? rule.Then : rule.Else);
                }
                catch (Exception e)
                {
                    Log($"Rule {rule.Name}: {e.Message}");
                }
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
        /// <returns>Why the condition is invalid, or null.</returns>
        string Parse(TerminalCommand command, Rule rule, bool stateful)
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

            if (rule.Blocks.Count == 0)
                return "no target: " + args[0];

            string rows = args.Count == 4 ? $"{args[1]} {args[2]} {args[3]}" : null;

            if (rows == null)
            {
                string name = args[1].ToLower();

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
        /// answers decides. Blocks that do not support it (NaN) are skipped,
        /// and a condition no block can answer is false.
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="tripped">True when the rule is already tripped, so its band applies.</param>
        bool Test(Rule rule, bool tripped = false)
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
                        return true;

                    sum += v;
                    count++;
                }

                if (count > 0)
                    return !term.Any && term.Cmp(sum / count, limit);
            }

            return false;
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

                if (error == null)
                    rules.Add(rule);
                else
                    Log($"Rule {rule.Name} disabled: {error}");
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

            error = Parse(new TerminalCommand(cond), rule, true);

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
