# Conditional Logic - Implementation Checklist

Source spec: [ConditionalLogicSpec.md](./ConditionalLogicSpec.md). This file is the execution checklist; design decisions live in the spec.

Constraints for every phase: C# 6, low character count, no LINQ on polled paths, no per-rule classes, `BlockCatalogue` hook loading untouched.

Each phase ends with a **Done when** gate. Do not start the next phase until it passes.

---

## Phase 0 - Groundwork

- [x] Read `CommandBus.ExecuteCommandGroupCoroutine`, `ExecuteCommandCoroutine`, `RunBody` and `RoutineState` ([CommandBus.cs](../Modules/CommandBus/CommandBus.cs)); note where a skip stack and `if`/`else`/`end` dispatch can hook in.
- [x] Read `BaseModule` state tracking (`_stateSelectors`, `_stateHandlers`, `PreviousStates`) ([BaseModule.cs](../Modules/BaseModule.cs)) and decide whether rules reuse it directly or just mirror its pattern.
- [x] Read `Configuration.DefaultSections` ([Configuration.cs](../Modules/Configuration/Configuration.cs)) and the `SystemConfigChangedEvent` / `BlockConfigChangedEvent` / `ConstructRefreshedEvent` subscription pattern used by another module.
- [x] Capture a baseline: run the existing test suite and record pass count. **490 passed, 0 failed** (net48, `dotnet test` in `tests\MotherCore.Tests`).
- [ ] Capture a baseline instruction count (`Runtime.CurrentInstructionCount`) on a ~100-battery grid for later comparison. *Needs the game; cannot be done from the test harness. Do before Phase 3 profiling.*
- [ ] Capture a baseline minified script size (build MotherOS/extension with the optimizer) so Phase 1 growth can be measured against the 100,000 character limit.

**Done when:** baseline tests green, integration points identified.

### Phase 0 findings

**`CommandBus` (if/else/end)**
- `ExecuteCommandGroupCoroutine` is a plain `foreach` over steps that calls `ExecuteCommandCoroutine` and stops on `state.Failed`. The `if`/`else`/`end` dispatch belongs in `ExecuteCommandCoroutine`, before `ResolveStep` runs, so skipped steps do not resolve variables. The skip check itself can sit in the group loop, before the step runs.
- `RoutineState` is shared by every step of one coroutine, including inlined config-command bodies (`RunBody` passes the same `state`). A skip stack on `RoutineState` therefore leaks into called commands: the stack must be scoped per routine body, so an open `if` in a called command is closed when that body ends (matches "closed implicitly at end of routine"). Parallel groups already get a fresh `RoutineState`, which gives the `{}` implicit close for free.
- `if` must evaluate after variable resolution of its own line (so `$VAR` in the condition works) but a skipped `if` must not evaluate its condition. Skipped nested `if`s only push a "skipped" frame.

**`BaseModule` state tracking**
- `_stateSelectors`/`_stateHandlers` are keyed per block and driven by `BlockCatalogue.BlocksToMonitor`. They are block-scoped, not rule-scoped, so they do not fit a rule over a block group with aggregation. **Decision: mirror the pattern, do not reuse it.** `ConditionModule` keeps its own per-rule `Active`/`Since`/`LastFired` state and runs from a `Clock.Schedule` task.

**Config and events**
- `Configuration.DefaultSections` is a private `string[]`; add `"rules"` there. Note it only creates empty sections when missing, it does not touch existing data.
- Module pattern for reload: `Subscribe<T>()` in `Boot()`, then branch on `e is T` in `HandleEvent` (see `MergeBlockModule`, `MechanicalBlockModule`, `DisplayModule`).
- `BlockConfigChangedEvent` and `SystemConfigChangedEvent` are emitted from `BlockCatalogue.LoadBlockConfigurations` (the latter only for the programmable block). `ConstructRefreshedEvent` is emitted from `RefreshConstruct`.
- Core modules are registered in `Mother.RegisterCoreModules` in boot order. `ConditionModule` must come **after** `BlockCatalogue` (needs resolution) and `CommandBus`, e.g. after `ActivityMonitor` / next to the other block modules.

**`this` keyword and loader reuse**
- `BlockCatalogue.ReplaceThisKeywordWithBlockName` is **private** and the hook loader is not to be touched. `ConditionModule` needs its own `this` replacement for block-local `[rules]`. Options: (a) a small copy in `ConditionModule`, (b) make the existing method `public` (a visibility-only change). Prefer (b) to save characters; it does not alter hook behavior.
- Block configs are available via `BlockCatalogue.GetBlockConfiguration(block)` (public), so the rule loader can read each block's `[rules]` section without parsing `CustomData` itself.
- The PB config is `Configuration.Ini`; read `[rules]` from it directly.

**Other notes**
- Target resolution: `BlockCatalogue` exposes `GetBlocks<T>(filter)` and `GetBlocksByName<T>(name)` (name or group). `$VAR` is already substituted before a step reaches `TerminalCommand`, but rule bodies are loaded from config, so `ConditionModule` must substitute `$VAR` in the `when` line at evaluation/rebuild time.
- `TerminalRoutine` parses `;`-separated steps and `{}` groups; `else;` and `end;` become ordinary `TerminalCommand`s named `else`/`end` with no arguments, so no parser change is required.
- `Clock.Schedule(Action, interval)` is the scheduler entry point for the polled rule task.

**Follow-up before Phase 1 (decisions needed)**
- [x] Confirm option (b) above (make `ReplaceThisKeywordWithBlockName` public). Done in Phase 1.
- [x] Confirm the skip stack is scoped per routine body (see `CommandBus` note). Done in Phase 1: `ExecuteCommandGroupCoroutine` records the open-block count on entry, cannot close blocks below it, and removes its own on exit.

---

## Phase 1 - MVP

### 1.1 Condition core (`ConditionModule`)

- [x] Create `src/Modules/Condition/ConditionModule.cs` as `ConditionModule : BaseCoreModule` and add it to [MotherCore.projitems](../MotherCore.projitems).
- [x] Register the module as a core module in [Mother.cs](../Mother.cs) (after `ActivityMonitor`).
- [x] Parse a condition from `TerminalCommand`: `<target> <predicate>` and `<target> <property> <op> <value>`.
- [x] Implement a single `Compare(op, a, b)` switch for `<  >  ==  !=`.
- [x] Value parsing: number, `%` ratio (0-1), state word (`locked`, `connected`). `$VAR` is substituted by the `CommandBus` for `if`, and by the rule loader for `when`.
- [x] Target resolution: quoted name/group, `"$VAR"`, `this`, via `BlockCatalogue`; bare-word system targets (`ship`).
- [x] Aggregation over the resolved list: default `avg` for ratios, `any` for bool/enum. (`sum` for energy/force has no Phase 1 property; it arrives with Phase 3 suffixes.)
- [x] Public `Evaluate(TerminalCommand, out string error)` as the single entry point used by both `when` and `if`.
- [x] Mixed-type groups: skip blocks that return `NaN`; unsupported target evaluates false. An unresolved target is an error (disables the rule, fails the `if`).

### 1.2 Property and predicate registries

- [x] `RegisterProperty(key, selector, any = false)` public API (`Dictionary<string, Func<IMyTerminalBlock, double>>`, `NaN` for unsupported). `any` picks the group aggregate.
- [x] `RegisterPredicate(name, rows)` public API; a predicate is a string of `property operator value` rows joined by `|`, parsed by the same path as expressions (`"charge < 20%|fill < 15%"`). The first row some block in the target answers decides. Any state word (`locked`, `open`, ...) is also a predicate for free (`status == word`). The per-predicate `band` arrives with Phase 2 `--band`.
- [x] Seed ~10 Tier 1 properties using typed API reads only (no terminal properties):
  - [x] `enabled`, `working`
  - [x] `charge` (battery, jump drive)
  - [x] `fill` (gas tank)
  - [x] `status` (connector, landing gear, door)
  - [x] `ship speed`, `ship altitude` (registered as `speed` and `altitude` on `IMyShipController`, so `"$COCKPIT" speed` works too)
- [x] Seed predicates: `on off working broken low critical empty full ready connected locked unlocked open closed`.
- [x] `status` compares enums by name.

### 1.3 `[rules]` loader and `when` rules

- [x] Add `rules` to `Configuration.DefaultSections`.
- [x] Load PB `[rules]` and each block's own `[rules]` section (`this` = that block).
- [x] Per rule: step 1 must be `when`; steps up to a top-level `else;` are the then routine; the rest the else routine. An `else;` inside an inline `if` does not split the rule.
- [x] Rule state per rule: `Active`. `Since` and `LastFired` arrive with `--for` / `--cooldown` in Phase 2.
- [x] Cache the resolved block list on the rule; never call `GetBlocksOfType` per tick.
- [x] Register one scheduled task with a shared interval (default 0.5 s, `[general] ruleInterval` in seconds, read at boot).
- [x] Edge trigger: false->true runs then routine, true->false runs else routine, via `CommandBus.RunTerminalCommand`. A rule starts false, so a rule that is true at boot runs its then routine on the first pass. A rebuild keeps each rule's last result, so it never fires a rule again.
- [x] Rebuild rules on `SystemBootedEvent`, `SystemConfigChangedEvent`, `BlockConfigChangedEvent`, `ConstructRefreshedEvent` (only while WORKING).
- [x] Error handling: log once naming the rule and disable only that rule for invalid operator, unknown property/predicate, unresolved target, or bad option. Never throw.
- [x] Warn once for a rule with no steps after `when`.
- [x] A failing rule routine must not stop the evaluation loop.

### 1.4 Inline `if` / `else` / `end` in `CommandBus`

- [x] Add an `if`/`else`/`end` skip stack to `RoutineState` (`Ifs`, a list of `IfFrame { Run, Taken, InElse }`).
- [x] Handle `if`, `else`, `end` in `ExecuteCommandGroupCoroutine` so they work in `[commands]`, `[menu:...]`, rule bodies and `[hooks]` with no per-section code.
- [x] `if` calls `ConditionModule.Evaluate`; skipped steps must not run or resolve variables.
- [x] Support nesting to any depth; each `if` consumes one `end;`.
- [x] Implicitly close open blocks at the end of a routine or `{}` parallel group.
- [x] Failure cases (`state.Failed`, print reason, stop routine): unknown predicate/property/target, stray `else`/`end`, `else` after `else`, any option (including `--band`, `--for`, `--cooldown`, `--every`) on `if` or `when` ("bad syntax"). Phase 2 makes the stateful options valid on `when` and keeps them an error on `if`, with a clearer message.
- [x] Confirm `wait` and named commands work inside branches. `view/notify` is not a Core command, so the tests use a recording command. `@Grid ...` is only valid as the first term of a routine, so it does not appear inside a branch.
- [x] Confirm `if` works inside a `when` rule's then/else branch.

### 1.5 Phase 1 tests (MotherCore.Tests)

[ConditionModuleTests.cs](../../tests/MotherCore.Tests/Tests/Module/ConditionModuleTests.cs) covers rules, and [CommandBusIfTests.cs](../../tests/MotherCore.Tests/Tests/Module/CommandBusIfTests.cs) covers inline `if` (a separate file, like `CommandBusVariableTests.cs`).

- [x] Loader: rule with/without `else`; predicate vs expression; `$VAR` target and value; `this`; quoted names containing `.`.
- [x] Existing hooks (`onLock`, `"Block".hookName`) still load and fire unchanged; `[rules]` is read independently of `[hooks]`.
- [x] Edge trigger fires once while the condition stays true; `else` fires on true->false only; re-arm after false.
- [x] Unknown property/predicate/target disables only that rule.
- [x] Inline `if`: true/false branch, optional `else`, nesting, implicit close, `wait` inside a branch, `if` inside a config command and a rule branch, skipped steps do not run or resolve variables, stray `else`/`end` fails.
- [x] `if` and `when` give the same result for the same condition text.
- [x] `[rules]` reload on Custom Data change and block removal; PB `[rules]` and block-local `[rules]` both load.

**Done when:** all Phase 1 tests and the baseline suite pass; the `Land`, `Toggle`, `shutdown` and `start` examples from the spec work end to end. **Met:** 547 passed (490 baseline + 57 new).

---

## Phase 2 - Stability

- [x] Add `<=` and `>=` to `Compare`.
- [x] `--band=n` hysteresis: separate trip/reset thresholds; predicates carry default bands (`low` 5%, `critical` 3%, `RegisterPredicate(name, rows, band)`); implicit 2% ratio / 1-unit band for `<`/`>`/`<=`/`>=`. A bare `--band=60` on a ratio threshold means 60%. `--band=0` turns it off. `==`/`!=` have no band.
- [x] `--for=s` debounce: start timer on first trip, clear on drop; accumulate `Runtime.TimeSinceLastRun` on a module clock (a task scheduled every cycle), not `DateTime`. It delays the then routine only; the else routine runs as soon as the band releases.
- [x] `--cooldown=s` minimum time between firings. The default is no extra hold: a rule is evaluated once per pass, so it already fires at most once per evaluation interval.
- [x] `--every=s` per-rule evaluation interval override. A rule with `--every` is checked each cycle against its own due time; other rules still run on the shared pass.
- [x] `--at=value` threshold override for predicates (allowed on both `when` and `if`; an error on an expression).
- [x] Inline `elseif`: first true branch wins, later conditions not evaluated, `else` only when nothing matched, one `end;` closes the chain.
- [x] `elseif` failures: after `else`, without `if`; never evaluated inside a skipped parent.
- [x] `--band`/`--for`/`--cooldown`/`--every` on `if` fail the step with "--x only works on a when rule"; an unknown option is "bad syntax"; a bad value is "bad option: --x".
- [x] Remaining Tier 1 properties and predicates: `mode`, `stockpile`, `oxygen`, `position`, `angle`, `locked`, `attached`, `detected`, `occupied`, `dampeners`, `handbrake`, `effectiveness`, `gravity`, `moving`; `status` for jump drives, air vents and pistons; predicates `stockpiling disconnected pressurized depressurized extended retracted extending retracting attached detached detected clear occupied weak moving stopped inspace ingravity` (`connectable`, `pressurized` and the other enum words work as `status == word`). `on`/`off` stay on `enabled`, so use `dampeners == 1` and `handbrake == 1` for the controller toggles. `stopped` means the ship, so a piston uses `status == stopped`.

### Tests

- [x] Flapping input around a threshold with `--band` and `--for` fires at most once.
- [x] Cooldown honored; `--every` overrides the shared interval; `--at` overrides a predicate threshold.
- [x] `--band`/`--for`/`--cooldown`/`--every` on `if` fail the step.
- [x] All `elseif` cases listed above.
- [x] Generators on at 20%, off at 80% with `--band=60` pair behaves correctly.

**Done when:** Phase 2 tests pass; no regression in Phase 1 tests. **Met:** 588 passed (547 + 41 new). Two Phase 1 expectations changed on purpose: `--for`/`--band`/`--cooldown`/`--every` on `if` now give the clearer message, and `--for` on `when` is valid.

---

## Phase 2.5 - Vocabulary ownership

Keeps Core light: Core owns the engine, and the scripts that own the blocks (Mother OS, MAPS) own the vocabulary. Players tune it in Custom Data.

- [x] `ConditionModule` registers only `enabled`, `working` and the predicates `on off working broken`. Properties, predicates, bands and state words are all registration, and the vocabulary is cleared on each boot so a reboot never doubles it.
- [x] Properties are a list of selectors per key, one per block type (`RegisterProperty<T>`). The first that answers for a block wins, so each module registers the block type it owns (`charge` for batteries and jump drives, `status` for doors, gear, vents...). Registering a key for a block type again is a no-op, so modules that share a base class can register in the base.
- [x] `RegisterPredicate` adds rows to an existing predicate (de-duplicated) instead of replacing it, and only sets the band when one is given. `low` is a battery, tank and air vent row.
- [x] State words are registered with the status that uses them (`RegisterWords`, `Word`), replacing the hard coded list.
- [x] `BlockSet<T>` helpers on `BaseModule.Block<T>()`: `.Number` (average), `.Flag` (any), `.Status` (enum words, any), and `RegisterPredicate` on `BaseExtensionModule`.
- [x] `[predicates]` in the Programmable Block's Custom Data adds or replaces a predicate (`name = rows`) and its default band (`name.band`). The entries are written over the registered predicates, so a removed entry is restored on the next boot.
- [x] A malformed predicate row reports `bad condition: row` instead of throwing.
- [x] Mother OS registers its vocabulary from the module that owns each block (battery, tank, landing gear, door, air vent, piston, mechanical block, motor stator, sensor, cockpit, thruster), and gains `ConnectorExtensionModule` and `JumpDriveModule`. MAPS registers `speed`, `moving`, `gravity`, `altitude` from `FlightControlModule`.

### Tests

- [x] Core: the engine tests run against `FakeConditionVocabulary`, a stand-in extension module registered with `ScriptBuilder.WithModules`.
- [x] Core: modules add rows to one predicate, repeated registrations are ignored, `[predicates]` add, replace and restore, and a reboot does not double the vocabulary.
- [x] Mother OS: `ConditionVocabularyTests` covers the vocabulary of each module (moved from Core).
- [x] MAPS: `FlightControlConditionTests` covers speed, altitude, moving and gravity (moved from Core).

**Done when:** Core, Mother OS and MAPS suites pass. **Met:** Core 582, Mother OS 384, MAPS 42.

Follow-ups: Core still has `ConnectorModule` (commands and `onLock` hooks), which Mother OS could own if MAPS docking does not need it. `Tier 2` properties (Phase 4) should be registered by the owning script from the start.

---

## Phase 3 - Scale

- [ ] Rule budget: evaluate round-robin from a saved cursor; stop for the tick when `Runtime.CurrentInstructionCount` passes a fraction of `Runtime.MaxInstructionCount` (default ~10%, `ruleBudget=10` in `[general]`).
- [ ] Ensure every rule is evaluated once per cycle and never twice in one cycle.
- [ ] Stagger rules across ticks to flatten instruction spikes.
- [ ] Per-pass read cache keyed by `target|property`, so rules sharing a block set and property read once.
- [ ] Aggregate suffixes `.min`, `.max`, `.sum`, `.all`, `.none` (e.g. `status.all == locked`).
- [ ] Boot-time lint (warn only): rule resolves > 100 blocks; total rules > 50.
- [ ] Profile on a ~100-battery grid with `Runtime.CurrentInstructionCount` before/after; tune budget, interval and lint defaults.

### Tests

- [ ] With a tiny budget, all rules still evaluate over successive ticks (cursor resumes).
- [ ] Group aggregates (avg/min/max/sum) fire once; mixed-type groups skip unsupported blocks.
- [ ] Shared read happens once per pass for rules sharing a target and property.

**Done when:** Phase 3 tests pass and profiling shows rule evaluation stays within the budget on the 100-battery grid.

---

## Phase 4 - Extras

- [ ] `and`/`or` composition in `when` (several clauses, quoted targets only).
- [ ] Variable thresholds (`--at=$MIN_POWER`, `$VAR` in values).
- [x] Extension registration API finalised and documented for `RegisterProperty` / `RegisterPredicate` (see Phase 2.5).
- [ ] Tier 2 properties: `count`, `storage <key>`, `var <name>`, `stored`/`output`/`input`, `capacity`, merge block `status`, `open`, rotor/piston limits and speeds, `timer` `counting`.
- [ ] Autopilot extension registers `fcs`, `plan`, `dock` targets (in the Autopilot script, not Core).
- [ ] Rule management commands: `rule/on name`, `rule/off name`, `rule/list`.
- [ ] Opt-in `prop:Name` terminal-property fallback for blocks without a typed API.

### Tests

- [ ] `and`/`or` evaluation and short-circuiting.
- [ ] Extension-registered properties/predicates resolve in both `when` and `if`.
- [ ] `rule/on`, `rule/off`, `rule/list` behavior.

**Done when:** Phase 4 tests pass.

---

## Cross-cutting: size, docs, release

Run after each phase.

- [ ] Check script character count after the ScriptOptimizer build ([build/](../../build)); stay under the limit.
- [ ] Review size-reduction tips: one parse path, one `Compare`, collection-initializer registries, no LINQ in hot paths.
- [ ] Update [src/README.md](../README.md) with `[rules]`, `when`, `if`/`else`/`end`, predicates and options.
- [ ] Add a [CHANGELOG.md](../CHANGELOG.md) entry per shipped phase.
- [ ] Cross-reference `[hooks]` event forms and `when` polled forms in the docs (e.g. `"BLD - Connector".onLock` next to `when ... locked`).
- [ ] Run the full test suite.
