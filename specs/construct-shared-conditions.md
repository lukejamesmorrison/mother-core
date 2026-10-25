# Construct-shared condition vocabulary

Status: **implemented** (phases 0 to 6). `when` rules and inline `if` / `elseif` use names defined by other scripts on the construct. Builds on the conditional logic engine (`src/Specs/ConditionalLogicSpec.md`, `ConditionModule`) and the construct sync used for commands and views (`IntergridMessageService`).

Last updated: 2026-10-25 (-04:00). The MotherCore suite has 700 tests, Mother OS 391, MAPS 46 and Mother GUI 48, all passing.

## Problem

Since the vocabulary moved out of Core (see `src/CHANGELOG.md`), each script registers only the properties and predicates of the blocks it owns (Mother OS: `charge`, `fill`, `locked`, ...; MAPS: `speed`, `altitude`, `gravity`, `moving`). A rule that names something the local script did not register is disabled at boot (`Rule <name> disabled: unknown property: altitude`).

This breaks scripts that use the vocabulary but own none of it. The Mother GUI blueprint has rules and inline `if`s on `ship altitude`, `ship speed`, `charge`, `detected`, `inspace`, `low`, `locked`, `connected`, and the GUI script registers none of them. The `approach` rule (`when ship altitude < 100`) is one of those that stopped working.

Putting the vocabulary into every script costs characters (100,000 limit) and defines the same thing in several places.

## Goal

A property, predicate or state word is **defined in one script** (the one that owns the blocks). Any Mother Core script on the construct can use it in a condition, with no registration of its own. This applies to every place a condition is used: `when` rules and inline `if` / `elseif` steps.

A **remote name** is a property, predicate or state word that the local script does not define but another script on the construct does. For example `altitude` is a remote name for Mother GUI, because only MAPS registers it.
## Why not share the definitions

A predicate is only rows of text (`low` = `charge < 20%`), but the properties behind it are C# delegates that read a block. Delegates cannot be sent over IGC, so a script that receives the rows still cannot evaluate them. Sharing the **names** and evaluating on the **owner** works.

## Current behaviour (read in code)

| Point | Location | Behaviour |
|---|---|---|
| Vocabulary | `ConditionModule` (`Props`, `Preds`, `Words`) | Local only. Cleared on every `Boot()`. Filled by each module's `Boot()` through `RegisterProperty`, `RegisterPredicate`, `RegisterStatus`, `RegisterFlag`. |
| Unknown name | `ConditionModule.Parse` | Returns `unknown property` / `unknown predicate`. `Load` logs it once and drops the rule. |
| Sync | `IntergridMessageService.ConstructPing`, `HandleSyncRequest`, `SyncConstructCommands` | Every 5 s each script broadcasts a `sync` request carrying `Commands` and `Views`. The reply carries the same. The receiver stores views per origin in `ConstructViews`, and commands via `CommandBus.RegisterRemoteCommands`. |
| Routing | `IntergridMessageService.SendUnicastRequest(id, request, onResponse)` | Unicast with a response callback. Used today with `null` for views. |
| Evaluate | `ConditionModule.Evaluate(TerminalCommand, out error)` | Public, already the single entry for `if` and `when`. |

## Design

### 1. Advertise names

Add two keys to the `sync` request and response bodies, next to `Commands` and `Views`:

- `Props`: the lower case property keys this script registered.
- `Preds`: the predicate names, plus the state words (`locked`, `connected`, ...).

The receiver stores them per origin in `ConstructVocab` (`Dictionary<long, HashSet<string>>`, one set per script). It is cleared on boot, like `ConstructViews`. `ConditionModule` exposes `Names()` for the sender to read, so `IntergridMessageService` does not touch its internals.

### 2. Resolve to a remote owner

In `Parse`, a name that is unknown locally is looked up in `ConstructVocab`. If a script owns it, the term is marked remote and no error is returned. If no script owns it, the rule stays pending (see 5) with the log line `unknown property: x (no script on the construct has it)`.

A name registered locally is tried locally first. It is only sent out when no local block can answer (see 2a).

#### 2a. Predicates are scoped by block type, and the block's own module registers them

A predicate is a name with rows, and each row reads a property that is registered **for a block type** (`RegisterProperty<T>`). A reader returns `NaN` for a block of any other type, and `Test` skips `NaN` and lets the first row that some block answers decide. So `low` is already scoped by block type: `charge < 20%` only ever applies to batteries, `fill < 15%` only to tanks, and the same name can mean different things for different blocks. The owner of each block type already contributes its rows, for example `BatteryModule` adds `charge < 20%` and `GasTankModule` adds `fill < 15%` to `low`.

That stays as it is: **each block module registers the predicates for its own block type, in the script that owns that module.** Nothing new is registered by the sharing feature.

The one gap is a predicate whose rows are spread over scripts, for example batteries in one script and tanks in another. A script that has only the tank row would see no block that answers `low` for a battery group. The rule is:

1. Evaluate locally. If any block answers a row, that answer decides, as today.
2. If **no** local block answers any row (the condition is *unanswered*), ask the owners that advertise the name.
3. The owner replies `1`, `0` or `?` (no block of mine answers it). Owners are asked in the same batch (see 3a) and the answer of the lowest script id that did not reply `?` is used, so the result is the same on every run.
4. If every owner replies `?`, the condition is false.

This also covers a property that is unknown locally: it is unanswered by definition, so it goes straight to the owners.

### 3. Ask the owner

A new `cond` route on `IntergridMessageService`:

- Request body: a list of conditions. Each has `Condition` (the condition text after `$VAR` substitution, without the timer options `--for`, `--cooldown`, `--every`) and `Tripped` (`1` or `0`, always `0` for `if`). `--band` and `--at` stay in the text.
- The owner runs `ConditionModule.Evaluate` for each and replies `OK` with one result per condition: `1`, `0`, `?` (no block answers it, see 2a) or `!reason` (the condition is invalid there).
- The sender sends it with `SendUnicastRequest(ownerId, request, onResponse)` on the construct channel.

Block names resolve on the owner through its own `BlockCatalogue`, so the names in a rule must exist on the construct (they do, it is one grid). `ship` is the owner's own controller.

#### 3a. Batching

One request carries every condition that is due for one owner, so the cost grows with the number of owners, not the number of rules.

- **Outbox.** `ConditionModule` keeps an outbox per owner id. Remote `when` rules and remote `if` steps add their condition to it instead of sending at once.
- **Flush.** The outbox is flushed once per `Tick`, after the rule pass. A `when` pass with 8 remote rules for MAPS sends 1 request. An `if` that is waiting adds at most one cycle of latency, and `ifs` from several routines in the same cycle share a request.
- **Wire format.** Indexed keys, so no separator can clash with quotes or `|` in a condition: `C0`, `T0`, `C1`, `T1`, ... in the request, and `R0`, `R1`, ... in the response, matched by index. Identical `(Condition, Tripped)` pairs in one batch are sent once and share the result.
- **Size.** At most 10 conditions per request. More are split into further requests to the same owner in the same flush.
- **Delivery.** A `when` rule applies the results as they arrive. An `if` waiting on a result resumes when its index arrives, or fails at the timeout (see 6). Late replies after a timeout are dropped.

### 4. Keep state on the rule's holder

The script that holds the rule keeps everything stateful: `Tripped`, `Since`, `--for`, `--cooldown`, `--every`, `Active`, and runs `Then`/`Else` itself. Only the boolean per pass travels. Hysteresis stays correct because `Tripped` is sent with each request, so the owner applies the band the same way a local rule would.

`Test(rule)` for a remote `when` rule returns the **last answer received**, and sends a new request when the rule is due. A rule never blocks waiting for a reply. The rule is evaluated again as soon as a reply arrives (without asking again), so it acts on an answer one round trip after the pass that asked, not a pass later. `--for` counts from then. Inline `if` works differently, see 6.

### 5. Late or missing owners

`ConstructPing` runs every 5 s, so names arrive after `SystemBootedEvent`, when rules are first built. `ConditionModule` therefore:

- keeps a rule with an unresolved name in a `Pending` state instead of dropping it,
- rebuilds when `ConstructVocab` changes (new event, for example `ConstructVocabChangedEvent`, raised from `SyncConstructCommands` when the stored set differs),
- treats a remote answer older than 3 ping intervals, or an owner that dropped off the construct, as `false`, and logs it once.

### 6. Inline `if` and `elseif`

An `if` step needs its answer before the next step runs, so it cannot use the last answer received like a `when` rule: that could be stale, or missing on the first run. Instead the step **waits for a fresh reply**.

`ExecuteCommandGroupCoroutine` is already an iterator (`IEnumerable<double>`) and yields waits, so it can pause the routine without blocking the script:

1. `ConditionModule.Evaluate` gains a non-blocking form. For a condition with remote terms it sends the `cond` request and returns `Pending` with a request id. A fully local condition returns its result immediately, as today, so local `if` costs nothing extra.
2. The group coroutine does `yield return 0` each cycle while the result is `Pending`, and then reads the reply. Other routines and modules keep running during the wait.
3. If no reply arrives within the timeout (default 2 s, `[general] condTimeout`), the step fails with `Command failed: <step> (no answer from <script> for <name>)`, in the same format as other failed steps. The same happens at once when no script owns the name, or its owner left the construct.

Mixed conditions (`if "$BATTERIES" low` with some rows local and some remote, see 2a) send one request and wait once.

`RunIfStep` currently returns a string and cannot yield, so it becomes an iterator step or is split into "start evaluating" and "apply the result". The skip rules do not change: a skipped `if` or an `elseif` after a branch already ran is never evaluated, so it never sends a request.

## Construct only

Condition sharing works only between scripts on **this construct**, never with other grids or their programmable blocks.

- Vocabulary names travel only in the `sync` ping, which already goes out with `TransmissionDistance.CurrentConstruct` on `CONSTRUCT_CHANNEL_TAG`. `ConstructVocab` is filled only from those messages.
- `cond` requests are only sent by unicast to a script id that is in `ConstructVocab`.
- The `cond` route rejects a request unless it arrived on `CONSTRUCT_CHANNEL_TAG` (`request.Channels` contains it and nothing else) and `OriginIsOnConstruct(OriginId)` is true. Anything else gets no reply and is not evaluated.
- The route is read only: it evaluates a condition and never runs a command or changes a block.

## Phases

The phases and tasks are in [Implementation plan](#implementation-plan) at the end of this spec. In short: advertise names, resolve remote terms, `cond` route with batching, remote `when`, remote `if` / `elseif`, then docs and verification. The Mother GUI blueprint needs no `.ms` changes, its rules and ifs start working once MAPS and Mother OS are on the construct.

## Tests (MotherCore.Tests harness)

- A rule with a name only a second script registers resolves to that script and fires `Then` when the owner answers `1`.
- A local name is never sent out.
- A rule with no owner stays pending, then becomes active when the owner's sync arrives.
- `Tripped` is sent and the band applies on the owner (a value between the limit and the band keeps the rule true).
- The owner leaving the construct makes the rule false, with one log line.
- A remote name in an inline `if` pauses the routine, then runs the true or false branch from the owner's answer. Steps after the `if` do not run before the reply.
- `elseif` with a remote name is not evaluated (no request sent) when an earlier branch already ran.
- A skipped `if` never sends a request.
- No reply within `condTimeout` fails the `if` step with the documented message, and the routine stops like any failed step.
- An `if` on a name no script owns fails at once, without waiting.
- A fully local `if` returns in the same step with no request.
- Batching: 8 remote `when` rules for one owner send 1 request, not 8. Rules for two owners send 2. Identical conditions in one batch are sent once. More than 10 conditions split into several requests.
- Batching: two routines with a remote `if` in the same cycle share one request, and each resumes with its own result.
- Split predicate: a battery group with `low` defined only for tanks locally is unanswered locally, is sent to the owner, and uses its `1` or `0`.
- Split predicate: two owners, the lower script id replies `?` and the higher replies `1`, the result is `1`. Both reply `?`, the result is `false`.
- A local answer is never overridden: if any local block answers a row, no request is sent.
- Construct only: a `cond` request that arrives on any other channel, or from an origin not on the construct, gets no reply and is not evaluated.
- `cond` never runs a command or changes a block.

## Decisions

- **Sync size.** `Props` and `Preds` add a short list of names to each sync message, well within the IGC limit. No separate measurement is needed.
- **Predicate scope.** Predicates are already scoped by block type through the typed property readers, and each block's own module registers its rows. Sharing does not change that (see 2a).
- **Batching.** In scope for the first release (see 3a).
- **Security.** Construct only, read only (see Construct only).

## Implementation plan

Constraints for every phase: C# 6, low character count, no LINQ on polled paths, no per-rule or per-message classes, and the existing `sync`, `command`, `localcmd` and `view` routes unchanged. A script with no other script on the construct must behave exactly as it does today.

Each phase ends with a **Done when** gate. Do not start the next phase until it passes. Tests use the multi-script `World` harness (`world.CreateScript().OnNetwork().Boot()`, `FakeIgcNetwork`, `ClockDriver`) in `tests/MotherCore.Tests`, as `IntergridMessageServiceTests` does.

### Phase 0 - Groundwork

- [x] Run the existing test suite and record the pass count as the baseline.
- [x] Read how `World` boots two scripts with different vocabularies (`FakeConditionVocabulary` registers one script's names), and write down how a test makes script A own `altitude` and script B not.
- [x] Read `Request`/`Response` body access (`BString`, `CreateResponse`) and `SendUnicastRequest` with a response callback, and note how a response is matched back to its request (`activeRequests`, `RespondingToId`).
- [x] Confirm where `Evaluate` is called from (`CommandBus.RunIfStep` and the rule pass) so the non-blocking form changes both.

**Findings:**

- Baseline: 648 tests passing.
- Two scripts: `world.CreateScript(grid).WithMother().WithModules(m => new FakeConditionVocabulary(m)).OnNetwork().Boot()` for the owner, the same without `WithModules` for the asker. Scripts on one grid share a terminal system, so both see every block (as in the game), and only the registered vocabulary differs. Each script's `BlockCatalogue` must be refreshed (`RefreshConstruct()` and a few `Clock.Run()`) after the others boot, or it does not know their programmable blocks and the `cond` route refuses them. `IGC.Me` must equal the programmable block's EntityId, as in the game (the harness sets it).
- `activeRequests` is cleared at the end of every `HandleIncomingIGCMessages`, so a callback is lost if any other message is handled before the response arrives. That is fine for `sync`, which repeats, but not for `cond` (see Phase 3).
- `Evaluate` is called from `CommandBus.RunIfStep` only. The rule pass in `EvaluateRules` has its own path (`Probe`).
- Message ids were `timestamp_random`, and two requests created in the same instant shared one. `GenerateUniqueId` now adds a counter, so a batch of requests each has its own id.

**Done when:** baseline recorded, the two-script test setup is understood. **Met.**

### Phase 1 - Advertise names

- [x] `ConditionModule`: add `Names(bool preds)` returning the lower case keys of `Props` (false), or of `Preds` plus the `Words` (true), as one comma separated string. Skips `name.band` keys. It takes the flag because `Props` and `Preds` are two keys.
- [x] `IntergridMessageService`: add `ConstructVocab` (`Dictionary<long, HashSet<string>>`), cleared in `Boot()` next to `ConstructViews`.
- [x] Add `Props` and `Preds` keys to the bodies built in `ConstructPing` and `HandleSyncRequest` (one shared `SyncBody()`), from `ConditionModule.Names()`. Guard with `Mother.GetModule<ConditionModule>()` so a script without the module still syncs.
- [x] `SyncConstructCommands`: store the names per origin (skip self), and when the stored set differs, emit `ConstructVocabChangedEvent` (new, `src/Modules/IntergridMessageService/Events/`). A script heard for the first time with no names emits nothing. Added to [MotherCore.projitems](../src/MotherCore.projitems).
- [x] Tests: two scripts sync and each holds the other's names in `ConstructVocab`. A script's own names are not stored. A changed set emits the event once, an unchanged set emits nothing.

**Done when:** after a sync, script B knows that script A owns `altitude`, and the event fires only on change. **Met.**

### Phase 2 - Resolve and hold remote terms

- [x] `Term`: add `Owners` (`List<long>`, the scripts that advertise the name, sorted by id). A term with owners is remote. **Done on `Rule` instead** (`Rule.Owners`, `Rule.Cond`): a predicate that no local module defines has no rows to build terms from, so the whole condition is remote and is sent as text.
- [x] `Parse`: an unknown property or predicate that appears in `ConstructVocab` is marked remote instead of returning `unknown property`. A name no script owns returns a new pending state instead of dropping the rule. A remote name skips the local target check only for `ship` (the owner's controller). Any other target must exist on this script too, and a `$VARIABLE` that only another script defines is left unresolved here, so it is refused locally (`no target: $X`, or `unknown variable in: ...` in the value) and never sent. An inline `if` (`ask` false) still returns the old error until Phase 5.
- [x] `Rule`: add `Pending` (waiting on a name no script owns yet). `Load` keeps pending rules, logs once, and `Rebuild` retries them. The log is `Rule x disabled: unknown property: y`, with ` (no script on the construct has it)` added when other scripts are on the construct, so a lone script logs what it did before.
- [x] Subscribe `ConditionModule` to `ConstructVocabChangedEvent` and call `Rebuild()`. Rebuild keeps each rule's state by name, as it does today.
- [x] A local name is parsed as local even if another script also advertises it. A remote predicate row is not supported inside a local predicate: a row with an unknown property is still the old error.
- [x] Add a three state result for `Test`: true, false and *unanswered* (no local block answered any row). `Test` became `Probe` (private) and `Check` (public, for the `cond` route and tests). `Evaluate` wraps `Check`.
- [x] Tests: a rule on `altitude` in script B is kept, not dropped, and logs once. It resolves after A's sync arrives. A local name is never marked remote. `Check` reports unanswered for a battery group when only the tank row of `low` is local.

**Done when:** a rule naming a remote property no longer disables, and local evaluation is unchanged (all existing `ConditionModuleTests` pass). **Met.**

### Phase 3 - `cond` route and batching

- [x] `ConditionModule`: add the outbox (`Outbox`, per owner id, the queued conditions with their callbacks) and a result table (`Replies` and `Heard`, keyed by owner and condition text) so a result can be matched back. Results are keyed by condition rather than by index, so they survive a `Rebuild`, and rules with the same condition share them.
- [x] Add `ConditionModule.Ask(ownerId, condition, tripped, onResult)`: dedupes identical `(Condition, Tripped)` in the outbox (their callbacks are combined). The index is the position in the request, so `Ask` returns nothing.
- [x] Flush the outbox at the end of the rule pass (`EvaluateRules`) with the public `Flush()`: one request per owner, split into groups of 10, built with `IMS.CreateRequest("cond", ...)` using the `C0`/`T0`, `C1`/`T1` keys. Sent with the new `IMS.SendAwaitedRequest`, see below.
- [x] `IntergridMessageService`: register the `cond` route. It first checks the construct only rules: arrived on `CONSTRUCT_CHANNEL_TAG` only, and `OriginIsOnConstruct(OriginId)`. Otherwise it returns `null` and does nothing. It calls `ConditionModule.Answer` for each condition, and replies `R0`, `R1`, ... as `1`, `0`, `?` or `!reason`. `Answer` never runs a command, and a condition that does not parse gives `!reason`.
- [x] `Check` returns the three state result so `?` is possible. A condition with `--band` and `Tripped = 1` applies the band as a local tripped rule would (the same `Probe(rule, tripped)`).
- [x] The response callback writes each result into the result table. A request that is not answered is dropped at the second sync after it was sent. **`SendUnicastRequest` was not enough:** its callback is lost when any other message is handled before the response arrives (`activeRequests` is cleared after each handling), so `SendAwaitedRequest` keeps the callback until the response or the second sync.
- [x] Tests: 8 remote rules for one owner send 1 request, for two owners send 2, 12 conditions split into 2 requests. Identical conditions are sent once. A request from another channel or an off construct origin gets no reply and nothing is evaluated. `cond` never runs a command. A reply is not lost when another message is handled first.

**Done when:** a script can send a batch to an owner, get per-condition results back, and the route refuses anything that is not on the construct. **Met.**

### Phase 4 - Remote `when` rules

- [x] `Test` for a remote term: evaluate local rows first. If unanswered, queue a condition per advertised owner and use the **last answer received** (initially `false`).
- [x] Combine owners: the lowest script id whose reply is not `?` decides. All `?` is false. All owners are asked in the same pass, and while a lower owner has not answered yet its answer is waited for. An `!reason` reply counts as `?` and is logged once.
- [x] Send `Tripped` with each request so the owner applies the band. Strip `--for`, `--cooldown` and `--every` from the text. Keep `--band` and `--at`.
- [x] Respect `--every` and the shared `ruleInterval` when queueing, so a rule that is not due sends nothing.
- [x] Treat an answer older than 3 ping intervals (plus the rule's own period, `--every` or `ruleInterval`), or an owner no longer in `ConstructVocab`, as `false`, and log it once. Nothing removes an owner from `ConstructVocab` when it leaves, so a gone owner is found by its answers aging out.
- [x] Tests: `when ship altitude < 100 --band=20 --for=2` in script B fires after A answers `1` and holds `--for`, then resets above the band using `Tripped`. An owner that leaves makes the rule false with one log line. Both owners answering `?` gives false. The lower id wins when both answer.

**Done when:** the Mother GUI `approach` rule works against a MAPS script in the harness, and no remote request is sent for a rule that is not due. **Met** with `FakeConditionVocabulary` standing in for MAPS. The MAPS suite itself is Phase 6.

### Phase 5 - Remote `if` and `elseif`

- [x] `ConditionModule.Evaluate`: add a non-blocking form that returns a result, or `Pending` with a handle when a remote answer is needed. Keep the current signature for local-only callers. **Done as `Begin(command, out error, out poll)`:** it returns `1` or `0` at once, or `2` (pending) with `poll`, a `Func<string>` that returns `null` while waiting, then `"1"` or `"0"`, or the failure text. `Evaluate` is unchanged and stays local only. Conditions queued by an `if` go out on every `Tick` (not only on a rule pass), so a wait is one cycle, not one `ruleInterval`.
- [x] `CommandBus`: split `RunIfStep` into "start evaluating" and "apply the result" (`ApplyIf`). In `ExecuteCommandGroupCoroutine`, `yield return 0` while the handle is pending, then apply the result with the same `ifs` stack rules (`3`, `0`, `2`, `4`, `5`, `6`).
- [x] Do not change the skip rules: a skipped `if`, and an `elseif` after a branch already ran, never evaluate and never queue a request.
- [x] Timeout: `[general] condTimeout` seconds (default 2). On timeout, the step fails with `no answer from script <id> for <name>` (the sync does not carry script names, so the id is used). A name no script owns fails at once with the usual `unknown property: x` / `unknown predicate: x`, without a request.
- [x] Add `condTimeout` to the Configuration reading in `Boot()` like `ruleInterval`.
- [x] Tests: the routine pauses at a remote `if`, then runs the right branch. Steps after it do not run before the reply. Two routines in one cycle share a request. A timeout fails the step and stops the routine. An unowned name fails at once. A local `if` returns in the same step with no request. All existing `CommandBusIfTests` still pass.

**Done when:** a remote `if` and a local `if` behave the same from the routine's side, apart from the wait. **Met.**

### Phase 6 - Wire up, document, verify

- [x] Run the full MotherCore suite, then the MotherOS and MAPS suites against the new Core. MotherCore 700, Mother OS 391, MAPS 46 (with the new test below) and Mother GUI 48 all pass.
- [x] Add a MAPS test that a Mother GUI style script with no vocabulary resolves `ship altitude < 100` through MAPS. `SharedConditionTests` in the MAPS suite: the asker knows MAPS's names after a sync, and an inline `if` on `altitude` and on `inspace` and a `when` rule on `altitude` are answered by MAPS.
- [x] Build Mother GUI, Mother OS and MAPS with the optimizer and record the minified size change against the 100,000 character limit (Release builds, characters of the packaged `script.cs`, Core at the branch start against now):

  | Script | Before | After | Change | Left under 100,000 |
  |---|---|---|---|---|
  | Mother OS | 94,017 | 98,156 | +4,139 | 1,844 |
  | MAPS | 92,124 | 96,257 | +4,133 | 3,743 |
  | Mother GUI | 95,054 | 99,175 | +4,121 | 825 |

  All three fit. Mother GUI has the least room, so the next addition to Core needs a size pass first.
- [x] Check the Mother GUI blueprint on the construct: `approach`, `cruiseLights`, `if ship inspace` and `if ship ingravity` work with MAPS and Mother OS present. Checked in game by the author. The harness covers the failure case (a name no script owns reports once and the rule or `if` does not run), but removing MAPS from a live construct was not checked.
- [x] Update [ConditionalLogicSpec.md](../src/Specs/ConditionalLogicSpec.md) (vocabulary is shared across the construct), [src/README.md](../src/README.md) and add a CHANGELOG entry.
- [x] Mark this spec's status as implemented.

**Done when:** all suites are green, size is within the limit, and the GUI blueprint rules and ifs work on a construct with the other scripts, with no per-script vocabulary added to the GUI. **Met.**
