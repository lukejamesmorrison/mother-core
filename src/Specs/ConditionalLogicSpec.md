# Conditional Logic - Spec

> Scope: named condition rules in a dedicated `[rules]` section (a `when` guard written as a routine), inline `if`/`else`/`end` in any routine, named predicates, new `ConditionModule`, reuse of `TerminalCommand`, `CommandBus` and `BaseModule` state tracking. Event hooks in `[hooks]` are unchanged.
> Constraints: low character count (script size limit), low per-tick instruction cost, C# 6.

---

## Goal

Let players declare "when X, run routine Y (else routine Z)" in Custom Data, in the same language as the rest of Mother: routines of `command args --option=value` steps. Everything on the right-hand side is an existing command or routine, so `CommandBus` is reused unchanged.

## Design in one paragraph

There is **one condition**: `<target> <predicate | property op value> [--options]`. It is parsed by the existing `TerminalCommand`, resolved and evaluated by one `ConditionModule` function. Two commands bind it:

| | `when` - rule | `if` - command |
|---|---|---|
| Question | "tell me when this *becomes* true" | "is this true *right now*?" |
| Where | `[rules]` section (`when` is the first step) | any routine step (`[commands]`, `[menu:...]`, `[rules]`, `[hooks]`) |
| Evaluated | polled by `ConditionModule`, on a schedule | once, when the step runs |
| State | edge, `--band`, `--for`, `--cooldown`, `--every` | none (stateless) |
| Branches | then = false->true edge, `else` = true->false edge | then = true now, `else` = false now, closed by `end` |

Predicates (`low`, `critical`, `ready`, `locked`, ...) are named conditions with defaults, so common cases read like plain English, and the expression form covers anything custom. Event hooks (`"Block".onLock`) are unchanged.

## Hooks vs rules

- **Event hooks** (`[hooks]`, unchanged) are keyed by an identifier (`onLock`, `onUnlock`, `onReady`, `onMerge`, ...) and fired by modules via `BlockCatalogue.RunHook(block, name)`. They are declared in a block's own `[hooks]` section (`onLock=...`) or centrally in the Programmable Block's `[hooks]` as `"Block Name".onLock=...`.
- **Rules** (`[rules]`, new) are named routines evaluated by `ConditionModule` on a schedule. They live in the Programmable Block's `[rules]` section, or in a `[rules]` section in a block's own Custom Data (where `this` means that block).
- The two sections are independent: `ConditionModule` loads `[rules]` itself and `BlockCatalogue`'s hook loading is not touched. A rule name and an event name can never collide.

## Syntax

```ini
[rules]
lowBattery=
| when "$BATTERIES" low;
| view/notify "$MENU_SCREEN" "Low battery." --level=error;
| else;
| view/notify "$MENU_SCREEN" "Battery recovered." --level=success;

cruiseLights=
| when "$COCKPIT" speed > 30 --for=3;
| block/off "$LIGHTS_LANDING";
| block/off "$LIGHTS_HEADLIGHT";
| else;
| block/on "$LIGHTS_LANDING";
```

### The `when` step

```
when <target> <predicate>                    [--options]
when <target> <property> <operator> <value>  [--options]
```

- It is parsed by the existing `TerminalCommand`, so quoting, `$VAR` substitution and `--key=value` / bare `--flag` options are identical to every other command. Operators must be separated by spaces (`speed > 30`, not `speed>30`) so they tokenise as arguments.
- `<target>` is a quoted block or group name, `$VAR` or `"$VAR"`, or `this` (inside a block's own Custom Data). A `$VAR` whose value has spaces is quoted for you. Same resolution as `block/on`.
- Operators: `<  >  <=  >=  ==  !=`. The value is a number, a `%` ratio (0-1), or a state word (`locked`, `connected`). `$VAR` works in values and options (`--at=$MIN_POWER`).
- Options (all optional, all `--key=value`):

| Option | Meaning |
|---|---|
| `--band=n` | Hysteresis: trip at `< 20`, reset only above `20+n` |
| `--for=s` | Debounce: condition must hold continuously `s` seconds before firing |
| `--cooldown=s` | Minimum seconds between firings |
| `--every=s` | Evaluate this rule every `s` seconds instead of the shared interval |
| `--at=value` | Override a predicate's default threshold (`when "$BATTERIES" low --at=25%`) |

### Rule body

- Steps after `when` form the **then** routine (fires on false->true).
- An optional `else;` step starts the **else** routine (fires on true->false, after the same hysteresis).
- Each step is a normal routine step (`| command args;`), including `wait`, `view/go`, `@Grid ...`, and `!cmd`.
- The rule name (the key) identifies the rule in logs and errors and is unique per `[rules]` section.

### Predicates

A predicate is a named expression registered per block type, with a default threshold and band. It is plain data on top of the property registry, so predicates and expressions share one parser and one evaluator.

| Predicate | Targets | Default meaning (proposed) |
|---|---|---|
| `low` | battery, tank | `charge/fill < 20%` (tank `< 15%`), band 5% |
| `critical` | battery, tank | `< 10%`, band 3% |
| `empty` / `full` | battery, tank | `< 2%` / `> 98%` |
| `ready` | jump drive | `charge > 99%` |
| `connected` | connector | `status == connected` |
| `locked` | landing gear, connector | `status == locked` |

`--at` overrides the threshold. Mixed-type groups only evaluate blocks that define the predicate.

### Event hook one-liners (unchanged)

Event hooks keep their terse form and are the right tool for module-raised events:

```ini
"BLD - Connector".onLock=shutdown;
"BLD - Connector".onUnlock=start;
```

A rule is the right tool when state is polled, when you want an `else` branch, or when hysteresis/debounce matter.

## Inline conditions - `if`

`if` asks the same question as `when`, once, from inside any routine. It reuses the condition grammar, predicates, aggregates and target resolution unchanged; only the binding differs.

```
if <target> <predicate>                    [--at=value]
if <target> <property> <operator> <value>  [--at=value]
| ...steps when true...
else;
| ...steps when false...
end;
```

- `else;` is optional. `end;` closes the block, because unlike a rule a routine keeps running after the condition. A block still open at the end of a routine (or `{}` parallel group) is closed implicitly.
- Blocks nest: `if` inside `if`, or `if` inside a `when` rule's then/else branch.
- `if` is stateless, so `--band`, `--for`, `--cooldown` and `--every` are errors on `if`. `--at` is allowed.
- It is a normal step: waits, `view/notify`, `@Grid ...` and named commands work inside branches, and a command can itself contain `if` blocks.

### Where `if` works

`if` / `else` / `end` are routine steps handled by `CommandBus`, so they work in **every section that executes commands**, with no per-section support: `[commands]`, `[menu:...]` entries (`MenuModule` runs entries through `RunTerminalCommand`), rule bodies and `[hooks]` values. Menu submenu entries (`.Day=`) are unaffected because `.` is a menu key prefix, not a step.

### Nesting

Every `if` consumes one `end;`, so blocks nest to any depth. Steps are trimmed before parsing, so indentation after the `|` is free. Indent each nested block two spaces and put `else;` and `end;` at the same column as their `if`: the shape of the text is then the shape of the logic.

**What the player wants** (the `Land` button in the Flight menu):

```
Land pressed
├─ gear unlocked?
│   ├─ yes ─ altitude < 100 m?
│   │         ├─ yes ─ landing light ON, sensors ON
│   │         │        └─ hydrogen low? ── yes ─ notify "Hydrogen low for landing."
│   │         └─ no  ─ notify "Descend below 100 m first."
│   └─ no  ─ notify "Gear already locked."
```

**What they write:**

```ini
[commands]
Land=
| if "$LANDING_GEAR" unlocked;
|   if grid altitude < 100;
|     block/on "BLD - Light - Landing";
|     block/on "$SENSORS";
|     if "$TANKS_H2" low;
|       view/notify "$MENU_SCREEN" "Hydrogen low for landing." --level=error;
|     end;
|   else;
|     view/notify "$MENU_SCREEN" "Descend below 100 m first." --level=error;
|   end;
| else;
|   view/notify "$MENU_SCREEN" "Gear already locked." --level=success;
| end;
```

**What they see** (the notification on `$MENU_SCREEN`, one outcome per situation):

| Gear | Altitude | Hydrogen | Result |
|---|---|---|---|
| unlocked | < 100 m | ok | landing light + sensors on, no message |
| unlocked | < 100 m | low | landing light + sensors on, "Hydrogen low for landing." |
| unlocked | >= 100 m | any | "Descend below 100 m first." |
| locked | any | any | "Gear already locked." |

**The same logic, flat.** When a branch grows, move it into a named command. Each command stays one screen tall, shows its own outcome list, and can be called from a menu, a rule or another command:

```ini
[commands]
Land=
| if "$LANDING_GEAR" locked;
|   view/notify "$MENU_SCREEN" "Gear already locked." --level=success;
| else;
|   if grid altitude < 100;
|     landingSetup;
|   else;
|     view/notify "$MENU_SCREEN" "Descend below 100 m first." --level=error;
|   end;
| end;

landingSetup=
| block/on "BLD - Light - Landing";
| block/on "$SENSORS";
| if "$TANKS_H2" low;
|   view/notify "$MENU_SCREEN" "Hydrogen low for landing." --level=error;
| end;
```

The same `Land` works directly as a menu entry, since `[menu:...]` entries run through the same routine executor:

```ini
[menu:FlightMenu]
Land=
| if "$LANDING_GEAR" locked;
|   view/notify "$MENU_SCREEN" "Gear already locked." --level=success;
| else;
|   landingSetup;
| end;
```

Readability guidance:
- Prefer **two levels at most** inline. Deeper logic becomes a named command called from the branch (`| landChecks;`), since commands already compose and a command's blocks are balanced independently.
- The `end;` pile at the bottom is the cost of nesting. Chains of `else; if ...; end; end;` flatten with `elseif` (phase 2, below).
- A rule's `when` is only valid as the **first** step; `when` inside `if` (or nested) is an error. An `if` inside a rule's then/else branch is fine.

### `elseif` (phase 2)

`elseif <condition>;` continues the same chain as the `if`, so a chain takes one `end;` and reads as a flat list of cases instead of a staircase. The `Land` command above, flattened:

```ini
[commands]
Land=
| if "$LANDING_GEAR" locked;
|   view/notify "$MENU_SCREEN" "Gear already locked." --level=success;
| elseif grid altitude >= 100;
|   view/notify "$MENU_SCREEN" "Descend below 100 m first." --level=error;
| else;
|   landingSetup;
| end;
```

- **Semantics:** branches are tried top to bottom and the first true one runs; `else;` runs only if none did. A condition after the first true branch is not evaluated.
- **Skip stack:** each open `if` holds `(parentActive, taken)`. `elseif` evaluates its condition only if `parentActive && !taken`; `else` runs only if `parentActive && !taken`; `end` pops. Still no parser change.
- **Same grammar:** `elseif` takes exactly the condition `if` takes (predicate or expression, `--at`), and the same rule applies that stateful options are errors.
- **Misuse:** `elseif` after `else`, or with no open `if`, fails the step like a stray `else`/`end`.
- **Rules:** `when` rules do not get `elseif`. Separate rules express "different branch per state" with no ordering dependency; use `if`/`elseif` inside a rule's branch when you need an ordered chain.

How it runs (small change in `CommandBus`): `RoutineState` gains a skip stack. `ExecuteCommandGroupCoroutine` handles `if` / `else` / `end`: `if` asks `ConditionModule.Evaluate(command)` (the same function the watcher uses, returning true/false/error), pushes the result, and steps are skipped while the top of the stack is false. `else` flips it, `end` pops it. No parser changes, and waits still work because the stack lives in the coroutine's state.

Skipped steps are not variable-resolved or executed, so `if` also works as a guard (`if ... ; ... else; view/notify ... ; end;`).

### Text compare

`if <a> == <b>;` and `if <a> != <b>;` (and `elseif`) compare two values as text, ignoring case. `CommandBus.RunIfStep` handles them after variables are resolved and before `ConditionModule` is asked, so no new vocabulary is needed:

```ini
[commands]
Gate=
| if $FLIGHT_MODE == parked;
|   view/notify "$MENU_SCREEN" "Parked." --level=success;
| elseif $FLIGHT_MODE != off;
|   view/notify "$MENU_SCREEN" "Flying or docked." --level=success;
| end;
```

- **Shape:** exactly three arguments, the middle one `==` or `!=`, and no options. Quote a value with spaces (`"cruise atmo"`).
- **Variables:** `$NAME` is resolved when the step runs, so a `var/set` earlier in the same routine is seen. An undefined variable stays as literal text (`$NAME`) and does not match a real value.
- **Everything else is unchanged:** `<target> <predicate>` and `<target> <property> <operator> <value>` work as before. Any other three-argument form, or one with options such as `--for`, fails with `bad syntax`.
- **Rules:** `when` does not support it. A rule keeps state on a block property, and a text compare has none. Put the compare inside the rule's branch.
- **Remote scripts:** it is evaluated on the script that runs the routine and never sent to another script.

## Group semantics

A condition on a group (or a name matching several blocks) is an **aggregate** and fires **once**:
- Default aggregate is `avg`. Override with a property suffix: `charge.min`, `charge.max`, `charge.sum`.
- Blocks that do not expose the property (wrong type in a mixed group) are skipped. If none do, the condition is false and the rule logs once.
- To fire per block, put a rule with `this` in each block's own Custom Data.

## `@` and block references

`@` keeps exactly one meaning: a remote **grid** target as the first term of a routine (`@Grid command ...`, `*` for all grids). Block references never use `@`; they are quoted names, optionally via `$VAR`, exactly as in `block/on "$BATTERIES"`.

| Symbol | Meaning |
|---|---|
| `@Grid` | Remote grid (first term of a routine only) |
| `*` | All grids |
| `!cmd` / `!!cmd` | Important command / force local |
| `$VAR` | Variable |
| `"Name"` | Block or group |
| `--option=value` | Option (rule options use the same syntax) |

## Examples - Nova Blade Mk1

Block and group names come from `nova-blade-mk1-mother-os-custom-data.ms` (`$VARS` are the ones already defined there). Rules go in the Programmable Block's `[rules]` section; event hooks stay in `[hooks]`. Everything except the event hooks is proposed syntax.

```ini
[rules]
;------------------------------------------------------
; Power: warn at 20%, recover above 25% (default low band)
;------------------------------------------------------
lowBattery=
| when "$BATTERIES" low;
| view/notify "$MENU_SCREEN" "Low battery." --level=error;
| else;
| view/notify "$MENU_SCREEN" "Battery recovered." --level=success;

; any single battery nearly empty (min aggregate), debounced
weakCell=
| when "$BATTERIES" charge.min < 5% --for=5;
| battery/auto "$BATTERIES";

;------------------------------------------------------
; Low power mode: drop non-essentials when critical
;------------------------------------------------------
lowPower=
| when "$BATTERIES" critical --for=3;
| block/off "$HOLOLCDS_GROUP";
| block/off "$SEARCHLIGHTS";
| block/off "BLD - Projector";
| else;
| block/on "$HOLOLCDS_GROUP";

;------------------------------------------------------
; Gas tanks
;------------------------------------------------------
lowHydrogen=
| when "$TANKS_H2" low;
| view/notify "$MENU_SCREEN" "Hydrogen low." --level=error;

lowOxygen=
| when "$TANKS_O2" low --at=25%;
| view/notify "$MENU_SCREEN" "Oxygen low." --level=error;

;------------------------------------------------------
; Lights follow speed
;------------------------------------------------------
cruiseLights=
| when "$COCKPIT" speed > 30 --for=3;
| block/off "BLD - Light - Landing";
| block/off "$LIGHTS_HEADLIGHT";
| else;
| block/on "BLD - Light - Landing";

;------------------------------------------------------
; Landing gear locked = parked
;------------------------------------------------------
parked=
| when "$LANDING_GEAR" locked --for=2;
| battery/charge "$BATTERIES";
| tank/stockpile "$TANKS_H2";
| else;
| battery/auto "$BATTERIES";
| tank/share "$TANKS_H2";

;------------------------------------------------------
; Altitude-assisted landing (needs an `altitude` property on the cockpit)
;------------------------------------------------------
approach=
| when "$COCKPIT" altitude < 100 --for=2;
| block/on "BLD - Light - Landing";
| block/on "$SENSORS";
| else;
| block/off "BLD - Light - Landing";
| block/off "$SENSORS";

;------------------------------------------------------
; Jump drive ready
;------------------------------------------------------
jumpReady=
| when "$JUMP_DRIVE" ready;
| view/notify "$MENU_SCREEN" "Jump drive ready." --level=success;
```

Event hooks (docking) stay in `[hooks]` and work today:

```ini
[hooks]
"BLD - Connector".onLock=shutdown;
"BLD - Connector".onUnlock=start;
```

A block-local rule uses `this` and lives in a `[rules]` section of the block's own Custom Data:

```ini
[rules]
jumpReady=
| when this ready;
| view/notify "BLD - Cockpit [LCD]:1" "Jump drive ready." --level=success;
```

Registry needs for this config: properties `charge` (batteries, jump drive), `fill` (tanks), `status` (connector, gear), `speed`, `altitude` (cockpit); predicates `low`, `critical`, `ready`, `locked`. Flight modes (Landing, Cruise) currently live only in the GUI menu; moving them to `[commands]` would let rules call them (`| !landing;`).

### Inline `if` examples (commands and menus)

Each example shows what the player sees: the menu entry they press, what the cockpit notification says, and the outcomes side by side.

**Toggle from one menu entry.** `on` / `off` are predicates for any functional block (`Enabled`), so a single button replaces separate On and Off entries.

```ini
[menu:HeadlightsMenu]
Toggle=
| if "$LIGHTS_HEADLIGHT" on;
|   block/off "$LIGHTS_HEADLIGHT";
|   view/notify "$MENU_SCREEN" "Headlights off." --level=success;
| else;
|   block/on "$LIGHTS_HEADLIGHT";
|   view/notify "$MENU_SCREEN" "Headlights on." --level=success;
| end;
```

| Headlights | Press `Toggle` | Cockpit shows |
|---|---|---|
| on | turns off | "Headlights off." |
| off | turns on | "Headlights on." |

**Refuse an unsafe action.** The notification tells the player why nothing happened.

```ini
[commands]
shutdown=
| if "$LANDING_GEAR" locked;
|   tank/stockpile "$TANKS_H2";
|   tank/stockpile "$TANKS_O2";
|   block/off "$THRUSTERS";
|   battery/charge "$BATTERIES";
|   view/notify "$MENU_SCREEN" "Systems shutdown." --level=success;
| else;
|   view/notify "$MENU_SCREEN" "Cannot shut down: landing gear not locked." --level=error;
| end;
```

| Landing gear | Press `Shutdown` | Cockpit shows |
|---|---|---|
| locked | systems shut down | "Systems shutdown." |
| unlocked | nothing changes | "Cannot shut down: landing gear not locked." (red) |

**Do a prerequisite, then continue.** An `if` with no `else` is a conditional step; the routine carries on afterwards.

```ini
[commands]
start=
| if "$CONNECTOR" connected;
|   view/notify "$MENU_SCREEN" "Undocking..." --level=success;
|   connector/unlock "$CONNECTOR";
|   wait 1;
| end;
| battery/auto "$BATTERIES";
| block/on "$THRUSTERS";
| view/notify "$MENU_SCREEN" "Systems started." --level=success;
```

| Connector | Press `Start` | Cockpit shows |
|---|---|---|
| connected | unlocks, waits 1 s, then starts | "Undocking..." then "Systems started." |
| not connected | starts immediately | "Systems started." |

**Warn, then carry on.** The warning is one extra line, and the flight mode still applies.

```ini
[menu:FlightMenu]
Landing=
| if "$TANKS_H2" low;
|   view/notify "$MENU_SCREEN" "Hydrogen low for landing." --level=error;
| end;
| block/on "$THRUSTERS";
| block/on "BLD - Light - Landing";
| view/notify "$MENU_SCREEN" "Landing mode set." --level=success;
```

**Rule decides *when*, command decides *what*.** Both read the same condition, so nothing is written twice:

```ini
[rules]
parked=
| when "$LANDING_GEAR" locked --for=2;
| shutdown;
```

Now `shutdown` is checked in two places: pressing it by hand still asks whether the gear is locked, and the `parked` rule only fires after the gear has been locked for 2 s.

`$SENSORS` and `$LIGHTS_*` names exist in the GUI config; `BLD - Projector` and `BLD - Light - Landing` are shown as literal names because the OS config does not define variables for them.

## Where rules live: `[rules]` (decided)

Rules live in a dedicated `[rules]` section; `[hooks]` stays for module-raised event hooks. The comparison that led here:

| | `[hooks]` | `[rules]` (dedicated) |
|---|---|---|
| Mental model | one place for "react to something" | event hooks vs polled rules are separate things |
| Loader reuse | shares `this` and reload with block hooks | none needed: `ConditionModule` loads its own section |
| Change to existing code | `BlockCatalogue` hook loader must hand off rule keys | none; subscribe to `SystemConfigChangedEvent`, `BlockConfigChangedEvent`, `ConstructRefreshedEvent` |
| Key discrimination | key with `.` = event hook, key without = rule, plus "value starts with `when`" in block-local sections | none; everything in the section is a rule |
| Name collisions | a rule named `onLock` collides with an event name; a mistyped `"Block"onLock` (missing `.`) silently becomes a rule | rule names are their own namespace |
| Cost visibility | rules look like free hooks | rules are the polled, instruction-costing things, visible at a glance |
| Management | none natural | `rule/on name`, `rule/off name`, `rule/list` (tier 2) get a clean noun |
| Validation / errors | mixed messages | "rule X ..." only |

**Case for `[rules]`:**
1. The original reason for hooks was loader reuse (`this`, lifecycle, name resolution). Named routines are not keyed by a block, so the only things shared are `this` (trivial: the rule loader knows its block) and the reload events, which `ConditionModule` can subscribe to directly.
2. `BlockCatalogue`'s hook loader is the riskiest code to touch: it splits keys with `Split('.')`, and `this` replacement is private. `[rules]` leaves it, and every existing hook, untouched.
3. Hooks are free until a module fires them; rules cost instructions every interval. A separate section makes that obvious, makes budget warnings simple ("rule X resolves 140 blocks"), and gives the rule-management commands a noun.
4. Cleaner documentation and errors: a rule never needs to explain why it is in a section named for events.
5. Adding the section is one name (`Configuration.DefaultSections`) plus the loader in `ConditionModule`.

**Case for `[hooks]`:** one fewer section to learn, and `"BLD - Connector".onLock` and `when ... locked` sit side by side so players can compare event and polled forms. This is a docs/UX benefit, and can be had by cross-referencing in docs.

**Decision:** `[rules]`. Event hooks (`"Block".onLock`, `onOpen`, ...) stay in `[hooks]` unchanged. Block-local rules live in a `[rules]` section of the block's own Custom Data. The one benefit of `[hooks]` (seeing event and polled forms together) is handled by cross-referencing in the docs.

## Rule budget (decided)

A fixed maximum rule count is the wrong limit: a rule's cost is roughly *blocks resolved x property read + compare*, so 5 rules over 100 batteries can cost more than 50 rules over single blocks. Rules are governed by a **budget, not a cap**:

1. **Instruction budget per pass**: evaluate rules round-robin from a saved cursor and stop for the tick once `Runtime.CurrentInstructionCount` passes a fraction of `Runtime.MaxInstructionCount` (default ~10%, configurable in `[general]`, e.g. `ruleBudget=10`). The remaining rules resume from the cursor next tick, so under load rules evaluate slightly later and never starve the rest of Mother (coroutines, IGC, views). Every rule is still evaluated each cycle, so none is starved either.
2. **Boot-time lint**: warn (never error) when a rule resolves a large block set (default > 100) or the total rule count is large (default > 50).
3. **Cheap by default**: shared interval, per-pass read cache and `--every` for slow rules (see Q1).
4. **Defaults are starting points**: tune them with the profiler (`Runtime.CurrentInstructionCount`) on a ~100-battery grid before treating the numbers as final.

## Syntax decisions

Considered and rejected:
- **Key-expression hooks** (`"Block".charge<20% band:10=...`, with a separate `:clear` key): MyIni splits a line at the first `=`, so keys cannot contain `==`, `<=`, `>=` or `flag=value`; the block name repeats on every rule; the clear branch must duplicate the condition text.
- **Rules inside `[hooks]`**: shared a section with event hooks but nothing else (see "Where rules live").
- **`$VAR` in the hook key** and **`[hooks:Target]` sections**: cosmetic improvements to the key form only; they do not remove its limits.
- **Semantic events only** (`"Block".onLow=...`): the most readable, but not general (no speed/altitude/custom thresholds) and cannot express an `else` branch. Kept as predicates inside `when` instead.

Chosen: named rule routine (`when` / `else`) + predicates, so one construct covers both plain-English cases and custom expressions, and options use the language's own `--key=value`.

## First-pass property registry

Derived from a survey of the commands, hooks, EventBus events and state selectors in MotherCore, MotherOS, MotherGUI and MotherAutopilotSystem, plus what the Nova Blade AutoLCD views already display (`working`, `EnabledCount`, `chargeSum`, `propbool Stockpile`, `tanks`, `Power`). A property is first-pass when a module already has a command or hook for that block's state, or a view already shows it.

### Conventions

- **Tier 1** = first pass (MVP). **Tier 2** = fast follow, same registry mechanism. **Tier 3** = needs new computation or a new public accessor, not just a registry row.
- **Key** is the property name in `when`/`if`. A key may exist for several block types (`charge` for batteries and jump drives); a block that does not define it is skipped (`NaN`).
- **`status`** is the uniform key for any block with a state enum. Its values compare by name (`status == locked`), and predicates are sugar over it.
- **Agg** is the default group aggregate: `avg` for ratios, `sum` for energy/force, `any` for booleans and enums. Override with a suffix (`.min`, `.max`, `.sum`, `.all`, `.none`); e.g. `"$LANDING_GEAR" status.all == locked`.
- **Source** is the typed PB API member (cheap, no terminal property lookups, per Q3).
- **Existing** names the Mother hook/event/command that already covers the same state, so the rule can be checked against it.

### System targets (non-block)

Some state does not belong to a block. A **bare word** target (unquoted) names a system scope; quoted names and `$VAR` are always blocks. This also keeps these unambiguous from `@Grid`.

| Target | Meaning | Source |
|---|---|---|
| `ship` | The grid's main controller (`Mother.ShipController`) | `IMyShipController` |
| `storage` | `LocalStorage` key/values (`set`/`get`) | `LocalStorage` |
| `var` | Config variables (`var/set`) | `Mother.Variables` |
| `fcs` | Flight control (Autopilot) | `FlightControlModule` |
| `plan` | Current flight plan (Autopilot) | `FlightPlanningModule` |
| `dock` | Docking state machine (Autopilot) | `DockingModule` |

`grid` avoids naming the cockpit in every rule: `when grid speed > 30 --for=3;` vs `when "$COCKPIT" speed > 30 --for=3;`. A named controller remains valid for blocks other than the main one.

### Tier 1 - blocks

| Key | Applies to | Source | Type / units | Agg | Predicates | Existing |
|---|---|---|---|---|---|---|
| `enabled` | any `IMyFunctionalBlock` | `Enabled` | bool | any | `on`, `off` | `block/on`/`off`, hooks `onOn`/`onOff`, `EnabledCount` view |
| `working` | any `IMyTerminalBlock` | `IsWorking` | bool | any | `working`, `broken` | `working {T:..}` view |
| `charge` | `IMyBatteryBlock` | `CurrentStoredPower / MaxStoredPower` | ratio 0-1 | avg | `low`, `critical`, `empty`, `full` | `battery/*`, `Power`/`chargeSum` views |
| `charge` | `IMyJumpDrive` | `CurrentStoredPower / MaxStoredPower` | ratio 0-1 | avg | `ready` (`> 99%`) | `chargeSum`, `chargeSumTime` views |
| `mode` | `IMyBatteryBlock` | `ChargeMode` | enum: `auto`/`recharge`/`discharge` | any | - | `battery/auto`/`charge`/`discharge` |
| `fill` | `IMyGasTank` | `FilledRatio` | ratio 0-1 | avg | `low`, `critical`, `empty`, `full` | `tank/*`, `tanks` view (commented-out selector in `GasTankModule`) |
| `stockpile` | `IMyGasTank` | `Stockpile` | bool | any | `stockpiling` | `tank/stockpile`/`share`, `propbool Stockpile` view |
| `status` | `IMyShipConnector` | `Status` | enum: `unconnected`/`connectable`/`connected` | any | `connected`, `connectable`, `disconnected` | hooks `onLock`/`onUnlock`/`onReady`, `Connector*Event` |
| `status` | `IMyLandingGear` | `LockMode` | enum: `unlocked`/`readytolock`/`locked` | any | `locked`, `ready`, `unlocked` | hooks `onLock`/`onUnlock`/`onReady`, `gear/*` |
| `status` | `IMyJumpDrive` | `Status` | enum: `idle`/`charging`/`ready`/`jumping` | any | `charging` | `JumpView` |
| `status` | `IMyDoor` | `Status` | enum: `open`/`opening`/`closed`/`closing` | any | `open`, `closed`, `opening`, `closing` | hooks `onOpen`/`onOpening`/`onClose`/`onClosing`, `door/*` |
| `status` | `IMyAirVent` | `Status` | enum: `pressurized`/`pressurizing`/`depressurized`/`depressurizing` | any | `pressurized`, `depressurized` | hooks `onPressurized`/..., `vent/*` |
| `oxygen` | `IMyAirVent` | `GetOxygenLevel()` | ratio 0-1 | avg | `low` (`< 50%`) | new read (`CanPressurize` also useful) |
| `status` | `IMyPistonBase` | `Status` | enum: `extending`/`extended`/`retracting`/`retracted`/`stopped` | any | `extended`, `retracted`, `extending`, `retracting` | hooks `onExtending`/..., `piston/*` |
| `position` | `IMyPistonBase` | `CurrentPosition` | metres | avg | - | `piston/distance`, `PistonView` |
| `angle` | `IMyMotorStator` (rotor/hinge) | `Angle` (deg) | degrees | avg | - | `rotor/angle`, `hinge/angle`, `RotorView`/`HingeView` |
| `locked` | `IMyMotorStator` | `RotorLock` | bool | any | `locked`, `unlocked` | `rotor/lock`, `hinge/lock` |
| `attached` | `IMyMechanicalConnectionBlock` | `IsAttached` | bool | any | `attached`, `detached` | hooks `onAttach`/`onDetach`, `rotor/attach` |
| `detected` | `IMySensorBlock` | `IsActive` | bool | any | `detected`, `clear` | hooks `onDetect`/`onClear` |
| `occupied` | `IMyShipController` | `IsUnderControl` | bool | any | `occupied`, `empty` | hooks `onOccupied`/`onEmpty` |
| `dampeners` | `IMyShipController` | `DampenersOverride` | bool | any | `on`, `off` | `dampeners/on`/`off` |
| `handbrake` | `IMyShipController` | `HandBrake` | bool | any | `on`, `off` | `handbrake/on`/`off` |
| `effectiveness` | `IMyThrust` | `MaxEffectiveThrust / MaxThrust` | ratio 0-1 | avg | `weak` (`< 30%`) | `ThrustersView`; lets rules fall back from ion/atmospheric thrusters |

### Tier 1 - system targets

| Target `key` | Source | Type | Predicates | Existing |
|---|---|---|---|---|
| `grid speed` | `IMyShipController.GetShipSpeed()` | m/s | - | new read (Autopilot only reads the velocity vector) |
| `grid altitude` | `TryGetPlanetElevation(Surface)` | metres, `NaN` in space | - | new read |
| `grid gravity` | `GetNaturalGravity().Length()` | m/s^2, 0 in space | `inspace`, `ingravity` | new read |
| `grid moving` | `speed > 0.1` (derived in Core, no Autopilot needed) | bool | `moving`, `stopped` | `GridMovingEvent`/`GridStoppedEvent` (Autopilot) |

`altitude` is the one property the first pass is committed to for flight-aware rules (approach, landing lights). `storage`, `var` and the Autopilot targets (`fcs`, `plan`, `dock`) are tier 2.

### Tier 2

| Key | Applies to | Source | Note |
|---|---|---|---|
| `count` | any resolved target | blocks matched | int; `BlockCatalogue` name/group lookup |
| `storage <key>` | `LocalStorage` | value of key | string/number; `set`, `get`. With `var`, the glue for user-defined state (`set mode landing` then `if storage mode == landing`) |
| `var <name>` | `Mother.Variables` | variable value | string/number; `var/set` |
| `fcs autopilot` | Autopilot | `AutopilotEngaged` | bool; `engaged`, `idle`; `fcs/start`, `fcs/stop` |
| `plan state` | Autopilot | `CurrentFlightPlan.CurrentState` | enum `inactive`/`active`/`complete`; `active`, `complete` |
| `dock state` | Autopilot | `DockingModule.State` | enum `idle`/`incoming`/`approaching`/`aligning`/`closing`/`opening`/`departing`/`docked`/`jolt`; `docked`, `docking` |
| `stored` / `output` / `input` | `IMyBatteryBlock` | `CurrentStoredPower`, `CurrentOutput`, `CurrentInput` | MWh / MW, `sum` |
| `capacity` | `IMyGasTank` | `Capacity` | litres, `sum` |
| `status` | `IMyShipMergeBlock` | `State` (`locked`/`working`/`none`) | hooks `onMerge`/`onUnmerge` |
| `open` | `IMyDoor` | `OpenRatio` | ratio |
| `speed`, `minlimit`, `maxlimit` | rotor/hinge, piston | `TargetVelocityRPM`, limits | `*/speed`, `*/ulimit`, `*/llimit` |
| `velocity` | `IMyPistonBase` | `Velocity` | m/s |
| `override` | `IMyGyro` | `GyroOverride` | `gyro/override` (registration currently commented out) |
| `thrust`, `maxthrust`, `override` | `IMyThrust` | `CurrentThrust`, `MaxEffectiveThrust`, `ThrustOverridePercentage` | N / ratio, `sum` |
| `counting` | `IMyTimerBlock` | `IsCountingDown` | `timer/*` |
| `running` | `IMyProgrammableBlock` | `IsRunning` | `pb/run` |
| `ship mass` | main controller | `CalculateShipMass().PhysicalMass` | kg; already read by `DockingModule` |
| `fcs speedlimit` | Autopilot | `SpeedLimit` | m/s |
| `plan waypoint` / `plan waypoints` | Autopilot | `NextWaypointIndex`, `GetWaypoints().Count` | index / count |
| `plan distance` | Autopilot | `GetDistanceToWaypoint` | metres; needs public accessor (internal today) |
| `dock procedure` | Autopilot | `DockingProcedure.State` (`inactive`/`requesting`/`approved`/`inprogress`/`denied`/`waiting`) | enum |
| `attitude mode` | Autopilot | `CurrentRotationMode` | enum |
| `almanac count` / `almanac nearest` | `Almanac.Records` | filter by `--type=`, `--iff=hostile|friendly|neutral`; distance from `Mother.CubeGrid` | proximity rules ("hostile within 2000 m") |
| `self state` / `self relay` | `Mother` | state (`booting`/`working`), `IsRelay` | `onBoot` already covers boot |
| `view active` | GUI `ViewModule` | active view/menu path per display | e.g. `if view active == MapView` |

### Tier 3 - needs new computation

- `ship stopdistance` / `ship stoptime`: from speed, mass and per-direction `MaxEffectiveThrust` (Autopilot has the thrust sums in `PropulsionModule.GetGroupMaxThrust`, but not stopping distance).
- `ship twr` (thrust-to-weight) and `ship accel`: `max thrust / (mass * gravity)`.
- `plan eta`: distance / speed.
- `ship pitch` / `ship roll`: absolute angles (Autopilot exposes only rotation error/rate per axis via `Attitude`).
- `attitude error` / `attitude rate` by axis, `calibration state`/`calibrating`: exist in Autopilot but are diagnostic.
- Event history predicates (`EventBus` keeps the last 128 emissions): rejected for first pass; hooks already cover event-driven cases.

### Not condition-worthy (configuration only)

`light/*` (colour, intensity, blink), `screen/*`, `sound/*`, `sorter/*`, `wheel/*`, `autolcd/*`, `view/*` (except `view active`), `blocks/tag`, `block/config`, `block/rename`, `Almanac` display modules. They are setters with no state worth branching on; `enabled` already covers on/off for all of them.

### Resulting scope

| | Count |
|---|---|
| Tier 1 block keys | 17 keys over 23 (key, block type) rows |
| Tier 1 system-target keys | 4 (`grid speed`, `altitude`, `gravity`, `moving`) |
| Tier 1 predicates | `on off working broken low critical empty full ready stockpiling connected connectable disconnected locked unlocked open closed opening closing pressurized depressurized extended retracted extending retracting attached detached detected clear occupied weak moving stopped inspace ingravity` |
| New API reads in Tier 1 | `oxygen`, `grid speed`, `grid altitude`, `grid gravity`, `effectiveness` |
| Already tracked by a module/hook/event | the rest |

Registry keys added by extension scripts (`fcs`, `plan`, `dock`, tier 2) are registered by the Autopilot script through `RegisterProperty`/`RegisterPredicate`, not by Core, so Core stays small and an Autopilot-less grid simply has no such targets. The registration API itself is part of the MVP so tier 2 needs no Core change.

## Q1 - Monitoring many block types/properties efficiently

1. **Property registry (single dictionary)**: `Dictionary<string, Func<IMyTerminalBlock, double>>` keyed by lowercase property name (e.g. `charge`). Each lambda casts with `as` and returns `NaN` when the block does not support the property:
   - `charge` -> `IMyBatteryBlock` `CurrentStoredPower / MaxStoredPower` (also `IMyJumpDrive`)
   - `fill` -> `IMyGasTank.FilledRatio`, or `GetInventory().CurrentVolume / MaxVolume` for cargo
   - `output`, `pos`, `speed` (`IMyShipController`), `altitude`, etc.
   - Enum properties (`status`) map to a number or compare by name.
   Adding a property costs one line; the evaluator is generic.
2. **Predicate registry**: predicates are rows of data over the property registry (`("low", "charge", "<", 0.20, band)`), parsed by the same path as expressions. Adding a predicate costs one line.
3. **Reuse `BlockCatalogue` target resolution**: resolve names/groups once at boot/rebuild and cache the block list on the rule. Never call `GetBlocksOfType` per tick.
4. **Aggregate** over the cached list with a single function (`avg`/`min`/`max`/`sum`).
5. **Shared reads**: if several rules reference the same block set and property, read it once per evaluation pass (per-pass dictionary keyed by `target|property`).
6. **Scheduling**: do not evaluate in `Run()` every tick. Register one scheduled task (e.g. every 100 ticks / ~1.6 s, configurable). `--every` overrides it for hot-path rules. Stagger rules across ticks to flatten instruction spikes.
7. **Reuse `BaseModule` state tracking**: `_stateSelectors`, `_stateHandlers` and `PreviousStates` already implement "read state, compare to previous, fire on change". Selector = registry lambda, previous state = last boolean result. Firing runs the rule's then/else routine through `CommandBus.RunTerminalCommand`, the same path `RunHook` uses.
8. **Edge-triggered, not level-triggered**: fire only when the result changes. This prevents repeated execution while the condition stays true.
9. **Invalidate caches** on `ConstructRefreshedEvent`, `BlockConfigChangedEvent` and `SystemConfigChangedEvent` (block added/removed, Custom Data changed). `ConditionModule` rebuilds its rules from `[rules]` on those events, so no extra invalidation path is needed.

## Q2 - Sliding values / flapping around a threshold

All optional per rule, with sane defaults:

| Mechanism | Option | Behavior |
|---|---|---|
| Edge trigger | (always on) | Fire once on transition; re-arm only after the condition becomes false |
| Hysteresis / deadband | `--band=n` | Trip at `< 20`, reset only when value `> 20+n`. Stored as separate trip/reset thresholds |
| Debounce | `--for=s` | Condition must hold continuously for `s` seconds before firing (timer started on first true, cleared on false) |
| Cooldown | `--cooldown=s` | Minimum time between firings, regardless of state |

Default: edge trigger + implicit 2% (ratio) / 1-unit band for `<`/`>` + one evaluation interval cooldown. Predicates carry their own default band. `==`/`!=` on enums need none.

State per rule (small struct/array): `bool Active; double Since; double LastFired`.

The `else` routine fires on true->false after the same hysteresis, giving clean on/off pairs (e.g. generators on at 20%, off at 80% with `--band=60`).

Time source: accumulate `Runtime.TimeSinceLastRun` rather than `DateTime`.

## Q3 - Terminal Properties/Actions vs. Programmable Block API

- `GetValue<T>("Name")`, `SetValue`, `ApplyAction("OnOff_On")` and `GetActionWithName` go through the terminal property/action dictionary: string lookups, boxing, delegates, higher instruction cost. Avoid on any polled path.
- Typed interface members (`IMyBatteryBlock.CurrentStoredPower`, `IMyPistonBase.CurrentPosition`, `IMyFunctionalBlock.Enabled`, `IMyDoor.Status`, `IMyGasTank.FilledRatio`, `IMyShipController.GetShipSpeed()`) are direct and cheap and should be used for condition reads.
- Terminal properties remain acceptable only as a low-frequency fallback for properties without a typed API (e.g. some mod blocks), as an opt-in `prop:Name` registry fallback.
- Actions in rule bodies run through existing commands, which choose their own implementation. Prefer API calls (`Enabled = true`) inside those commands.
- Verify with the profiler (`Runtime.CurrentInstructionCount`) before/after on a grid with ~100 batteries.

## Architecture / Reuse

- **New**: `ConditionModule : BaseModule` (single core module): `Condition` parse/resolve/evaluate (shared), `[rules]` loader, rule watcher, property + predicate registries, rule state. Public `Evaluate(TerminalCommand)` is the single entry point used by both `when` (polled) and `if` (inline).
- **Changed**: `Configuration.DefaultSections` gains `rules`. `CommandBus.RoutineState` gains an `if`/`else`/`end` skip stack handled in `ExecuteCommandGroupCoroutine`. `BlockCatalogue` and event hook loading are **not** changed.
- **Reuse**: `TerminalCommand` (tokenising `when`, `--options`, quoting), `MyIni` parsing, `BlockCatalogue` (name/group resolution), `CommandBus` for the then/else routines, `ConfigVariables` (`$var`), `BaseModule` state/handler, `SystemConfigChangedEvent`/`BlockConfigChangedEvent`/`ConstructRefreshedEvent`, existing scheduler. No new tokenizer or option parser.
- **Loader**: `ConditionModule` reads the PB's `[rules]` section and each block's own `[rules]` section (where `this` is that block). Other scripts' programmable blocks are skipped: their rules run on their own script. The rules rebuild one rule per cycle (`Tick`), the old rules keep running meanwhile, and changes that arrive during a rebuild share one more pass. For each rule: step 1 must be `when`, steps up to `else` are the then routine, the rest the else routine.
- **Size reduction tips**: one parse path (`when` -> `TerminalCommand`, predicate or 3-term expression); one `Compare(op, a, b)` switch; registries as collection initializers; no per-rule classes (tuple/array row); no LINQ in hot paths.
- **Rejected**: a `[when]` section (superseded by `[rules]`), rules inside `[hooks]` (see "Where rules live"), and `if ... then ...` inside `[commands]` (commands run on demand, not continuously; a rule can still call a command).

## Shared vocabulary across the construct

A property, predicate or state word is defined by the one script that owns the blocks (Mother OS: `charge`, `low`; MAPS: `altitude`, `speed`). Any other Mother Core script on the same construct can use it in a condition without registering anything, so Mother GUI's `when grid altitude < 100` and `if grid inspace` work with MAPS present. See [construct-shared-conditions.md](../../specs/construct-shared-conditions.md) for the design.

- **Names travel in the existing `sync` ping** (`Props`, `Preds`), only on the construct channel. The condition itself is evaluated on the owner, because the property readers are delegates that cannot be sent over IGC.
- **Local first.** A name registered locally is evaluated locally. Only a condition that no local block answers is sent to the scripts that advertise the name (for example `low` for a group of batteries when only the tank row is defined locally). The owner replies `1`, `0` or `?`, and the lowest script id that replies `1` or `0` decides. If every owner replies `?`, the condition is false.
- **`when` rules** keep their state (`Tripped`, `--for`, `--cooldown`, `--every`) on the script that holds the rule and use the last answer received, so a remote answer is up to one pass old. The rule is also evaluated again when a reply arrives, so it acts on an answer as soon as it comes in rather than on the next pass. An owner that stops answering for 3 syncs makes the rule false, with one log line. A rule on a name no script owns yet is kept, and starts working when the owner's sync arrives.
- **Inline `if` and `elseif`** pause the routine until the owner replies. `[general] condTimeout` (seconds, default 2) is how long they wait, then the step fails with `Command failed: <step> (no answer from script <id> for <name>)`. An `if` on a name no script owns fails at once. A local `if` returns in the same step with no request, and a skipped branch never sends one.
- **Batching.** Conditions due in the same cycle go to an owner in one request (up to 10, identical conditions sent once).
- **Construct only, read only.** The `cond` route only accepts requests from scripts on this construct and never runs a command or changes a block.

## Error Handling

- Invalid operator, unknown property/predicate or unresolved target: log once to the Mother console naming the rule, and disable only that rule. Do not throw. A name that no script on the construct defines keeps the rule pending instead (logged once, with `(no script on the construct has it)` when other scripts are present), and it starts working when an owner appears.
- Empty target list or no block exposing the property: evaluate as false, retry on cache rebuild.
- Duplicate or unknown `--option` on `when`: same as other commands (duplicate options already throw in `TerminalCommand`); the rule is disabled and logged.
- A rule with no steps after `when`: valid but warns once.
- `if` with an unknown predicate/property/target, or with a stateful option (`--for` etc.): the step fails like any failing command (`state.Failed`), prints the reason and stops the routine. An `if` that waits for another script and gets no answer within `condTimeout` fails the same way. An `if` with no matching `end` is closed implicitly; an `else`/`elseif`/`end` with no open `if` fails the step.
- Rule routine failure must not stop the evaluation loop.

## Phases

1. **MVP**: shared `Condition` with a handful of predicates and `< > == !=`; `when` rules (edge trigger, `else`, shared interval) and inline `if`/`else`/`end`; registry of ~10 properties, aggregates (`avg` default).
2. **Stability**: `--band`, `--for`, `--cooldown`, `--every`, `--at`, `<=`/`>=`, inline `elseif`.
3. **Scale**: staggered evaluation, per-pass read cache, `min/max/sum`.
4. **Extras**: `and`/`or` composition (`when` with several clauses, quoted targets only), variable thresholds, extension modules registering properties and predicates via `RegisterProperty(key, selector)` / `RegisterPredicate(...)`.

## Tests (MotherCore.Tests)

- Loader: rule with/without `else`, `when` options, predicate vs expression, `$VAR` target/value, `this`, quoted names containing `.`.
- Block event hooks (`onLock`, etc.) and `"Block".hookName` keys still load and fire unchanged; `[rules]` is read independently of `[hooks]`.
- Edge trigger fires once while the condition stays true.
- Inline `if`: true/false branch selection, `else` optional, nesting, implicit close at end of routine, `wait` inside a branch, `if` inside a config command and inside a rule branch, skipped steps do not run or resolve variables, stray `else`/`end` fails.
- `elseif` (phase 2): first true branch wins, later conditions not evaluated, `else` only when nothing matched, one `end;` closes the chain, `elseif` inside a skipped parent is never evaluated, `elseif` after `else` or without `if` fails.
- Rule budget: with a tiny budget, all rules still evaluate over successive ticks (cursor resumes), and a rule is never evaluated twice in one cycle.
- `if` and `when` evaluate the same condition text to the same result (shared `Evaluate`).
- Flapping input around a threshold: with `--band` and `--for` fires at most once.
- Cooldown honored; re-arm after false; `else` fires on true->false only.
- Unknown property/predicate/target disables only that rule.
- Group aggregates (avg/min/max/sum) fire once; mixed-type groups skip unsupported blocks.
- `[rules]` reload on Custom Data change and block removal rebuilds/removes rules, one rule per cycle; PB `[rules]` and block-local `[rules]` (`this`) both load, and another script's PB `[rules]` does not. A script's own Custom Data write (`var/set --save`) does not reload anything.

## Decisions

- Predicate vocabulary and thresholds: current table accepted for now, refine later.
- `else;` stays as the divider between then/else branches.
- Nesting: `if` blocks nest with `end;` (see Nesting). The `--then=`/`--else=` one-liner is dropped (cannot nest, `=` limit inside option values).
- `if` is supported in every section that executes commands (`[commands]`, `[menu:...]`, `[rules]` bodies, `[hooks]`).
- Rules live in a dedicated `[rules]` section; `[hooks]` stays for module-raised event hooks.
- System targets (bare words `ship`, `storage`, `var`, `fcs`, `plan`, `dock`) accepted.
- Group semantics: out of scope for this iteration (default `any`/`avg`/`sum` stays as written).
- Tier 2: `count`, `storage`, `var`, `fcs`, `plan`, `dock`. Tier 1 system targets are the `ship` ones; `altitude` is the committed flight-aware property.
- Defaults for interval and hysteresis: keep as written, adjust as needed.
- Rule budget: a per-pass instruction budget (round-robin cursor, ~10% default) plus boot-time lint, not a rule-count cap (see "Rule budget").
- `elseif`: added, in phase 2 (see `elseif`).

## Open Questions

None currently. Remaining work is validation by profiling (rule budget defaults) and by use (predicate thresholds, interval/hysteresis defaults).
