# Mother Core: size reduction (master spec)

Status (2026-10-25, branch `feature/conditional-logic`): this is the plan of record for script size. It replaces the cross-project plan that lived in `MotherOS/specs/script-size-reduction.md`. Script-specific leftovers stay in `MotherOS/specs/script-size-reduction.md` and `MotherGUI/specs/reduce-script-size.md`. `MotherCore/specs/v1.1-character-reduction.md` is historical (superseded by `BlockCommand<T>` and `BlockSet<T>`).

## Goal

Remove at least 5,000 characters from Core. Core is compiled into every script (OS, GUI, MAPS) through `MotherCore.projitems`, so each Core character saved is saved three times. The extension scripts should gain roughly the same amount as a by-product.

Limit: 100,000 characters in the packaged script (Programmable Block). C# 6.

## Current state

### Measurements disagree, rebuild first

| Script | Last documented (Release, before conditional logic) | Newest `script.cs` found in `%AppData%\SpaceEngineers\IngameScripts\local` (2026-10-25 17:35) |
|---|---|---|
| Mother OS | 94,657 | 98,282 |
| Mother GUI | 96,387 | 99,301 |
| MAPS | 93,455 | 96,383 |

The newest files are 2.9k to 3.6k larger than the documented numbers. The most likely cause is the conditional logic work (`ConditionModule`, `if/else/end`, `when`, `Block<T>().Number/Flag/Status`). It could also be a Debug build or a build where the optimizer was skipped. I did not run a build, so this is unconfirmed. Action: run `dotnet build -c Release -v n` on OS, GUI and MAPS and record the `ScriptOptimizer: a -> b characters` line and the final count. Use those as the baseline for the 5k target.

Confirmed baseline (latest, supplied by the maintainer): OS 98,156, GUI 99,175, MAPS 96,257. Headroom: OS 1,844, GUI 825, MAPS 3,743. The documented 94-96k figures are stale; conditional logic added about 3.7k to 3.8k per script. Use these numbers for the 5k target, so the goal is Core shrinking to roughly OS 93.2k, GUI 94.2k, MAPS 91.3k or lower.

### Where Core characters go

Estimated from the packaged GUI script (plus or minus 2k per row, attributed by string literals). Core is about 61.6k of GUI's 96k.

| Core area | Packaged chars (est.) |
|---|---|
| CommandBus | 8,500 |
| IntergridMessageService | 8,300 |
| BlockCatalogue | 7,900 |
| DisplayModule and `Display` | 6,900 |
| ConditionModule | 5,700 (source is 41k, the largest single module file after BlockCatalogue and IGMS) |
| Configuration | 4,000 |
| Mother, BaseModule, Almanac, Utilities | 12,000 |
| Connector, MergeBlock, Mechanical, ActivityMonitor | 2,800 |

The ConditionModule figure predates the full condition registry; re-measure it after the rebuild.

### Build-time passes already in place

The post-build `ScriptOptimizer` (`MotherCore/build/ScriptOptimizer`) and `RemoveKeywords.ps1` run from `MotherCore.targets` for every consumer. Measured on 2026-10-25 before the conditional logic growth. Staged in MotherCore, not committed, not yet run in game.

| Pass | OS | GUI | MAPS | Notes |
|---|---|---|---|---|
| `var` for locals | 2,091 | 1,911 | 1,835 | Runs first |
| Collection aliases (`AliasRewriter`) | 2,432 | 2,345 | 2,410 | Generated one-character subclasses of `List`, `Dictionary`, `HashSet` and others. Casts, `is` and `as` keep the original type. Estimate in the GUI spec was 3.7-4.0k; real is 2.4k. |
| Narrow `public` (`AccessNarrower`) | 610 | 477 | 592 | |
| Line breaks (`NewlineStripper`) | 660 | 689 | 673 | |
| `RemoveKeywords.ps1` (`private`, `readonly`, top-level `public`) | 2,478 | 2,162 | 1,943 | |

Each pass recompiles and a pass that breaks the script is skipped. `MotherCoreOptimize=false` disables the optimizer.

### Source-level work already done

- `BlockCommand<T>` and `RegisterBlockCommand<T>`: 36 no-argument block commands migrated in OS and Core (9,236 characters saved on OS). Details in `MotherOS/specs/script-size-reduction.md`.
- `BlockSet<T>` and `BaseModule.Block<T>(prefix)` (`Command`, `Number`, `Flag`, `Status`, `Monitor`, `On`), shipped with the logic engine in commit `bd40e3c`. Used by Core `ConnectorModule` and 12 OS and MAPS modules.

### Decisions already taken (2026-10-25)

- No Core module is compiled out. ConditionModule stays (GUI keeps `if/else/when`). Connector, MergeBlock and Mechanical monitoring stay because `BlockCatalogue` refreshes on their events. No `DefineConstants` or Lite profile.
- GUI stays a single script. No companion views script.
- Per-module variant builds and dead-member pruning (239 characters) are not pursued.

## Remaining options for Core

Savings are estimates from token counts and source counts, not measured builds. "Core only" is what the change removes from every script's Core share. Items overlap, so do not add them blindly.

| # | Option | Est. saving per script | Effort | Risk | Notes |
|---|---|---|---|---|---|
Automatic, no source edits, helps all scripts. Replaced `StringHelper`. Implemented as `CallHelperRewriter` (wired in `ScriptOptimizer\Program.cs`, tests in `ScriptOptimizer.Tests`). In-game load confirmed 2026-10-25 for the five-method build. Only the instruction counts are still open. |
| C2 | Lambda command registration, Core's commands (see below) | **Done.** Core's 11 commands are lambdas; `purge` and `rename` were the last two classes (491 on Test). The original estimate of about 1,000 counted conversions that had already shipped. | Medium | Low | The fixed cost is shared with the OS and GUI conversions, which is where most of the saving is. |
| C3 | String consolidation: duplicated literals (`"OriginName"` x7, `"almanac"` x8, `"Commands"` x6, `-----` separators) | **Shipped, measured 324 on Test** (the 600-1,000 estimate was too high: each constant reference still costs about 3 characters after minification, so a literal only pays back from about 3 uses) | Low | Wire format if protocol keys are renamed | Do not rename intergrid keys. Constants only. `Literals` in `Utilities\Literals.cs`. |
| C4 | `RegisterModule<T>()` removing `public X(Mother m):base(m){}` and `new X(this)` | 500-1,100 | Medium | Field initializers that read `Mother` break | Core's 16 modules count in every script. Check each first. |
| C5 | `Updated(name, text)` helper for `MessageFormatter.Format(BlockMessages.BlockUpdated, ...)` | 400-600 | Low | None | 97 `Format` calls across the trees. |
| C6 | Merge the duplicated `ActivityMonitor` indicator in `Terminal.cs` and `DisplayModule.cs` | about 300 | Low | None | Was GUI item 1.7. |
| C7 | Expression-bodied members and single-statement braces in `ScriptOptimizer` | 300-500 | Medium | None | Do last. |
| C8 | `Keys(ini, section)` helper | about 200 | Low | None | Re-measure after the alias pass. |
| C9 | Audit `ConditionModule`, `BlockCatalogue`, `IntergridMessageService` and `CommandBus` for duplicate paths | unknown | Medium | Behaviour | Not analysed. The spec for conditions (`ConditionalLogicSpec.md`, "Architecture / Reuse") already lists tips: one parse path, one `Compare(op, a, b)` switch, table-driven registries. Check the implementation follows them, and scan for the 5.7k estimate being inflated by Tier 2 or Tier 3 properties that no script registers. |

### Does this reach 5k?

- C1 + C2 + C3 + C5 + C6 is about 4,800-6,200 before overlap. Realistically about 4,500-5,500, so 5k is reachable but only if C1 ships. Without C1 the total is about 2,300-3,000 and the target is missed.
- Adding C4 and C7 gives a margin of about 1-1.5k.
- C9 is the only option that could add more than 1k without a new mechanism. It needs a one-day measured review before it is counted.

Recommended order: rebuild and record baseline, C2 (also unlocks the OS and GUI savings), C1 (after C2 because command conversion removes call sites), C3, C5, C6, then C4 and C7 only if the target is still not met, with C9 in parallel as a measurement task.

## C1: call-helper pass

New pass in `ScriptOptimizer`, same design as `AliasRewriter` and `AccessNarrower`.

Route repeated API calls through one-character forwarding methods in a top-level static class placed after `Program`:

```csharp
static class H
{
    public static bool I(string s) => string.IsNullOrEmpty(s);
    public static string S(this string s, int i) => s.Substring(i);
    public static bool C<K, V>(this Dictionary<K, V> d, K k) => d.ContainsKey(k);
    public static bool G<K, V>(this Dictionary<K, V> d, K k, out V v) => d.TryGetValue(k, out v);
}
```

Measured as a Roslyn rewrite estimate on the 2026-10-25 builds (net of helper declarations):

| Method | GUI | OS | MAPS |
|---|---|---|---|
| `string.IsNullOrEmpty` | 716 | 274 | 223 |
| `Dictionary.ContainsKey` | 313 | 443 | 273 |
| `Dictionary.TryGetValue` | 254 | 194 | 174 |
| `string.Substring` | 374 | 262 | 238 |
| `double.TryParse` | 153 | 141 | 189 |
| All 26-37 worthwhile methods | 2,892 | 2,506 | 2,727 |

Rules:
- One helper per exact method symbol or overload. Group generics by `OriginalDefinition`.
- Skip struct instance methods (the extension method copies the receiver), except primitives.
- Keep `out` and `ref`. Skip calls on a `null` literal, and inside expression trees or `nameof`. Parenthesise or skip a non-primary receiver.
- Do not wrap interface member calls (`IDictionary.ContainsKey`).
- Pass order: `var`, aliases, call helpers, narrowing, line breaks. The helpers must stay `public` (narrowing comes later).
- Recompile; skip the pass on any error.

Instruction count: each wrapped call adds a method call, and the changelog records an earlier "script too complex" failure. Names are already minified when the optimizer runs, so exclude by API symbol, not by type name. First version: the five methods above only. A sixth (`List.ForEach`) failed to load in game and was reverted, see the checklist. Measure `Runtime.CurrentInstructionCount` at the end of boot and as the average over about 60 idle ticks, before and after. Narrow the allow-list if idle rises by more than 1%. Only if that fails, add a `[Hot]` attribute on per-tick types (`Clock`, `Terminal` update, render loops) that the pass honours and then strips.

Checklist:
- [x] Manual first slice (interim, removed): `StringHelper` (`IsEmpty`, `Sub`), call sites rewritten by script in 35 files. Saved 536 (OS), 1,094 (GUI), 440 (MAPS), 374 (Test). It served as the number the automatic pass had to beat.
- [x] Automatic pass: `ScriptOptimizer/CallHelperRewriter.cs`, runs between aliases and narrowing. Allow-list of five methods (seven overloads at most): `string.IsNullOrEmpty`, `string.Substring` (1 and 2 arguments), `Dictionary.ContainsKey`, `Dictionary.TryGetValue`, `double.TryParse(string, out double)`. A helper is only emitted when its call sites repay its declaration. Static methods become receiver-style extension calls (`s.I()`, a non-primary receiver is parenthesised), instance methods only rename the method (`s.S(1)`). Nested calls are rewritten together. The pass is skipped by the existing recompile check on any error.
- [x] Decision (2026-10-25): the manual helper goes away once the pass matches it. Gate met on a Release build of all four consumers (see the measurement log: Test 58,636 against 58,653 with `StringHelper` plus the pass, before the dictionary and parser helpers). Call sites were reverted to `string.IsNullOrEmpty(x)` and `x.Substring(...)` with a Roslyn rewrite (Core 18 files, OS, GUI and MAPS 17 more), and `StringHelper.cs` and its `MotherCore.projitems` entry are deleted.
- [x] `AliasRewriter.Names` (shared by both passes) fell back to pairs only over unused single characters, so a script that had used every single character got no names and the new pass crashed on OS. Pairs now draw on all letters.
- [x] `ScriptOptimizer` tests (`MotherCore/build/ScriptOptimizer.Tests`, NUnit, 15 tests): struct receiver skip, parenthesised receiver, `out` argument, null literal, named arguments, interface receiver, alias subclass receiver, `?.`, nested calls, name collisions, name fallback to pairs, unprofitable helpers.
- Evaluated and skipped (call sites are outside Core, so the declaration is not repaid): `IsBlank` (0 Core calls, net loss in OS and GUI), `Join` (0 Core calls, net loss), `Trim()` to `.T()` (3 characters per call, about 80 net at best). Still open: `StartsWith`/`EndsWith`, `Split`, `StringBuilder.Append` (measure instruction count first). The pass makes each of these a one-line allow-list entry, and the profit check drops it if it does not pay.
- [x] `List.ForEach` was tried on 2026-10-25 and reverted. It saved 247 (OS), 87 (GUI), 97 (MAPS) and 60 (Test), but the OS script then failed in game with an "Assembly not found" error. Cause not identified; the only difference from the working build was the `List<K>.ForEach(Action<K>)` helper and its call sites. Do not retry until the error is understood (check the Programmable Block's whitelist for generic extension methods over `Action<K>`, or a ForEach-only build with the helper class removed). The allow-list is back to five methods.
- [x] Measured and held back (2026-10-25, OS only, one experimental run, 444 extra characters over the five-method list, of which `ForEach` was about 220): `List.Add` (saves 1 character per call and is the hottest call in the script, so never worth it), `String.Trim` (about 20 net), `String.Replace` (about 55, used by the JSON serializer), `String.StartsWith` (about 50), `StringBuilder.Append` (about 55, serializer loops), `IsNullOrWhiteSpace` (5 sites). The experimental run was never run in game, and the `ForEach` failure suggests widening needs an in-game check per method.
- [x] Verify in game that extension methods in a top-level static class load (the helper class is placed after `Program`). Confirmed 2026-10-25: the five-method OS build loads. The build with `List.ForEach` did not, so the failure is specific to that helper, not to extension methods.
- [ ] Record instruction counts before and after. `Mother.cs` already prints `Complexity: N/50000` each cycle when `DebugMode` is on. Procedure: build with `MotherCoreOptimize=false` and with the optimizer, in the same world, and record the `Complexity` value at the end of boot and the average over about 60 idle ticks. Accept if idle rises by less than 1%.
- [ ] Widen further (`StartsWith`, `Replace`, `Trim`, `Append`, `IsNullOrWhiteSpace`, `ForEach`) only if that measurement allows it, one method per in-game test.

## C2: lambda command registration (Core migration details)

All migration details for Core live here. The OS and GUI specs only list which of their commands convert.

### Problem

Every hand-written command repeats a field, a constructor, a `Name` override and an `Execute` signature. In the packaged script that is about 125-155 characters before the first line of logic. `BlockCommand<T>` already fixed this for block-by-name commands.

### Design

```csharp
// MotherCore/src/Commands/LambdaCommand.cs (new; add to MotherCore.projitems)
public class LambdaCommand : BaseModuleCommand
{
    readonly int MinArgs;
    readonly string Usage;
    readonly Func<TerminalCommand, string> Run;

    public override string Name { get; }

    public LambdaCommand(string name, int minArgs, Func<TerminalCommand, string> run, string usage)
    {
        Name = name; MinArgs = minArgs; Run = run; Usage = usage;
    }

    public override string Execute(TerminalCommand command)
        => command.Arguments.Count < MinArgs ? Usage ?? CommandBus.Messages.NoArgumentsProvided : Run(command);
}

// BaseModule, plus the same one-line overload on Mother and CommandBus
public void RegisterCommand(string name, int minArgs, Func<TerminalCommand, string> run, string usage = null)
    => RegisterCommand(new LambdaCommand(name, minArgs, run, usage));
```

Fixed cost about 250 characters, repaid after three commands. `BlockCommand<T>` stays as a thin wrapper over the same registration.

### Core commands to convert (12, all ship in every script)

`boot`, `purge`, `rename`, `halt`, `help`, `var/set`, `ping`, `get`, `set`, `clear`, `print`, `connector/lock` (this last one also needs the `InvalidCommandFormat` vs `NoArgumentsProvided` alignment, see below).

Examples:

```csharp
// Terminal.Boot(); replaces ClearCommand.cs
CommandBus.RegisterCommand("clear", 0, c => { ClearConsole(); return ""; });

// IntergridMessageService.Boot(); replaces PingCommand.cs (registered through Mother)
Mother.RegisterCommand("ping", 0, c => { Ping(); return "Pinging all grids"; });
```

A command with two guards (for example `light/intensity`: 0 arguments gives `NoArgumentsProvided`, 1 argument gives `"Invalid brightness format."`) uses `minArgs: 1` and tests the second case inside the lambda, so behaviour is identical.

### Helper move

`GetIncrementalValue`, `GetDistributedValue` and `IsSharedMode` are `protected` on `BaseModuleCommand`, so lambdas cannot call them. Move them onto `TerminalCommand` (where `Options` already lives), for example `c.GetIncrement(text)`.

### Rules

- Name, argument guard, usage text and returned text must match the old class exactly. Port existing assertions, do not rewrite them.
- Keep registration order. `CommandBus` dispatches on the first name match and `help` lists in registration order.
- Keep a real class when it has state beyond the module field (sorter filter base, calibrate, dock).
- Lambdas capturing `this` keep the module alive for the program lifetime, as the old command class did.

### Tests

Core tests that construct a command class directly stop compiling when the class is deleted. Switch them to the terminal path (`script.RunTerminal("...")`) that the migrated block commands already use. Add unit tests for the guard, the usage text and `minArgs` of 0.

### Performance

No per-tick cost. One extra delegate call per execution, against thousands of instructions in a typical command body. Measure once with a routine that fires a converted command about every few ticks over 60 ticks; accept if the difference is under 1%.

### Connector and `BlockCommand` migration state in Core

- `connector/toggle` and `connector/unlock` are migrated (`ConnectorModule`, via `BlockSet<T>`). About 508 characters gross; the shared `BlockCommand<T>` plus registration cost about 430, so Core's net from block commands is about 80. The saving is in the modules that consume Core.
- `connector/lock` is the exact block-command template except it returns `InvalidCommandFormat` for no arguments. Align it to `NoArgumentsProvided` (a one-word behaviour change) only after checking no test depends on it, then migrate.
- Tier 2 `light/reset` (two block types) needs a two-type `BlockSet` overload; that is OS-only and tracked in the OS spec.

## Other Core notes

- Do not rename intergrid protocol keys to save characters: it changes the wire format between grids.
- Language limits: no `out var`, tuples, local functions, pattern matching, `using` aliases or `using static`.
- `RemoveKeywords.ps1` reads and writes without an explicit encoding under PowerShell 5.1. It works today but is fragile. The `°` mojibake in `SetMotorStatorLimitCommand.cs` (line 85) should become `\u00B0`.
- Growth guard: the GUI went from 70k to 99k characters in six months. Add a build warning above 95,000 characters so saved space is not silently spent.

## Phase checklist

- [ ] Phase 0: run MotherCore, MotherOS and GUI tests. Commit the staged optimizer work. Rebuild OS, GUI and MAPS in Release and record the real baseline here. In-game check on OS and GUI: boot, `help`, a stored-variable round trip (Serializer), an Almanac or intergrid ping (`is Dictionary<string,object>` checks), an `if/else` routine, and a GUI view. Add a CI step that fails when the optimizer reports a skipped pass.
- [x] Phase 1: C2 for Core's commands. `purge` and `rename` converted last (`connector/lock` is no longer a Core command). `purge` now lists purged modules in the order almanac, storage, where it used to list storage first when both were named.
- [ ] Phase 2: C1 call helpers. Done: automatic pass with the five-method allow-list (`CallHelperRewriter`), `StringHelper` removed (`ForEach` tried and reverted), in-game load confirmed 2026-10-25. Open: instruction count (needs the game), then any further widening one method at a time.
- [ ] Phase 3: C3 (done, see the log), C5, C6.
- [ ] Phase 4: C9 measurement review of ConditionModule, BlockCatalogue, IGMS, CommandBus.
- [ ] Phase 5: C4, C7, C8 only if the 5k target is not met.

After each phase, rebuild all three consumers, run MotherCore (436 tests at last count), MotherOS (285) and GUI tests, and add a row below.

## Measurement log

| Date | Change | OS | GUI | MAPS | Core delta |
|---|---|---|---|---|---|
| 2026-10-25 | Optimizer passes and keyword removal (documented, pre-conditional growth) | 94,657 | 96,387 | 93,455 | n/a |
| 2026-10-25 | Newest `script.cs` on disk, unverified build config | 98,282 | 99,301 | 96,383 | n/a |
| latest | **Baseline for the 5k target** (user-supplied) | 98,156 | 99,175 | 96,257 | 0 |
| 2026-10-26 | Re-baseline after conditional logic growth (Test 59,777) | 98,407 | 99,701 | 96,783 | 0 |
| 2026-10-26 | C1 manual slice: `StringHelper` (`IsEmpty`, `Sub`). Test 59,403 | 97,871 | 98,607 | 96,343 | -536 OS, -1,094 GUI, -440 MAPS |
| 2026-10-25 | C1 automatic pass (`CallHelperRewriter`, 6 helpers) replaces `StringHelper`; call sites reverted. Test 58,636. Test with `StringHelper` plus the pass, before the revert: 58,653 | 97,104 | 97,922 | 95,687 | -1,303 OS, -1,779 GUI, -1,096 MAPS (against the 2026-10-26 re-baseline) |
| 2026-10-25 | C1 allow-list widened with `List.ForEach` (7 helpers), **reverted**: OS failed in game with "Assembly not found". Finals were OS 96,857, GUI 97,835, MAPS 95,590 | 96,857 | 97,835 | 95,590 | reverted |
| 2026-10-25 | C1 after the `ForEach` revert (6 helpers, five-method allow-list). Final character counts. Test 58,612 | 97,080 | 97,878 | 95,663 | -1,327 OS, -1,823 GUI, -1,120 MAPS (against the 2026-10-26 re-baseline) |
| 2026-10-25 | Optimizer name range widened from U+052F to U+08FF. Cause: the packager hands out single-character names up to U+07A4, which used every one the optimizer could take, so about 55 alias and helper names were two characters. Now none are. Test 58,612 (unchanged, its script is small enough not to exhaust the range) | 96,776 | 97,548 | 95,386 | -304 OS, -330 GUI, -277 MAPS against the previous row. In game not yet checked beyond U+07A4, the packager's own limit. |
| 2026-10-26 | **Latest baseline (user-supplied)**, after the name-range widening. OS is 100 below the row above, probably OS-side edits since | 96,676 | 97,548 | 95,386 | -1,480 OS, -2,153 GUI, -1,397 MAPS against the 2026-10-26 re-baseline. Test 58,612 |
| 2026-10-26 | C3: `Literals` constants (header keys, `almanac`, `storage`, `waypoint`, config section names, `REQUEST::` and `RESPONSE::` prefixes, `SquareSimple`, `Monospace`, the 54-dash rule) replace 17 repeated literals at about 50 sites. `LogCommand` builds its line once. Test 58,288 (Release, MotherCore 706 tests pass). OS, GUI and MAPS not rebuilt yet | n/a | n/a | n/a | -324 Test (58,612 to 58,288) |
| 2026-10-26 | C2: `purge` and `rename` converted to lambdas in `Mother.RegisterCoreCommands`; `PurgeCommand.cs` and `RenameCommand.cs` deleted. Test 57,797 (Release, MotherCore 710 tests pass). OS, GUI and MAPS not rebuilt yet | n/a | n/a | n/a | -491 Test (58,288 to 57,797) |
