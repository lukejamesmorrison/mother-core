# CommandBus – Refactor Spec

> Date: June 7, 2026  
> Scope: `CommandBus`, `TerminalCommand`, `TerminalRoutine`, `HaltCommand`, `HelpCommand`

---

## Summary

Three test files cover this area:

| Test File | Tests |
|---|---|
| `CommandBusTests.cs` | 34 |
| `TerminalCommandTests.cs` | 27 |
| `TerminalRoutineTests.cs` | 31 |

Overall coverage is solid for the happy path and the most common player-facing input patterns. Several meaningful gaps exist — mostly around command resolution priority, newer features (`!!` force-local, `_` local prefix, `!` important config commands), and a few uncovered edge cases in parsing.

---

## Command & Routine Examples

This section contains examples of the various ways players define and execute commands. This documents focuses on enabling and properly testing these variations to ensure exceptions are handled without disrupting the player experience, and also ensuring overall complexity remains low due to game engine limitations.


### Command and Routine Overview

Commands are simple actions conducted by the system `piston/distance`, `light/color`. They are designed to be atomic and have a single purpose. Routines are sequences of commands, potentially with parallel groups that can be executed as a unit. Both commands and routines can include options (e.g. `--speed=1.5`) and targets (e.g. `@Piston1`). All player inputs should be interpreted first as routines, which are then unpacked into their constituent commands for execution. This allows for flexible input while maintaining a consistent execution model. Semicolons (`;`) are used to separate commands within a routine, and braces (`{ ... }`) denote parallel groups. More broadly speaking, we should consider anything wrapped in `{ ... }` as the formal definition of a routine. We can see traces of this in the flight plan parsing with Mother Autopilot System. Command terms are space-separated.

#### Player-defined commands

Players can define custom commands within the `[commands]` section of the programmable block's custom data. These commands act as quick accessors for custom routines.

Programmable Block > Custom Data
```ini
[commands]
; simple command with no options
openDoor=door/open AirlockDoor;
; sequentual routine with multiple commands
openDoors=door/open AirlockDoors; light/color AlertLights green;
; parallel routine with two groups (each group runs in its own coroutine)
openDoorsParallel={ door/open AirlockDoor1; } { door/open AirlockDoor2; }
; sequentual and parallel routine (AirlockDoor1 should open and the we execute the following two routines in parallel)
openDoorsMixed=door/open AirlockDoor1; { door/open AirlockDoor2; } { light/color AlertLights red; }
```

Players may also use multiple lines to improve readability. New lines are indicted with the pipe character (`|`):

```ini
[commands]
; sequentual routine with multiple commands across multiple lines
openDoors=
| door/open AirlockDoors; 
| light/color AlertLights green;
```

Players can also call custom commands from other custom commands, allowing for multi-step resolution:
```ini
[commands]
; sequential (though still very quick)
extendLandingGear=
| activateLandingGearMagPlates;
| ExtendlandingGearLegs;

; parallel (both commands execute at the same time)
extendLandingGear=
| { activateLandingGearMagPlates; }
| { ExtendlandingGearLegs; }

activateLandingGearMagPlates=block/on LandingGearMagPlates;
ExtendlandingGearLegs=piston/distance LandingGearPistons 2.5;
```

When a player wants to send a remote command, they must designate a target for the routine using the `@` symbol which looks up the grid by name in the Alamanc.
```ini
[commands]
; remote command to open the airlock on the mining ship
openMiningShipAirlock=@MiningShip door/open AirlockDoor;
```

There is also cases where we want to send multiple remote commands to different grids within the same command, so this should be supported as well.
```ini
[commands]
; set fleet into attack mode remotely by targeting the grids by name
fleetEngageEnemy=
| @FighterA mode/set attack;
| @FighterB mode/set attack;
```

#### Handling spaces in command arguments

To maximize flexibility for player input, command arguments should support spaces. However, spaces also act as separators between command terms, so we need a way to distinguish between spaces that separate terms and spaces that are part of an argument. To solve this, we allow players to wrap arguments in double quotes. This allows for spaces within arguments without breaking the parsing logic.

```ini
[commands]
; open a door with a name that includes spaces
openDoor=door/open "Airlock Door";
; set a light color with a name that includes spaces, and an argument that includes spaces
greeting=screen/print "Airlock Display" "Welcome to the Airlock, Commander Shepard!";
; send a remote command to a grid with spaces in its name
openMiningShipAirlock=@"Mining Ship" door/open "Airlock Door";
```

#### Special Characters

We should support as many special characters as possible in command arguments, as players may have blocks with special characters in their names, or may want to use special characters for readability. For now we should at least support newline for using in commands like `screen/print`
```ini
[commands]
; print ascii art with newlines
printLogo=
| screen/print "Main Display" "══════════════════════════════════════════════════════
|\ndMMMb  dMP dMP dMP .aMMMb  dMMMMb  dMMMMb  .dMMMb
|\nP' VP dMP dMP dMP dMP'dMP dMP.dMP dMP VMP dMP' VP 
|\nMMb  dMP dMP dMP dMP dMP dMMMMK' dMP dMP  VMMMb    
|\ndMP dMP.dMP.dMP dMP.aMP dMP'AMF dMP.aMP dP .dMP   
|\nP'  VMMMPVMMP'  VMMMP' dMP dMP dMMMMP'  VMMMP'    
|\n══════════════════════════════════════════════════════
|\ndMMMb  dMP dMP dMMMMMP dMP .dMMMb  dMP dMP dMMMMM    
|\nP' VP dMP dMP dMP     dMP dMP' VP dMP dMP dMP    
|\nMMb  dMMMMMP dMMMMP  dMP  VMMMb  dMMMMMP dMMMP   d
|\ndMP dMP dMP dMP     dMP dP .dMP dMP dMP dMP     dM
|\nP' dMP dMP dMP     dMP  VMMMP' dMP dMP dMP     dMP
|\n══════════════════════════════════════════════════════
|\n" --color=255,255,225;
| screen/bgcolor "Main Display" 75,0,0;
```

#### Delaying execution with `wait` command

To give players access to delayed execution, we have a `wait` command that can be used within routines. This is a core gameplay mechanic that allows for timing control within sequences of commands.
```ini
[commands]
; wait for 2 seconds before executing the next command
openAirlock=wait 2; door/open AirlockDoors;
```

This command should remain versitile and always force all subsequent commands to wait before executing. We must however pay special attention to scope so that the wait command is always executed in the same coroutine as the commands it is meant to delay, otherwise we risk the wait having no effect on the timing of subsequent commands.

```ini
[commands]
; wait 2 seconds, open doors
openAirlock=door/open AirlockInnerDoors; wait 2; door/open AirlockInnerDoors;
; open outer doors immediately, but wait 2 seconds to open the inner doors
openAirlock=
| { wait 2; door/open AirlockDoorsInner; } 
| { door/open AirlockDoorsOuter; }
```

#### Flight Plans

The most complex manifestation of routines and commands are in flight plan definitions. Flight plans are strings, but within them they may define commands for execution at waypoints in the flight plan. This means that we should recognize entire flight plans as strings, but be able to resolve and execute routines from within them later. We also need to consider how spaced string are resolved using double quotes within the flightplan string which also uses double quotes.

Here is an example flight plan where we fly to a waypoint, then execute a routine at that waypoint, then fly to another waypoint and execute another routine:
```ini
[commands]
; set the current flight plan with two waypoints, each with a routine to be executed when the grid reaches the waypoint's position.
testFlightPlan=
| fp/set 
| "
|	GPS:FirstWaypoint:190.12:-54.45:45.89:#FF75C9F1: 
|	{ light/color Light1 green; }	
|
|	GPS:SecondWaypoint:211.78:-52.93:59.19:#FF75C9F1:
|	{ light/color "Light 2" green; }	
| "
````

### using Variables

#### Global Variables

Players can defined variables in the programmable block's custom data. Variables are interted into player defined commands during boot. This allows players to improve code reuse.  variables are stored and set for the current lifecycle only. If a player wishes to save a variable across lifecycles, they can use the `--save` option for the `var/set` command.

```bash
var/set MAX_SPEED 75 --save;
```

We access variables within custom commands with the `$` symbol:

```ini
[variables]
MAX_SPEED=75

[commands]
; use the MAX_SPEED variable in a command
setMaxSpeed=thruster/thrust MainThrusters $MAX_SPEED;
```

#### Inline variables
Players may also allow variables as inputs to their custom defined commands by enclosing with double braces `{{ }}`. A default value is provided to the right of the `:` colon. Values for these variables are passed in as command `--options`.

```ini
[commands]
; use an inline variable for speed with a default of 50
setSpeed=thruster/thrust MainThrusters {{speed:50}};
```
The the player would run it:

```bash
# run with speed of 75
setSpeed --speed=75;
```

IF we want to get really crazy, we can also set the default to be the global varaible:

```ini
[variables]
MAX_SPEED=85
[commands]
; use an inline variable for speed with a default of the global variable MAX_SPEED
setSpeed=thruster/thrust MainThrusters {{speed:$MAX_SPEED}};
```
And run it with or without an option:
```bash
# run with speed of 75
setSpeed --speed=75;
# run with default speed of 85 from the global variable
setSpeed;
```


### Future State of Commands and Routines

I would like to enable the following capabilities in a future iteration of the logic discussed in this document.

1. Logical statements. I see this comming in two general shapes:
    1. IF/ELSE - for situations where players want to define switches and trigger automations when a block's property changes
    1. GT/LT/EQ - for situations where players want to trigger automations when a block's property reaches a certain value (for example a gas tank reaches 75% full, batteries are below 10% charge). The challenge here is not overloading the command bus when a value teeters above and below the target threshold.

2. Real-time variable substitution. Currently variables are substituted during boot so players are unable to dynamically set variables during runtime to assist with logic flow. We should find a way to effeciently set variables with `var/set` mid-execution so that players can create simple switches themselves.
 
## TerminalCommand

### What is covered

- Basic parsing: name, positional arguments, `--key=value` and `--flag` options
- Quoted arguments (single and multiple), including mixed with options
- Whitespace: leading/trailing trim, carriage return stripping, multiple spaces between terms
- `GetOption` including a missing-key fallback
- `GetBoolFromString`: `"true"` (case-insensitive), `"1"`, falsy values, `null`, whitespace
- Multiple positional arguments (`piston/distance Piston1 1.5 3.0`)
- Options-only commands
- Flight plan strings (with internal `{...}` and `;`) as a single quoted argument
- `!!` force-local prefix: sets `IsForceLocal = true` and strips the prefix from `Name`

### Gaps

| # | Gap | Source | Priority | Status |
|---|---|---|---|---|
| T2 | **Unclosed quote** — `SplitInputIntoTerms` handles this gracefully (`end = input.Length`), but there is no test asserting the fallback behaviour (e.g. `new TerminalCommand("print \"unclosed")`). | Prior review | Low | **Done** |
| T3 | **Single `!` prefix in a command name** — a player typing `!halt` gets a `Name` of `"!halt"`. This is significant because `ResolveConfigCommand` explicitly checks for the `!` prefix in `ConfigCommands`. No test covers what `TerminalCommand` produces for a `!`-prefixed name. | Prior review | Medium | **Done** |
| T4 | **Empty command string** — `new TerminalCommand("")` throws `ArgumentOutOfRangeException` on `Arguments[0]` since `SplitInputIntoTerms` returns an empty list. Current (broken) behaviour is pinned by `Empty_Command_String_Throws_ArgumentOutOfRange`. The guard fix is in Phase 2. | Prior review | Medium | **Pinned** |
| T6 | **Nested double quotes in flight plan arguments** — A flight plan passed as a quoted argument (`fp/set "... { light/color "Light 2" green; } "`) will be split at the first inner `"` by `SplitInputIntoTerms` because it uses `IndexOf('"', i+1)` to find the closing quote. The flight plan argument would be truncated at `... { light/color `. | Examples | High | Open |

---

## TerminalRoutine

### What is covered

- Sequential multi-command parsing, command order, trailing-semicolon trimming
- Default target `"self"`, `@Name` remote target, `@*` broadcast — parsing and target-stripping
- `Unpack()` with nested named routines; fluent return
- Parallel groups (`{ ... } { ... }`): detection, group count, command count, order
- Parallel groups with `wait` commands; empty group skipped
- Mixed content outside braces — asserted as NOT treated as parallel (⚠ this conflicts with examples, see R6)
- Whitespace handling (trim, extra spaces between semicolons)
- Semicolons inside quoted strings ignored as separators
- `wait` command parsed as a normal command in the command list
- Quoted flight plan not split by internal semicolons, does not trigger parallel-group detection

### Gaps

| # | Gap | Source | Priority | Status |
|---|---|---|---|---|
| R2 | **`Unpack()` on a routine that has parallel groups** — `Unpack()` only iterates `Commands`, which is empty when the routine was parsed as parallel groups. The result is an empty `UnpackedRoutineString`. Current (broken) behaviour pinned by `Unpack_On_Parallel_Group_Routine_Produces_Empty_String`. The fix is in Phase 3. | Prior review | High | **Pinned** |
| R3 | **`Unpack()` with a command not in the lookup** — a command that is not a named alias should pass through unchanged. Not explicitly tested; it is only tested with commands that are in the lookup. | Prior review | Low | **Done** |
| R4 | **Quoted content with semicolons inside parallel groups** — e.g. `{ screen/print "a;b"; }`. The parser tracks `braceDepth` and `insideQuotes` but no test exercises their interaction inside a group. | Prior review | Low | **Done** |
| R5 | **Remote target with spaces in grid name** — The agreed convention for multi-word grid names is to wrap the entire target token in quotes with `@` inside: `"@Mining Ship" cmd`. `SetTarget` currently splits by the first space before checking for `@`, so `"@Mining` becomes the first term and the `@` check fails. The fix is small and isolated: make `SetTarget` quote-aware so that if the routine string starts with `"`, it scans to the closing `"`, unquotes the token, then checks for the `@` prefix and strips it to set `Target`. | Examples | High | Open |
| R6 | **Mixed sequential + parallel routing is a broken documented use case** — `door/open AirlockDoor1; { door/open AirlockDoor2; } { light/color AlertLights red; }` — `ContainsParallelGroups` returns `false` (content exists outside braces), so `SplitRoutineCommands` is used. The `{ ... }` blocks get lumped into a single malformed command token whose `Name` is `{`. The existing test `Mixed_Content_Outside_Braces_Is_Not_Treated_As_Parallel` **asserts the broken behaviour** and will need to be replaced when this is fixed. | Examples | High | Open |
| R7 | **Multiple remote targets in a single routine** — `@FighterA mode/set attack; @FighterB mode/set attack;` — `SetTarget` reads only the first term of the whole routine string and strips it. The second command `@FighterB mode/set attack` is never re-inspected for its own `@` prefix; `@FighterB` becomes the command name. **Broken documented use case.** | Examples | High | Open |

---

## Configuration

### What is covered

- Loading variables from `[variables]`, substituting into commands at runtime
- `$VAR` substitution, longest-name-first ordering, stripping `$` prefix from variable names
- Stripping double quotes from variable values and command values
- `{{param}}` and `{{param:default}}` placeholder substitution
- `{{param:$VAR}}` — global variable as default for an inline parameter
- `var/set` in-memory update, with and without `--save`
- `CollapseWhitespaceOutsideQuotes` — reduces multiple spaces while preserving quoted content

### Gaps

| # | Gap | Source | Priority | Status |
|---|---|---|---|---|
| CF1 | **Multi-line pipe notation** — `MyIni` strips `\|` continuation markers and joins lines automatically before the value is ever read by `RegisterCommands`. The pipeline therefore requires no custom test: `MyIni` owns this behaviour and any regression would be a library-level failure, not a Mother-level one. | Examples | Medium | **Not Required** |
| CF3 | **Flight plan multi-line loading** — same rationale as CF1: `MyIni` reassembles the pipe-continued lines into a single string before `RegisterCommands` or `TerminalRoutine` ever sees them. No end-to-end test is needed at this layer. | Examples | Medium | **Not Required** |

---

## CommandBus

### What is covered

- Construction and boot
- `RegisterCommand`, `RunTerminalCommand` (basic), empty-string returns false
- Semicolon-separated commands via `RunTerminalCommand`
- Boot registers `help` and `halt`; boot clears `ConstructCommands` and `ImportantConstructCommands`
- Variable substitution (single and multiple `$VAR` tokens)
- `GetSelfCommandNames`: module commands, config commands, both combined, empty
- `RegisterRemoteCommands`: normal storage, self-id ignored, overwrites existing entry
- Separation of `!`-prefixed important commands into `ImportantConstructCommands`
- `FindInstanceWithCommand` / `FindInstanceWithImportantCommand`: found, not found, cross-script
- `FindInstanceWithImportantCommand` does not match normal (non-important) commands
- Config command expansion (single step and multi-step)
- Coroutine scheduling: single command → one coroutine; semicolons → one coroutine sequential; parallel groups → one coroutine per group

### Gaps

| # | Gap | Source | Priority | Status |
|---|---|---|---|---|
| C1 | **`!!` force-local execution path** — `RunTerminalCommand("!!help")` should bypass any important construct command with the same name and execute locally. The parsing is tested in `TerminalCommandTests`, but the `ExecutePrimitiveCommand` and `ResolveConfigCommand` branches that honour `IsForceLocal` are never exercised from `CommandBus`. | Prior review | High | **Done** |
| C2 | **`_` underscore local-command prefix** — `RunTerminalCommand("_myAction")` should resolve to `ConfigCommands["myAction"]` and execute locally, even if an important construct command with that name is registered. This path is entirely untested. | Prior review | High | **Done** |
| C3 | **`!` important config command resolution** — `ConfigCommands["!dock"] = "help"` should be resolved when `RunTerminalCommand("dock")` is called and no construct instance owns it. The lookup `"!" + command.Name` branch in `ResolveConfigCommand` has no test. | Prior review | High | **Done** |
| C4 | **`GetSelfCommandNames` with important (`!`-prefixed) config commands** — `ConfigCommands["!dock"]` — the key is included in names via the `foreach` loop. No test asserts that `!dock` is present in the returned list, which matters for `RegisterRemoteCommands` consumers. | Prior review | Medium | **Done** |
| C5 | **`wait` command in a coroutine** — a routine like `"track; wait 2; track"` should yield between the two `track` executions. No test verifies that the wait yields the correct duration and that subsequent commands don't execute on the same tick. | Prior review | High | **Done** |
| C6 | **Config command expanding to parallel groups** — `ConfigCommands["par"] = "{ track; } { track; }"` — when `RunTerminalCommand("par")` is called, the expanded routine should launch multiple coroutines. Not tested. | Prior review | Medium | **Done** |
| C7 | **`CommandNotFound` message** — when an unrecognized command is run, `Messages.CommandNotFound` is passed to `Mother.Print`. No test asserts the correct message is printed or that the bus doesn't throw. | Prior review | Low | **Done** |
| C8 | **`HaltCommand` execution** — `halt` is registered on boot but `Execute` (which calls `Clock.Halt()`) is never directly tested. | Prior review | Medium | **Done** |
| C9 | **`HelpCommand` output** — `help` is registered on boot but the string it returns (listing all command names) is never asserted. Also revealed that `HelpCommand` was iterating `Module.Commands` (the `BaseModule` list, always empty for `CommandBus`) instead of `Module.ModuleCommands`; the bug was fixed as part of writing the test. | Prior review | Low | **Done** |
| C10 | **`RegisterRemoteCommands` with an empty list** — should result in an empty set stored for the remote id, not an exception. | Prior review | Low | **Done** |
| C11 | **`RunTerminalCommand` with only whitespace** — e.g. `"   "` — `commandString.Length > 0` would be true, so it would attempt to create a `TerminalRoutine`, which in turn creates a `TerminalCommand` from whitespace-trimmed empty string, throwing on `Arguments[0]`. Guard changed to `!string.IsNullOrWhiteSpace`. | Prior review | Medium | **Done** |
| C12 | **`wait` scope is not tested with parallel groups** — `{ wait 2; door/open A; } { door/open B; }` — the `wait` in the first group should only block commands within that coroutine. If the wait mechanism bleeds across parallel coroutines, both groups would stall, silently breaking the player's intended concurrency. | Examples | High | **Done** |
| C13 | **Fleet-targeting (multi-target) routing is untested** — a config command like `@FighterA mode/set attack; @FighterB mode/set attack;` should route each semicolon-separated command to its own target. Because `SetTarget` only captures the first `@` prefix of the whole routine string, the second `@FighterB` token becomes a command name rather than a routing directive. No tests cover multi-target dispatch or the expected failure mode. | Examples | High | Open |

---

## Proposed Test Suite

The following tests are recommended to close the gaps above. They are grouped by the file they belong in and ordered by the phase in which they should be implemented (see Migration Plan).

### Phase 1 — `TerminalCommandTests.cs`

```
[T4] Empty_Command_String_Throws_ArgumentOutOfRange   -- ✅ pins current broken behaviour (guard in Phase 2)
[T3] Single_Bang_Prefix_Is_Preserved_In_Name          -- ✅ new TerminalCommand("!halt").Name == "!halt"
[T2] Unclosed_Quote_Falls_Back_To_End_Of_String        -- ✅ argument is captured to end of input
```

### Phase 1 — `TerminalRoutineTests.cs`

```
[R3] Unpack_Passes_Through_Commands_Not_In_Lookup                        -- ✅ command not in lookup survives Unpack() unchanged
[R4] Semicolon_In_Quoted_String_Inside_Parallel_Group_Is_Not_A_Separator -- ✅ { screen/print "a;b"; } → 1 command, argument "a;b"
[R2] Unpack_On_Parallel_Group_Routine_Produces_Empty_String              -- ✅ pins current broken behaviour (fix in Phase 3)
```

### Phase 1 — `CommandBusTests.cs`

```
[C1]  Force_Local_Bypasses_Important_Construct_Command              ✅
[C2]  Underscore_Prefix_Resolves_Local_Config_Command               ✅
[C3]  Important_Config_Command_Is_Resolved_When_No_Construct_Owner              ✅
[C4]  GetSelfCommandNames_Includes_Important_Config_Command_With_Bang_Prefix   ✅
[C5]  Wait_Blocks_Subsequent_Commands_In_Same_Coroutine                        ✅
[C6]  Config_Command_Expanding_To_Parallel_Groups_Launches_Multiple_Coroutines ✅
[C7]  Unknown_Command_Prints_CommandNotFound_And_Does_Not_Throw   ✅
[C8]  Halt_Command_Clears_All_Coroutines                           ✅
[C9]  Help_Command_Output_Lists_All_Registered_Commands        ✅
[C10] RegisterRemoteCommands_With_Empty_List_Stores_Empty_Set  ✅
[C11] RunTerminalCommand_With_Only_Whitespace_Returns_False        ✅
[C12] Wait_In_Parallel_Group_Does_Not_Block_Other_Parallel_Group   ✅
```

### Phase 1 — `ConfigurationTests.cs`

> **CF1 and CF3 removed.** `MyIni` reassembles pipe-continued lines into a single string before `RegisterCommands` ever reads the value. The multi-line → single-string transformation is a `MyIni` library concern, not a Mother concern, so no end-to-end tests are required at this layer.

### Phase 2 — `TerminalCommandTests.cs`

```
[T6] Nested_Quote_In_Flight_Plan_Argument_Parses_Correctly
     -- fp/set "GPS:A:1:2:3:: { light/color \"Light 2\" red; }" → argument preserved fully
```

### Phase 2 — `TerminalRoutineTests.cs`

```
[R5] Remote_Target_With_Quoted_Name_Parses_Correctly
     -- new TerminalRoutine("\"@Mining Ship\" cmd").Target == "Mining Ship"
```

### Phase 3 — `TerminalRoutineTests.cs`

```
[R6] Mixed_Sequential_And_Parallel_Parses_Into_Sequential_Then_Parallel
     -- "cmd1; { cmd2; } { cmd3; }" → Commands[0].Name == "cmd1" AND ParallelGroups contains 2 groups
     (NOTE: replaces the existing Mixed_Content_Outside_Braces_Is_Not_Treated_As_Parallel test)
[R7] Multiple_Remote_Targets_In_Single_Routine_Route_Independently
     -- "@A cmd1; @B cmd2;" → two separate remote dispatch calls, each to its correct target
```

### Phase 3 — `CommandBusTests.cs`

```
[C13] Multi_Target_Routine_Dispatches_Each_Command_To_Its_Own_Target
```


---

## Migration Plan

### Phase 1 — Tests only, zero code changes (low risk)

All tests that exercise **existing behaviour** can be added first. This pins the current state, makes regressions visible immediately, and is fully safe.

- Add all Phase 1 tests from the proposed suite above.
- For R2 specifically, write a test that **documents the current (broken) output** rather than the desired output. This creates a pinned baseline that will automatically flag the change when Phase 3 arrives.

**Estimated effort:** Low. No source changes.  
**Risk:** None.

---

### Phase 2 — Isolated, low-blast-radius fixes (low–medium risk)

Each item in this phase touches a single, well-scoped method with no downstream callers that need structural changes.

| Item | Change required |
|---|---|
| T4 / C11 | Guard `TerminalCommand` constructor and/or `RunTerminalCommand` against empty/whitespace input. `RunTerminalCommand` guard updated to `!string.IsNullOrWhiteSpace` (**Done**); `TerminalCommand` constructor guard deferred (T4 pins broken behaviour). |
| R5 | Fix `SetTarget` to be quote-aware. If the routine string starts with `"`, scan to the closing `"`, unquote the token, then apply the existing `@` prefix check. This is a change to a single method with no structural side effects. |
| T6 | Fix `SplitInputIntoTerms` for nested quotes. The simplest approach is to define an escape character (`\"` within a quoted string), or to use a different delimiter for the outer flight plan wrapper. Coordinate with the flight plan format specification before implementing. |

**Estimated effort:** Medium.  
**Risk:** Low for T4/C11. Medium for R5 (`SetTarget` is called on every routine construction) and T6 (`SplitInputIntoTerms` is called from both `TerminalCommand` and `DisplayTypeResolver`). Run the full test suite after each change.

---

### Phase 3 — Structural changes, high regression potential (high risk)

These items require changes to the core parsing pipeline. They should each be done in isolation on a feature branch with the full test suite green before merging.

#### 3a. Mixed sequential + parallel (R6)

**What needs to change:** `ContainsParallelGroups` currently returns `false` if any content exists outside `{ }` blocks. It needs to be extended to a three-state result: *all-sequential*, *all-parallel*, or *mixed*. `ParseRoutineString` then needs a third branch that:
1. Splits the input by `;` (using `SplitRoutineCommands`) to get sequential tokens.
2. For each token, checks whether it is a pure `{ ... }` block.
3. Pure `{ ... }` tokens are collected into a parallel block and launched together.
4. Non-brace tokens are executed sequentially before the parallel block.

The existing test `Mixed_Content_Outside_Braces_Is_Not_Treated_As_Parallel` **must be deleted or replaced** by the new R6 test.

**Risk:** High. This is the most-used parsing path. Every sequential and parallel test in `TerminalRoutineTests` and `CommandBusTests` exercises this logic. A bug here silently produces wrong command ordering or dropped commands.

#### 3b. Multiple remote targets per routine (R7 / C13)

**What needs to change:** `SetTarget` currently sets one target for the entire routine. For multi-target support, routing needs to move to per-command granularity:
1. After `SplitRoutineCommands`, inspect each command string for a leading `@Token` prefix.
2. If found, strip it and record it as the per-command target.
3. In `HandleRoutine` / `LaunchRoutineCoroutines`, dispatch each command individually to its target rather than dispatching the whole routine to one target.

This intersects with `ExecuteCommandGroupCoroutine`, which currently runs commands sequentially in a single list. Introducing per-command targets means the coroutine must decide at each step whether to dispatch locally or remotely.

**Risk:** High. The current `HandleRoutine` → `IMS.SendRequestFromRoutine` path batches an entire routine for remote dispatch as a unit. Decomposing this into per-command dispatch changes the IMS communication pattern and could break multi-step remote routines that rely on sequential ordering guarantees from the current batching.

---

## Risk Areas — Most Likely to Break

The following are the parts of the system that are most likely to produce silent failures if Phase 3 changes are not managed carefully. Changes here should be treated as high-risk and require full test coverage before and after.

### 1. `SplitRoutineCommands` ⚠ Highest risk

Every sequential routine goes through this method. The brace-depth tracking interacts with quote tracking to preserve `{ ... }` blocks and quoted strings as atomic tokens. The mixed-mode change (Phase 3a) requires this method to change its splitting semantics for `{ ... }` tokens. Any regression here affects **all** routine parsing — sequential, parallel, config commands, and flight plans.

**Concrete failure mode:** If `{ cmd; }` is accidentally split at its interior `;`, it produces a command with name `{` followed by a command with name `}`, both of which fail silently with `CommandNotFound`.

### 2. `ContainsParallelGroups` ⚠ High risk

This is the binary classifier that decides which parsing branch is taken. It currently fails on any content outside braces. If the Phase 3a fix changes it to return true for mixed input, all existing sequential routines that happen to contain `{` within a quoted string argument could be misclassified.

**Concrete failure mode:** `nav/set-flight-plan "GPS:A: { cmd; }"` — the `{` is inside a quoted argument, but `ContainsParallelGroups` uses character-level scanning and may not be quote-aware. The existing flight plan tests currently pass because the current logic returns `false` for this input. After the Phase 3a change, verify that all flight plan tests still pass.

### 3. `SetTarget` ⚠ High risk

`SetTarget` is called once per routine construction, before any parsing. It mutates `RoutineString` by stripping the target prefix. If the Phase 2 fix for R5 (quoted target names) or the Phase 3b fix for R7 (per-command targets) changes how `RoutineString` is modified, the downstream `SplitRoutineCommands` and `ParseParallelGroups` receive a different string than expected.

**Concrete failure mode:** If `SetTarget` for `@"Mining Ship" cmd1; cmd2` incorrectly strips one too many or too few characters, `RoutineString` becomes `"cmd1; cmd2` (with a leading quote), which causes `SplitInputIntoTerms` to treat `cmd1; cmd2` as a single quoted argument.

### 4. `ExecuteCommandGroupCoroutine` / `ExecuteCommandCoroutine` ⚠ High risk

This coroutine is the heart of command sequencing and wait timing. The `yield return` values control when the next command in a sequence runs. Any change to how wait values are yielded, or how config commands are inlined, can silently change the timing of all automation routines. This is difficult to catch in tests because the Clock's tick granularity means off-by-one errors in yield counts may not be caught unless tests assert at each individual tick.

**Concrete failure mode:** If a `wait` command inside a config command that is expanded inline does not yield correctly, subsequent commands execute on the wrong tick. Player automations that depend on precise timing (airlock sequences, landing gear sequences) break silently.

### 5. The existing `Mixed_Content_Outside_Braces_Is_Not_Treated_As_Parallel` test ⚠ Medium risk

This test **asserts broken behaviour** for the mixed sequential+parallel use case. When Phase 3a is implemented, this test will start failing. This is expected and intentional — but if it is not updated at the same time as the parser change, CI will fail and the change may be incorrectly reverted. The test must be deleted and replaced with the R6 test in the same commit as the parser change.

---

## Test Harness Recommendations

The following improvements to the test infrastructure in `tests/` would meaningfully reduce boilerplate, improve readability, and make the gaps identified above practical to test. They are ordered by the number of existing and proposed tests they would simplify.

---

### H1. `CustomDataBuilder` — eliminates repeated INI string construction ✅ Done

**Problem:** Every `ConfigurationTests` test builds CustomData from scratch with `string.Join("\n", "[variables]", "KEY=VALUE", "", "[commands]", ...)`. Missing a blank line between sections causes `MyIni` parse failures that produce silent wrong results rather than test errors. The pattern is copied verbatim across 20+ tests.

**Recommendation:** Add a `CustomDataBuilder` to `TestUtilities/` that produces a valid INI string and makes the intent clear at a glance.

```csharp
// TestUtilities/CustomDataBuilder.cs
public class CustomDataBuilder
{
    public CustomDataBuilder WithVariable(string name, string value) { ... }
    public CustomDataBuilder WithCommand(string name, string value) { ... }
    public string Build() { ... } // emits correct [variables] / [commands] sections
}
```

Usage in tests:
```csharp
_mother.ProgrammableBlock.CustomData = new CustomDataBuilder()
    .WithVariable("SPEED", "75")
    .WithCommand("openDoor", "door/open AirlockDoor")
    .Build();
```

This directly benefits all variable/parameter substitution tests in the proposed Phase 1–3 suite.

---

### H2. `ExecutionTracker` moved to `TestUtilities/` — shared across test files ✅ Done

**Problem:** `ExecutionTracker` (the `IModuleCommand` that counts `Execute` calls) is currently a private nested class inside `CommandBusTests`. Any other test file that needs to count command invocations — coroutine ordering, wait timing, config command expansion — must either duplicate it or leave the behaviour untested.

**Recommendation:** Move `ExecutionTracker` to `TestUtilities/TrackingCommand.cs` as a public class, and optionally add a variant that records the order of calls:

```csharp
// TestUtilities/TrackingCommand.cs
public class TrackingCommand : BaseModuleCommand
{
    public string CommandName;
    public override string Name => CommandName;
    public int ExecutionCount { get; private set; }
    public List<int> ExecutionOrder { get; } = new List<int>(); // call index

    public TrackingCommand(string name = "track") { CommandName = name; }

    public override string Execute(TerminalCommand command)
    {
        ExecutionCount++;
        return "";
    }
}
```

The `ExecutionOrder` variant is particularly useful for verifying that sequential commands after a `wait` do not execute early (C5, C12).

---

### H3. `ClockDriver` — structured tick control for coroutine tests ✅ Done

**Problem:** Tests that verify coroutine sequencing manually call `clock.Run()` one line at a time and track tick counts in comments. This is fragile: adding a new `yield return 0` inside a coroutine shifts every subsequent assertion by one tick, causing all downstream assertions to silently pass with stale counts.

**Recommendation:** Add a `ClockDriver` helper to `TestUtilities/` that makes tick boundaries explicit:

```csharp
// TestUtilities/ClockDriver.cs
public class ClockDriver
{
    readonly Clock _clock;
    public ClockDriver(Clock clock) { _clock = clock; }

    /// <summary>Advances the clock by exactly n ticks.</summary>
    public ClockDriver Tick(int n = 1) { for (int i = 0; i < n; i++) _clock.Run(); return this; }

    /// <summary>Advances the clock until no coroutines remain or the limit is reached.</summary>
    public ClockDriver RunToIdle(int maxTicks = 100)
    {
        int t = 0;
        while (_clock.CoroutineCount > 0 && t++ < maxTicks)
            _clock.Run();
        return this;
    }
}
```

Usage:
```csharp
var driver = new ClockDriver(_mother.GetModule<Clock>());
commandBus.RunTerminalCommand("track; wait 2; track");

driver.Tick();                             // tick 1: first "track" runs
Assert.That(tracker.ExecutionCount, Is.EqualTo(1));

driver.Tick();                             // tick 2: still waiting
Assert.That(tracker.ExecutionCount, Is.EqualTo(1));

driver.RunToIdle();                        // remaining ticks: second "track" runs
Assert.That(tracker.ExecutionCount, Is.EqualTo(2));
```

This directly enables C5 and C12 without fragile manual tick counting.

---

### H4. `PrintCapture` — assert what Mother prints without `Terminal` wiring ✅ Done

**Problem:** Several gaps (C7 `CommandNotFound`, C9 `HelpCommand` output) require asserting the string passed to `Mother.Print`. The `Terminal` module is not booted in most module-level tests, so `Mother.Print` falls back to `Program.Echo`. There is no way to capture that output in the current setup without replacing the echo delegate, which requires rebuilding the program.

**Implementation:** `PrintCapture` in `TestUtilities/PrintCapture.cs` accepts a `TestSession` and redirects its underlying `Program.Echo` delegate to an in-memory list. It exposes `Lines`, `Contains(fragment)`, and `Clear()`.

Usage:
```csharp
var session = new TestSession().Boot();
var capture = new PrintCapture(session);

session.Bus.RunTerminalCommand("nonexistent");
session.Clock.RunToIdle();

Assert.That(capture.Contains("Command not found"), Is.True);
```

This unblocks C7, C9 and any future test that needs to assert on user-visible output.

---

### H5. `RemoteScriptRegistrar` — Superseded by `MockIGCNetwork` ✅ Done

**Update:** The original proposal was a simple ID-management helper. The implemented solution goes further: `MockIGCNetwork` in `TestUtilities/MockIGCNetwork.cs` is a full in-process IGC transport that connects multiple `TestSession` instances. It replaces both `RemoteScriptRegistrar` and the manual `_mother.Id + 1` ID arithmetic.

The grid name is passed to the `TestSession` constructor; `OnNetwork()` takes only the network. Almanac cross-registration between all sessions on the same network is automatic — no manual wiring required.

```csharp
var network = new MockIGCNetwork();

var shipA = new TestSession("ShipA")
    .OnNetwork(network)
    .WithCustomData(new CustomDataBuilder()
        .WithCommand("attack", "@ShipB weapons/fire")
        .Build())
    .Boot();

var shipB = new TestSession("ShipB").OnNetwork(network).Boot();

// ShipA already knows "ShipB" and vice versa — no manual Almanac wiring needed.
shipA.Bus.RunTerminalCommand("attack");
shipA.Clock.RunToIdle();

// Assert message was queued without full delivery
Assert.That(network.SentMessages.Any(m => m.TargetId == shipB.IGC.Me), Is.True);

// Or deliver to the recipient and assert execution
network.Deliver();
shipB.Clock.RunToIdle();
```

**Key types:**
- `MockIGCNetwork` — the network hub; `AllocateEndpoint()`, `Deliver()`, `ClearSentMessages()`
- `MockIGC` — `IMyIntergridCommunicationSystem` implementation; routes through the network
- `MockUnicastListener` / `MockBroadcastListener` — `IMyUnicastListener` / `IMyBroadcastListener` implementations with message queues
- `SentMessage` — capture record for asserting outbound traffic without full delivery

Sessions joined via `OnNetwork()` expose `session.NetworkIGC` typed as `MockIGC` for direct queue inspection.

---

### H6. `BootedCommandBus` factory method in `BaseModuleTests` — Superseded by `TestSession`

**Update:** This helper is no longer needed. `TestSession` (H8) covers the same use case with a cleaner API and also handles booting all core and extension modules, clock reset, and the `ClockDriver` wrapper. Tests that previously used the three-line `new CommandBus / Boot / Reset` pattern should use `new TestSession().Boot()` instead.

---

### H7. Complete `ProgrammableBlockFactory` ✅ Done

**Problem:** `Factories/ProgrammableBlockFactory.cs` existed but was entirely commented out. Tests that need a PB with specific properties (a particular `CustomName`, `EntityId`, or `CustomData`) had no factory to call — they either used the default fake from `ProgramBuilder` or set properties inline, which is fragile when the fake's interface changes.

**Implementation:** `ProgrammableBlockFactory` in `Factories/ProgrammableBlockFactory.cs` is now complete. It creates a `FakeItEasy` fake with mutable `CustomData` (supports get/set), a configurable `CustomName` (defaults to `"Mother Core PB"`), and a random `EntityId` when none is supplied. This factory is used directly by R7/C13 (multi-script scenarios where each PB needs a distinct identity).

---

### H8. `TestSession` — full boot cycle orchestrator ✅ Done

**Problem:** Tests that exercise the full `CustomData → Configuration.Boot → CommandBus.Boot → RunTerminalCommand` pipeline had no clean way to express the complete setup. Each test assembled the same three-to-five line boot sequence manually, with no consistent clock reset discipline.

**Implementation:** `TestSession` in `TestUtilities/TestSession.cs` is a builder-before-boot, context-holder-after-boot. It creates its own `Program` and `Mother`, boots every registered core and extension module in order, then applies the grid name. The minimal case is one line:

```csharp
var s = new TestSession().Boot();
```

Layer in only what each test needs:

```csharp
var s = new TestSession()
    .WithCustomData(new CustomDataBuilder()
        .WithCommand("openDoor", "door/open AirlockDoor")
        .Build())
    .WithCommands(tracker)
    .Boot();

s.Bus.RunTerminalCommand("openDoor");
s.Clock.Tick();
s.Clock.RunToIdle();
```

Post-boot properties: `s.Bus` (`CommandBus`), `s.Clock` (`ClockDriver`), `s.Config` (`Configuration`), `s.Mother` (`Mother`).

`ClockDriver` (H3) is built directly into the session — `s.Clock` is ready to use immediately after `Boot()`.

An optional grid name passed to the constructor sets `Mother.Name` after all modules boot (so `Configuration.Boot()` cannot overwrite it). If omitted, the name derived by `Configuration` is kept; if that is also empty, it falls back to `"grid-{Mother.Id}"`.

**Extension for MotherOS / MotherGUI:** Subclass `TestSession` and override `OnBeforeBoot` to register extension modules before the boot loop runs:

```csharp
public class MotherOSTestSession : TestSession
{
    public MotherOSTestSession(string gridName = null) : base(gridName) { }

    protected override void OnBeforeBoot(Mother mother)
    {
      //
    }
}
```

`TestSession` lives in `MotherCore.Tests.TestUtilities`. Each downstream project owns only its thin subclass.

---

### Summary

| # | Utility | Location | Unblocks gaps | Status |
|---|---|---|---|---|
| H1 | `CustomDataBuilder` | `TestUtilities/CustomDataBuilder.cs` | All variable/param tests (CF1, CF3 removed as not required) | **Done** |
| H2 | `TrackingCommand` (shared) | `TestUtilities/TrackingCommand.cs` | C5, C6, C12, C13 | **Done** |
| H3 | `ClockDriver` | `TestUtilities/ClockDriver.cs` | C5, C12, coroutine ordering | **Done** |
| H4 | `PrintCapture` | `TestUtilities/PrintCapture.cs` | C7, C9 | **Done** |
| H5 | `MockIGCNetwork` + `MockIGC` | `TestUtilities/MockIGCNetwork.cs` | C13, R7, all multi-script tests | **Done** |
| H6 | `BootedCommandBus()` in `BaseModuleTests` | `Tests/BaseModuleTests.cs` | All CommandBus tests | Superseded by `TestSession` |
| H7 | Complete `ProgrammableBlockFactory` | `Factories/ProgrammableBlockFactory.cs` | R7, C13 (CF1, CF3 removed as not required) | **Done** |
| H8 | `TestSession` | `TestUtilities/TestSession.cs` | All end-to-end tests; base for MotherOS/GUI | **Done** |

All harness items H1–H8 are complete. Phase 1 test gaps can now begin.
