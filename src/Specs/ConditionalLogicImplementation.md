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
- [ ] Confirm option (b) above (make `ReplaceThisKeywordWithBlockName` public).
- [ ] Confirm the skip stack is scoped per routine body (see `CommandBus` note).

---

## Phase 1 - MVP

### 1.1 Condition core (`ConditionModule`)

- [ ] Create `src/Modules/Condition/ConditionModule.cs` as `ConditionModule : BaseModule` and add it to [MotherCore.projitems](../MotherCore.projitems).
- [ ] Register the module as a core module in [Mother.cs](../Mother.cs).
- [ ] Parse a condition from `TerminalCommand`: `<target> <predicate>` and `<target> <property> <op> <value>`.
- [ ] Implement a single `Compare(op, a, b)` switch for `<  >  ==  !=`.
- [ ] Value parsing: number, `%` ratio (0-1), state word (`locked`, `connected`), `$VAR` already substituted by `TerminalCommand`.
- [ ] Target resolution: quoted name/group, `"$VAR"`, `this`, via `BlockCatalogue`; bare-word system targets (`ship`).
- [ ] Aggregation over the resolved list: default `avg` for ratios, `sum` for energy/force, `any` for bool/enum.
- [ ] Public `Evaluate(TerminalCommand)` as the single entry point used by both `when` and `if`.
- [ ] Mixed-type groups: skip blocks that return `NaN`; empty or unsupported target evaluates false.

### 1.2 Property and predicate registries

- [ ] `RegisterProperty(key, selector)` public API (`Dictionary<string, Func<IMyTerminalBlock, double>>`, `NaN` for unsupported).
- [ ] `RegisterPredicate(name, key, op, threshold, band)` public API; predicates are data rows over the property registry.
- [ ] Seed ~10 Tier 1 properties using typed API reads only (no terminal properties):
  - [ ] `enabled`, `working`
  - [ ] `charge` (battery, jump drive)
  - [ ] `fill` (gas tank)
  - [ ] `status` (connector, landing gear, door)
  - [ ] `ship speed`, `ship altitude`
- [ ] Seed predicates: `on off working broken low critical empty full ready connected locked unlocked open closed`.
- [ ] `status` compares enums by name.

### 1.3 `[rules]` loader and `when` rules

- [ ] Add `rules` to `Configuration.DefaultSections`.
- [ ] Load PB `[rules]` and each block's own `[rules]` section (`this` = that block).
- [ ] Per rule: step 1 must be `when`; steps up to `else;` are the then routine; the rest the else routine.
- [ ] Rule state per rule as a small array/tuple: `Active`, `Since`, `LastFired`.
- [ ] Cache the resolved block list on the rule; never call `GetBlocksOfType` per tick.
- [ ] Register one scheduled task with a shared interval (default ~100 ticks, configurable in `[general]`).
- [ ] Edge trigger: false->true runs then routine, true->false runs else routine, via `CommandBus.RunTerminalCommand`.
- [ ] Rebuild rules on `SystemConfigChangedEvent`, `BlockConfigChangedEvent`, `ConstructRefreshedEvent`.
- [ ] Error handling: log once naming the rule and disable only that rule for invalid operator, unknown property/predicate, unresolved target, or bad option. Never throw.
- [ ] Warn once for a rule with no steps after `when`.
- [ ] A failing rule routine must not stop the evaluation loop.

### 1.4 Inline `if` / `else` / `end` in `CommandBus`

- [ ] Add an `if`/`else`/`end` skip stack to `RoutineState`.
- [ ] Handle `if`, `else`, `end` in `ExecuteCommandGroupCoroutine` so they work in `[commands]`, `[menu:...]`, rule bodies and `[hooks]` with no per-section code.
- [ ] `if` calls `ConditionModule.Evaluate`; skipped steps must not run or resolve variables.
- [ ] Support nesting to any depth; each `if` consumes one `end;`.
- [ ] Implicitly close open blocks at the end of a routine or `{}` parallel group.
- [ ] Failure cases (`state.Failed`, print reason, stop routine): unknown predicate/property/target, stray `else`/`end`, stateful option (`--band`, `--for`, `--cooldown`, `--every`) on `if`.
- [ ] Confirm `wait`, `view/notify`, `@Grid ...` and named commands work inside branches.
- [ ] Confirm `if` works inside a `when` rule's then/else branch.

### 1.5 Phase 1 tests (MotherCore.Tests)

Add [ConditionModuleTests.cs](../../tests/MotherCore.Tests/Tests/Module/) and extend [CommandBusTests.cs](../../tests/MotherCore.Tests/Tests/Module/CommandBusTests.cs).

- [ ] Loader: rule with/without `else`; predicate vs expression; `$VAR` target and value; `this`; quoted names containing `.`.
- [ ] Existing hooks (`onLock`, `"Block".hookName`) still load and fire unchanged; `[rules]` is read independently of `[hooks]`.
- [ ] Edge trigger fires once while the condition stays true; `else` fires on true->false only; re-arm after false.
- [ ] Unknown property/predicate/target disables only that rule.
- [ ] Inline `if`: true/false branch, optional `else`, nesting, implicit close, `wait` inside a branch, `if` inside a config command and a rule branch, skipped steps do not run or resolve variables, stray `else`/`end` fails.
- [ ] `if` and `when` give the same result for the same condition text.
- [ ] `[rules]` reload on Custom Data change and block removal; PB `[rules]` and block-local `[rules]` both load.

**Done when:** all Phase 1 tests and the baseline suite pass; the `Land`, `Toggle`, `shutdown` and `start` examples from the spec work end to end.

---

## Phase 2 - Stability

- [ ] Add `<=` and `>=` to `Compare`.
- [ ] `--band=n` hysteresis: separate trip/reset thresholds; predicates carry default bands; implicit 2% ratio / 1-unit band for `<`/`>`.
- [ ] `--for=s` debounce: start timer on first true, clear on false; accumulate `Runtime.TimeSinceLastRun`, not `DateTime`.
- [ ] `--cooldown=s` minimum time between firings (default: one evaluation interval).
- [ ] `--every=s` per-rule evaluation interval override.
- [ ] `--at=value` threshold override for predicates (allowed on both `when` and `if`).
- [ ] Inline `elseif`: first true branch wins, later conditions not evaluated, `else` only when nothing matched, one `end;` closes the chain.
- [ ] `elseif` failures: after `else`, without `if`; never evaluated inside a skipped parent.
- [ ] Remaining Tier 1 properties and predicates: `mode`, `stockpile`, `oxygen`, `position`, `angle`, `locked`, `attached`, `detected`, `occupied`, `dampeners`, `handbrake`, `effectiveness`, `ship gravity`, `ship moving`; add predicates `stockpiling connectable disconnected pressurized depressurized extended retracted extending retracting attached detached detected clear occupied weak moving stopped inspace ingravity`.

### Tests

- [ ] Flapping input around a threshold with `--band` and `--for` fires at most once.
- [ ] Cooldown honored; `--every` overrides the shared interval; `--at` overrides a predicate threshold.
- [ ] `--band`/`--for`/`--cooldown`/`--every` on `if` fail the step.
- [ ] All `elseif` cases listed above.
- [ ] Generators on at 20%, off at 80% with `--band=60` pair behaves correctly.

**Done when:** Phase 2 tests pass; no regression in Phase 1 tests.

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
- [ ] Extension registration API finalised and documented for `RegisterProperty` / `RegisterPredicate`.
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
