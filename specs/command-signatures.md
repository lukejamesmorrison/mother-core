# Command signatures

Status: **proposal**. Nothing is implemented. Research only: code read, file counts taken with a script, nothing built or run in game.

Last updated: 2026-10-25 (-04:00).

Model: Laravel Artisan [defining input expectations](https://laravel.com/framework/docs/12.x/artisan#defining-input-expectations). A command declares its arguments and options in one string, and the framework parses, validates, defaults and documents from it.

## Problem

Every command re-implements the same steps by hand: count the arguments, print a usage message, resolve the block by name, parse numbers, read options, apply defaults, format a response. The module registers a class per command for this. Measured over Mother OS, MAPS and GUI (source files that derive from `BaseModuleCommand`):

| Measure | Count |
|---|---|
| Command classes | about 60 (63 files matched, one is an abstract base) |
| Of which check `NoArgumentsProvided` | 42 |
| Of which return `InvalidCommandFormat` | 28 |
| Of which resolve blocks and return `BlockNotFound` | 40 |
| Of which index `Arguments[1]` | 37 |
| Of which parse a number (`float.Parse`, `NumberHelper.Parse`, ...) | 13 |
| Of which read an option with `GetOption` | 12 |
| Of which use `GetIncrementalValue` / `IsSharedMode` | 8 / 6 |
| Source size of these files (with doc comments, not packaged size) | 133 KB |

The scaffolding costs more than size:

- **Hand-rolled validation drifts.** Read in code, not run:
  - `SetLightColorCommand` guards `Arguments.Count == 0` and then reads `Arguments[1]`, so `light/color Lamp` should throw `ArgumentOutOfRangeException`.
  - `RegisterCommand("print", 0, c => c.Arguments[0])` in `Terminal` declares zero required arguments and reads the first, so `print` alone should throw.
  - `RegisterCommand("set", 1, ...)` in `LocalStorage` declares one and re-checks for two inside the lambda.
- **Bad numbers are not handled.** `float.Parse` is called directly in many commands (`piston/distance --speed=abc`). I found no `catch` on the `ExecutePrimitiveCommand` to `Execute` path (the only catches in the command path are in `ResolveStep` and `ConditionModule`), so a typo probably throws out of the coroutine. Needs confirming in game.
- **Typos in options are silent.** Only the sorter commands reject unknown options (`BlockMessages.InvalidOption`). Elsewhere `--sped=2` is ignored.
- **Usage text does not exist.** `help` prints command names only (`CommandBus.Boot`). There is no way to ask what arguments a command takes. A commented-out hint already exists in `SetPistonDistanceCommand` and `SetRotorAngleCommand`: `//public string Signature => "piston/distance {piston} {distance} {--speed=} {--share}";`.
- **Cloned commands.** `piston/distance` and `rotor/rotate` and `hinge/rotate` differ by block type and a unit. The four cockpit commands (`dampeners/on|off`, `handbrake/on|off`) are 50 lines each and differ by one property.
- **Character budget.** Limit is 100,000 packaged characters. Headroom per `core-size-reduction.md`: OS 1,844, GUI 825, MAPS 3,743. Whatever is added to Core is added to all three scripts.

## Goal

1. A command is registered with one **signature string** plus a handler. No class, no `Name`, no argument counting, no usage text, no number parsing, no block lookup boilerplate.
2. The signature is the single source for parsing, validation, defaults, usage and help.
3. Core gets **smaller**, not larger, once its own callers are migrated. This is the main constraint (see Size).
4. Migration is incremental. Existing `IModuleCommand` classes keep working until replaced.

Non-goals: changing the routine grammar (`;`, `{ }` groups, `@grid`), the `!` / `!!` / `_` prefixes, config commands (`[commands]` and `{{name:default}}`), or the construct command sync. The signature layer sits **after** `TerminalCommand` tokenization and does not touch routing.

## Current behaviour (read in code)

| Point | Location | Behaviour |
|---|---|---|
| Tokenize | `TerminalCommand.ParseCommand`, `SplitInputIntoTerms` | Splits on spaces, quotes group. `--k=v**`** or bare `--k` (value `"true"`) goes to `Options`, everything else to `Arguments`. A duplicate option throws `ArgumentException`. Values need `=`; `--speed 2` is not an option value. |
| Contract | `IModuleCommand`, `BaseModuleCommand` | `Name` and `string Execute(TerminalCommand)`. The returned string is printed. |
| Lambda commands | `LambdaCommand`, `BaseModule.RegisterCommand(name, minArgs, run, usage)` | Only a name and a minimum argument count. No types, options or defaults. |
| Block commands | `BlockCommand<T>`, `BlockSet<T>.Command(verb, Action<T>, message)` | Resolves `Arguments[0]` with `GetBlocksByName<T>`, applies the action, formats the message. Takes no other arguments or options. |
| Dispatch | `CommandBus.ExecutePrimitiveCommand` | Prints `> command`, calls `Execute`, prints the result. The result is never inspected, so a usage error is only text and a routine continues past it. |
| Failure | `CommandBus.Fail`, `RoutineState.Failed` | Only used when variable resolution fails. A failing `Execute` cannot stop a routine. |
| Sync | `GetSelfCommandNames`, `RegisterRemoteCommands` | Only command **names** cross the construct. |
| Option helpers | `TerminalCommand.GetIncrement`, `GetDistributedValue`, `IsShared`; `BaseModuleCommand.GetIncrementalValue`, `IsSharedMode` | `--add` / `--sub` / `--share` are tested with `ContainsKey`, so `--share=false` still means shared. `RenameCommand` uses `GetBoolFromString`, which does honour `false`. |
| Existing fluent layer | `BlockSet<T>` (`Command`, `Number`, `Flag`, `Status`, `Monitor`, `On`) | Already the registration style used by Mother OS modules (`Block<IMyPistonBase>("piston").Command(...)`). |

## Laravel features: adopt, adapt, skip

| Laravel | Mother | Decision |
|---|---|---|
| `{user}` required argument | `{piston}` | Adopt |
| `{user?}` optional | `{piston?}` | Adopt |
| `{user=foo}` default | `{speed=0.5}` | Adopt |
| `{user*}` array | `{filter*}` | Adopt (needed for `sorter/add item item ...`) |
| `{--queue}` flag | `{--share}` | Adopt |
| `{--queue=}` option with value | `{--speed=}` | Adopt |
| `{--queue=default}` | `{--speed=0.5}` | Adopt |
| `{--id=*}` repeatable option | n/a | Skip. `TerminalCommand` rejects duplicate options. |
| `{--Q\|queue}` shortcut | n/a | Skip. A leading `-` is a negative number in Mother (`-45`). |
| `{user : description}` | n/a | Skip. Descriptions cost packaged characters in every script. The signature is the documentation. |
| types | `{distance:float}` | **Add.** Laravel has none; here number parsing is the main boilerplate. |
| `$this->argument('user')`, `$this->option('queue')` | `c.Str("user")`, `c.Float("speed")`, `c.Flag("share")` | Adopt, typed accessors |
| `Artisan::command('mail:send {user}', fn)` closure commands | `RegisterCommand("signature", handler)` | This is the primary model |
| Interactive prompts for missing input | n/a | Not applicable |

## Design

### 1. Signature grammar

```
name  token*
token := {arg} | {arg?} | {arg=default} | {arg*} | {arg?*} | {--flag} | {--opt=} | {--opt=default} | {--*}
arg   := word [":" type]        opt := word [":" type]    (type follows the optional "=default")
type  := int | float | bool | a|b|c     (omitted means string; a|b|c is a choice, see 2)
```

`{arg*}` takes one or more values and `{arg?*}` zero or more (as in Laravel, where `*` alone is required). `{--*}` accepts any option and leaves the unknown ones in `Options` (see 8).

Examples, from commands that exist today (the full list is in "Signatures for every registered command"):

```
piston/distance {piston} {distance:float} {--speed=0.5:float} {--add} {--sub} {--share}
rotor/rotate    {rotor} {angle:float} {--speed=0.5:float} {--add} {--sub} {--share}
light/blink     {light} {interval} {--length=:float} {--offset=:float}
sorter/add      {sorter} {filter?*} {--fresh} {--mode=whitelist|blacklist}
dampeners/on    {cockpit?}
set             {key} {value}
```

`:` is read only at the end of a token, so a default cannot contain one. Single braces are used because signatures live in C# strings. They never appear in routine text, so they do not clash with `{ ... }` parallel groups or the `{{name}}` placeholders of config commands.

### 2. Types

`SetPistonDistanceCommand`, `SetRotorAngleCommand` and others each parse numbers. The parser does it once, **before** the handler runs:

- `int`, `float`: parsed with `float.TryParse` (an `int` is checked and cast). `float` also accepts a trailing `%`, which replaces the `Replace("%","")` calls in `wheel/*`. A bad value is answered with `Invalid argument for block: <value>` (reuse `BlockMessages.InvalidArgument`) instead of throwing.
- `bool`: `GetBoolFromString`. Flags always use this, which fixes `--share=false`.
- Choice `a|b|c`: the value must be one of the words, compared case-insensitively. The inventory needs it for `sorter/mode`, `sorter/add --mode`, `screen/content`, `view/notify --level` and `calibrate`. It replaces hand-written `switch` and `TryParse` helpers and gives the usage text the legal values for free.
- A default is validated as the same type when the signature is registered.

### 3. Binding and the handler

New `CommandInput` wraps `TerminalCommand` after validation:

```csharp
class CommandInput
{
    public readonly TerminalCommand Command;
    public string Str(string name);          // argument or option, default applied
    public float Float(string name);
    public int Int(string name);
    public bool Flag(string name);
    public bool Has(string name);            // option or optional argument was given
    public List<string> List(string name);   // {name*}
    public float Increment(string name);     // value for --add, negated for --sub, else 0
    public float Share(float total, int count); // divides when --share
}
```

`Increment` and `Share` move the logic out of `BaseModuleCommand`, which carries a note to delete `GetIncrementalValue` once no class uses it.

A new `SignatureCommand : IModuleCommand` holds the parsed signature and the handler. Its `Execute`:

1. Match `TerminalCommand.Arguments` and `Options` against the parsed tokens.
2. On a mismatch (missing required argument, extra argument, unknown option, bad type) return `Usage: <signature>`, or the specific reason plus usage.
3. Otherwise call the handler with a `CommandInput`.

The signature is parsed once, when the command is registered, not per execution.

Registration (replaces `RegisterCommand(name, minArgs, run, usage)`):

```csharp
RegisterCommand("set {key} {value}", c => { Set(c.Str("key"), c.Str("value")); return c.Str("key") + "=" + c.Str("value"); });
```

### 4. Block commands

`BlockSet<T>` already knows `T` and the prefix, so the block lookup moves into the set. The **first argument token is the block target**. The handler receives the resolved list, and `BlockNotFound` is returned by the set, not by each command:

```csharp
Block<IMyPistonBase>("piston")
    .Command("distance {piston} {distance:float} {--speed=0.5:float} {--add} {--sub} {--share}",
        (pistons, c) =>
        {
            float step = c.Increment("distance");
            float each = c.Share(c.Float("distance"), pistons.Count);

            pistons.ForEach(p => SetDistance(p, step == 0 ? each : p.CurrentPosition + c.Share(step, pistons.Count), c.Float("speed")));

            return "distance=" + c.Str("distance") + "m";   // formatted into BlockMessages.BlockUpdated
        });
```

This replaces `SetPistonDistanceCommand` (67 lines). The existing `Command(verb, Action<T>, message)` overload stays as the shorthand for `"verb {name}"`, and `BlockCommand<T>` is rebuilt on top of `SignatureCommand` so it can be deleted. Commands that need two block types (`light/*` covers `IMyLightingBlock` and `IMySearchlight`) call the lookup twice in a plain `RegisterCommand` handler; the set variant is for the single-type case.

The sketch above is illustrative. The exact handler shape (return a detail string, return a full message, or return `null` for the default) is open question 3.

### 5. Usage and help

- `Usage: <signature as registered>`. No extra strings are stored, the signature is its own usage text.
- `help` lists names as today. `help <name>` prints the signature.
- Config commands and remote commands have no signature. `help x` for them prints the name only. Sync keeps sending names; adding signatures to the 5 second `sync` payload is a non-goal (cost on every tick, no consumer yet).

### 6. Strictness and compatibility

Decision: **strict**. Today extra arguments and unknown options are ignored. A signature makes them detectable, and migrated commands reject them:

| Case | Result |
|---|---|
| Missing required argument | Usage |
| Extra positional argument (no `*`) | Usage |
| Unknown option | `Invalid command option: --x` (`BlockMessages.InvalidOption`) |
| Bad typed value | `Invalid argument for block: v` |

Consequences:

- **The signature must list every option the old class read.** An option left out of a signature becomes an error for players. Each migration compares the old class against its signature (`GetOption`, `Options.ContainsKey`, `Options[...]`, `IsSharedMode`, `GetIncrementalValue` which reads `add` / `sub`), and the parity tests include every option the old class accepted.
- **No global options exist to exempt.** I grepped Core for option reads: they are only inside commands (`var/set --save`, `rename --unique`) and in `ConditionModule` for `if` / `when` (`--at`, `--band`, and its own keys), which are not module commands. Config commands (`[commands]`) consume their options for `{{name}}` substitution before any module command runs, so they are unaffected.
- **One command takes arbitrary options.** `autolcd/print` forwards every option to its view as template values, so it cannot list them. It declares `{--*}` (see 8). No other command does this.
- **Visible behaviour change.** Player routines with stray arguments or misspelt options stop working at the step, with a usage line, instead of running silently. Note it in `CHANGELOG.md` and the release notes when the first commands migrate.
- **Check blueprints.** Before each bulk migration step, scan the maintained blueprints and the player-facing docs for calls that rely on ignored extras.

### 7. Failure and routines

`ExecutePrimitiveCommand` prints the result and moves on, so a usage error does not stop a routine (see Current behaviour). Making it stop needs `Execute` to report failure separately from text, which is the same gap as `remote-routine-failure-reporting.md`. Keep it out of this work; `SignatureCommand` can adopt whatever result type that spec chooses.

### 8. Wildcard options (`{--*}`)

Decided: the wildcard is in. It exists for one command, `autolcd/print`, whose options are data, not behaviour. The player writes `autolcd/print Panel status --title="Fuel" --level=3`, and `SubstituteView` fills the view's `{{title}}` and `{{level:1}}` placeholders from `Options`. The set of names belongs to the view text, which is not known when the command is registered.

Rules:

- **Only unknown options are let through.** Declared options are still checked and typed. `{--*}` only switches off the "unknown option" rejection. Positional arguments stay strict, so `autolcd/print Panel status extra` is still a usage error.
- **The handler sees every option, declared or not.** `CommandInput` does not copy them; the handler passes `c.Command.Options` straight to `SubstituteView`, exactly as today. This keeps the behaviour identical and costs nothing. (Passing only the unknown ones would be a second code path and no command needs it.)
- **Values are raw strings.** There is no typing, a bare `--flag` is `"true"`, and names are matched case-sensitively against the placeholder (`--Title` does not fill `{{title}}`).
- **At most one `{--*}` per signature.** A second is an error at registration. The usage text prints it as written.
- **It keeps declared names reserved.** If a signature ever declares `{--size=:float}` and also `{--*}`, `--size` is typed, and a view's `{{size}}` receives the typed option's text. Do not reuse a declared name as a template variable.
- **Cost.** The token is stored as an option named `*`, and the unknown-option check becomes `k.N==o||k.N=="*"`. About 20 packaged characters, nothing else.

What the wildcard gives up, and what to do about it:

- **Typos are not caught.** `--tittle=Fuel` is accepted and the `{{title}}` placeholder renders empty, because a placeholder with no option and no default is replaced with `""` (`Mother.SubstituteCommandParameters`). The same is true today. A follow-up inside the handler could report options that no placeholder in the view uses (an `IndexOf("{{" + key)` per option). It is not part of the signature layer and is not in the size estimate.
- **A value containing `=` is lost.** `TerminalCommand.ParseCommand` splits an option on every `=`, and `--text=a=b` has three parts, so the value becomes `"true"`. This exists today for every option, but a command that forwards free text is the most likely to hit it. Not changed here. Fix it in the tokenizer (split on the first `=` only) as its own change.
- **It is easy to overuse.** A wildcard disables the typo check for that command, so keep it to commands whose options are template data. A review rule is enough: new `{--*}` uses need a reason in the spec. Config commands (`[commands]` with `{{param}}` placeholders) already work this way and are not module commands, so they need no signature.

## Signatures for every registered command

Read from the code on 2026-10-25: every `RegisterCommand`, `RegisterBlockCommand` and `Block<T>().Command` site in MotherCore, MotherOS, MotherGUI and MotherAutopilotSystem. **115 commands**: Core 11, OS 85 (45 classes plus 40 block shorthand commands), GUI 9, Autopilot 10. The first token is the command name. Each value or option is what the current code reads, so the signature reproduces today's inputs, with these notes:

- `{name:float}` is a number. `float` also accepts `50%`. `{x:a|b}` is a choice. `{--x}` is a flag, `{--x=}` an option with a value, `{--x=d}` one with a default. `{x?}` optional, `{x*}` one or more, `{x?*}` zero or more. `{--*}` is any option.
- The first argument of a block command is the block name or group. A `Block<T>("prefix")` registration resolves it, reports `BlockNotFound`, and passes the handler the blocks (design 4). `{piston}` below is the name of that token and has no other meaning.
- Defaults are the constants in the code (`PistonModule.DEFAULT_SPEED` is 0.5, `RotorModule` and `HingeModule` are 2).
- "Today" in the notes column is the behaviour that changes under a signature (see the list after the tables).

### Mother Core (11)

| Command | Signature | Notes |
|---|---|---|
| `help` | `help {command?}` | The argument is new (design 5). Today it lists names. |
| `halt` | `halt` | |
| `boot` | `boot` | |
| `ping` | `ping` | |
| `clear` | `clear` | |
| `print` | `print {text}` | Today `print` with no text throws (`Arguments[0]` behind `minArgs` 0). Extra words are ignored, so multi-word text must be quoted. |
| `set` | `set {key} {value}` | `LocalStorage`. Today `minArgs` is 1 and the lambda re-checks for 2. |
| `get` | `get {key}` | |
| `var/set` | `var/set {name} {value} {--save}` | |
| `purge` | `purge {modules} {--force}` | `modules` is a comma list (`almanac`, `storage`, `*`). Today the `--force` check runs before the argument check. |
| `rename` | `rename {name} {--unique}` | |

Not registered, built into `CommandBus`, and unchanged by this spec: `wait`, `if`, `elseif`, `else`, `end` and the `when` step of a `[rules]` entry. They are routine control, not module commands.

### Mother OS: block shorthand, 40

All are `verb {target}` and need no handler arguments. They already use `BlockSet<T>.Command(verb, action, message)`, so migration only adds the strictness. Shown as `prefix/verb` with the block type.

| Block | Commands (each takes one `{target}`) |
|---|---|
| `IMyDoor` | `door/open`, `door/close`, `door/toggle` |
| `IMyAirVent` | `vent/pressurize`, `vent/depressurize`, `vent/toggle` |
| `IMyBatteryBlock` | `battery/charge`, `battery/discharge`, `battery/auto`, `battery/toggle` |
| `IMyShipConnector` | `connector/lock`, `connector/unlock`, `connector/toggle` |
| `IMyLandingGear` | `gear/lock`, `gear/unlock`, `gear/toggle` |
| `IMyGasTank` | `tank/stockpile`, `tank/share`, `tank/toggle` |
| `IMyPistonBase` | `piston/reset`, `piston/stop`, `piston/attach`, `piston/detach` |
| `IMyMotorStator` (rotor) | `rotor/unlock`, `rotor/reset`, `rotor/attach`, `rotor/detach` |
| `IMyMotorStator` (hinge) | `hinge/lock`, `hinge/unlock`, `hinge/reset`, `hinge/attach`, `hinge/detach` |
| `IMyFunctionalBlock` | `block/on`, `block/off`, `block/toggle` |
| `IMyTerminalBlock` | `block/actions` |
| `IMyTimerBlock` | `timer/trigger`, `timer/stop` |
| `IMyConveyorSorter` | `sorter/clear` |
| `IMySoundBlock` | `sound/stop` |

### Mother OS: class commands, 45

| Command | Signature | Notes |
|---|---|---|
| `piston/distance` | `piston/distance {piston} {distance:float} {--speed=0.5:float} {--add} {--sub} {--share}` | |
| `piston/speed` | `piston/speed {piston} {speed:float} {--add} {--sub} {--share}` | Has no `--speed` today, correctly. |
| `piston/ulimit` | `piston/ulimit {piston} {limit:float}` | Metres. Registered twice from one class (`ulimit`, `llimit`). |
| `piston/llimit` | `piston/llimit {piston} {limit:float}` | |
| `rotor/rotate` | `rotor/rotate {rotor} {angle:float} {--speed=2:float} {--add} {--sub} {--share}` | Today `abs(speed)`. |
| `rotor/speed` | `rotor/speed {rotor} {speed:float} {--free} {--add} {--sub} {--share}` | `--free` today means `== "true"`. |
| `rotor/lock` | `rotor/lock {rotor} {--stop=true:bool}` | See note 1 below. |
| `rotor/ulimit` | `rotor/ulimit {rotor} {limit:float}` | Degrees. |
| `rotor/llimit` | `rotor/llimit {rotor} {limit:float}` | |
| `hinge/rotate` | `hinge/rotate {hinge} {angle:float} {--speed=2:float} {--add} {--sub} {--share}` | |
| `hinge/speed` | `hinge/speed {hinge} {speed:float} {--free} {--add} {--sub} {--share}` | |
| `hinge/ulimit` | `hinge/ulimit {hinge} {limit:float}` | |
| `hinge/llimit` | `hinge/llimit {hinge} {limit:float}` | |
| `wheel/height` | `wheel/height {wheel} {height:float} {--add} {--sub}` | Metres. No `--share` today. |
| `wheel/power` | `wheel/power {wheel} {power:float}` | Accepts `50%`. |
| `wheel/strength` | `wheel/strength {wheel} {strength:float}` | Accepts `50%`. |
| `wheel/friction` | `wheel/friction {wheel} {friction:float}` | Accepts `50%`. |
| `thruster/thrust` | `thruster/thrust {thruster} {amount}` | `50%` or `100000N`. Stays a string, the handler reads the unit. |
| `gear/auto` | `gear/auto {gear} {state:bool}` | Today `bool.TryParse`, so `1` is rejected. |
| `dampeners/on` | `dampeners/on {cockpit?}` | No argument means the main cockpit. |
| `dampeners/off` | `dampeners/off {cockpit?}` | |
| `handbrake/on` | `handbrake/on {cockpit?}` | |
| `handbrake/off` | `handbrake/off {cockpit?}` | |
| `light/reset` | `light/reset {light}` | Two block types (`IMyLightingBlock`, `IMySearchlight`), so a plain `RegisterCommand` handler. |
| `light/color` | `light/color {light} {color}` | Today a missing color throws. |
| `light/intensity` | `light/intensity {light} {value:float} {--add} {--sub}` | |
| `light/blink` | `light/blink {light} {interval} {--length=:float} {--offset=:float}` | `interval` is a preset (`off`, `slow`, `med`, `fast`) or seconds, so it stays a string. |
| `screen/print` | `screen/print {screen} {text} {--color=} {--bgcolor=} {--size=:float} {--alignment=} {--align=} {--padding=:float}` | `--align` is an alias for `--alignment`, so both are declared. Extra words are dropped today, so multi-word text must be quoted. |
| `screen/color` | `screen/color {screen} {color}` | |
| `screen/bgcolor` | `screen/bgcolor {screen} {color}` | |
| `autolcd/print` | `autolcd/print {target} {view} {--*}` | `target` is `selector` or `selector:index`. Every option is a template value for the view. |
| `autolcd/clear` | `autolcd/clear {target}` | |
| `sorter/add` | `sorter/add {sorter} {item?*} {--fresh} {--mode=whitelist\|blacklist}` | Needs at least one of `item`, `--fresh`, `--mode`; the handler checks that. |
| `sorter/remove` | `sorter/remove {sorter} {item*}` | Today rejects every option, as the signature will. |
| `sorter/mode` | `sorter/mode {sorter} {mode:whitelist\|blacklist}` | |
| `sorter/drain` | `sorter/drain {sorter} {state:bool}` | Today a missing state throws. |
| `sound/set` | `sound/set {sound} {name}` | |
| `sound/play` | `sound/play {sound} {name?}` | |
| `pb/run` | `pb/run {pb} {argument*}` | Today the words are joined with spaces. |
| `timer/start` | `timer/start {timer} {--delay=:float}` | |
| `block/rename` | `block/rename {block} {name}` | |
| `block/action` | `block/action {block} {action} {param?*}` | |
| `block/config` | `block/config {selector} {key} {value}` | Today reports success on zero blocks. |
| `tag/get` | `tag/get {tag}` | Today no argument prints `tag: #`. |
| `tag/set` | `tag/set {selector} {tag}` | Today a missing argument throws. |

Not registered: `gyro/override {gyro} {state:bool}` (`GyroOverrideCommand`). `GyroModule.Boot` has the registration commented out, and I found no packaged class for it in the OS script. It does not count toward the savings. Decision pending for OS (see open question 10).

### Mother GUI (9)

| Command | Signature | Notes |
|---|---|---|
| `view/up` | `view/up {display}` | Today returns a hand-written `Usage: view/up <display name>`. |
| `view/down` | `view/down {display}` | |
| `view/select` | `view/select {display}` | |
| `view/back` | `view/back {display}` | |
| `view/go` | `view/go {display} {target} {parameter?} {--title=}` | `display` may be `self`. |
| `view/notify` | `view/notify {display} {message} {--level=info\|success\|warning\|error} {--duration=3:float}` | An unknown level falls back to info today, and a bad duration to 3. Both become errors. |
| `screen/content` | `screen/content {display} {type:none\|script\|text}` | Today `Not enough args` and `Invalid content type. Use one of: none, script, text.` |
| `screen/script` | `screen/script {display} {script}` | |
| `screen/scripts` | `screen/scripts {display}` | |

### Mother Autopilot System (10)

| Command | Signature | Notes |
|---|---|---|
| `yaw` | `yaw {degrees:float}` | |
| `pitch` | `pitch {degrees:float}` | |
| `roll` | `roll {degrees:float}` | |
| `gyro/face` | `gyro/face {waypoint}` | A GPS string, so it must be quoted. |
| `calibrate` | `calibrate {scope?:all\|status\|thrust\|rotation} {axis?}` | No scope means status. `axis` is `X\|Y\|Z` for `thrust` and `Pitch\|Yaw\|Roll` for `rotation`, which depends on `scope`, so the handler checks it. The class is 1,207 packaged characters, and most of it stays. |
| `dock` | `dock {grid} {--local=} {--remote=}` | |
| `fcs/start` | `fcs/start {--speed=:float}` | |
| `fcs/stop` | `fcs/stop` | |
| `fp/set` | `fp/set {plan}` | |
| `propulsion/thrust` | `propulsion/thrust {thruster} {amount}` | `50%` or `100000N`, as `thruster/thrust`. |

Not registered, kept on purpose: `gyro/override {gyro} {state:bool}`. `AttitudeModule.Boot` has the registration commented out while flight controls are in progress, and the command may come back. Keep it. When the rest of the module migrates, rewrite the class as a commented-out signature registration next to the existing comment, so it is ready to enable and `BaseModuleCommand` is not kept alive by dead code:

```csharp
//RegisterCommand("gyro/override {gyro} {state:bool}", c => ...);
```

### What the inventory shows

1. **`rotor/lock` effectively always stops.** `GetOption("stop")` returns `""` when the option is absent, and `"" != null && "" != "false"` is true, so `stop` is true unless the player writes `--stop=false`. `Lock` also defaults `stopOnLock` to true. A plain `{--stop}` flag would flip the default to false, so the signature keeps today's behaviour with `{--stop=true:bool}`. Confirm that is what players expect.
2. **Four registered commands throw on missing input today**: `light/color` and `sorter/drain` read `Arguments[1]` behind a zero-count check, `tag/set` reads `Arguments[0]` and `[1]` unguarded, and `print` reads `Arguments[0]` behind `minArgs` 0. `tag/get` with no argument prints `tag: #` for a null tag. A signature turns each into a usage line.
3. **Multi-word text arguments need quotes.** Today `screen/print Lcd Hello World` prints `Hello` and drops the rest. Strict mode rejects it, which is the right outcome but is visible. Affected: `screen/print`, `print`, `view/notify`, `sound/*`, `block/rename`, `tag/set`.
4. **Same name, different script.** `screen/*` exists in OS (`print`, `color`, `bgcolor`) and GUI (`content`, `script`, `scripts`) and `gyro/override` in OS and Autopilot (both unregistered). Names do not collide today, and the signature does not change that.
5. **Wildcard and choice.** `{--*}` (1 command: `autolcd/print`, decided) and `a|b|c` (5 uses: `sorter/mode`, `sorter/add --mode`, `screen/content`, `view/notify --level`, `calibrate`; `light/blink` presets could use it too; still open) come from this list, not from the Laravel model. Together about 80 packaged characters (estimated, included in Size below).
6. **Most commands are simple.** The 40 block shorthand commands need nothing new, and most class commands are a block name plus one or two typed values and the shared `--add` / `--sub` / `--share` options. The few that stay mostly handler code are `calibrate`, `light/blink`, `screen/print`, `autolcd/*` and the sorter commands.

## Size

This is the real risk. The parser, `CommandInput` and `SignatureCommand` are Core code, so they ship in OS, GUI and MAPS.

### Estimate (2026-10-25)

Method: measured packaged sizes in the newest `MotherOS\Script.cs` (97,094 characters, UTF-8), where each command class was located by its name literal. Then wrote the replacement for one representative of each command shape in the same packaged style (one-character identifiers, no whitespace) and counted it, and extrapolated to the siblings. Nothing was built. Treat it as an estimate, plus or minus 25%.

**What the old form costs today (measured).** 39 of the 43 OS command classes were found, totalling 14,849 characters, about 380 each, plus about 17 for the `RegisterCommand(new X(this))` line in `Boot`. (The other four are the two limit classes, which build their name at run time, the sorter base class, and the unregistered `gyro/override` class.) Packaged, a class is already compact. The fixed overhead per class (class header, module field, constructor, `Name`, `Execute` declaration) is about 95 characters, the guards and `Arguments[n]` reads another 50 to 80, and a block lookup with its `BlockNotFound` return about 60. The command logic itself does not shrink.

**Representatives, old (class plus registration line) versus new (signature plus handler):**

| Command | Old | New | Saved |
|---|---|---|---|
| `piston/distance` (3 option flags, parse, share) | 538 | 238 | 300 |
| `piston/speed` | 433 | 180 | 253 |
| `wheel/power` | 367 | 90 | 277 |
| `rotor/lock` | 315 | 87 | 228 |
| `sound/set` | 323 | 79 | 244 |
| `block/rename` | 296 | 74 | 222 |
| `timer/start` | 377 | 114 | 263 |
| `pb/run` | 354 | 90 | 264 |
| `sorter/mode` | 344 | 136 | 208 |
| `screen/bgcolor` | 281 | 117 | 164 |
| `dampeners/on` (optional target, two lookups) | 332 | 188 | 144 |
| `light/color` | 410 | 279 | 131 |
| `light/blink` (most complex) | 708 | 551 | 157 |
| `tag/get` (no block lookup) | 299 | 191 | 108 |

The saving is largest where a command is a thin wrapper around one block type (about 220 to 300 characters) and smallest where the body is real logic or needs two block types (about 110 to 160). Commands with options keep their signature text, which costs 30 to 70 characters.

**OS total, 43 classes:**

| Group | Classes | Saved |
|---|---|---|
| Mechanical distance (`piston/distance`, `rotor/rotate`, `hinge/rotate`) | 3 | about 900 |
| Mechanical speed (piston, rotor, hinge) | 3 | about 760 |
| Mechanical limits (`ulimit`/`llimit`, piston and stator) | 2 | about 440 |
| One value on one block type (`wheel/*` x4, `sound/set`, `block/rename`) | 6 | about 1,570 |
| `rotor/lock` | 1 | about 230 |
| Cockpit pairs (`dampeners`, `handbrake`) | 4 | about 580 |
| Lights (`reset`, `color`, `intensity`, `blink`) | 4 | about 600 |
| Screen colours | 2 | about 330 |
| Sorter (`mode`, `add`, `remove`, shared base 769) | 4 | about 710 (`add` / `remove` / base is the roughest figure) |
| Misc (`timer/start`, `pb/run`, 11 others at about 140 each) | 13 | about 2,070 |
| **Gross** | **42 packaged** | **about 8,150** (range 6,400 to 9,350) |

The unregistered `gyro/override` class is the 43rd and is not packaged, so it saves nothing in the script.

**Core cost (new code, packaged style, counted):** token class 49, signature parse 492, bind and validate 555, `CommandInput` 474, `BlockSet.Command(sig, handler)` 155, three `RegisterCommand(sig, handler)` accessors 165, plus about 80 for the `{--*}` wildcard (about 20) and the `a|b|c` choice (about 60), estimated, not written out. Total added **about 1,970**.

**Core removed (measured):** `LambdaCommand` 218, `BlockCommand<T>` 309, the old `RegisterCommand(name, minArgs, ...)` accessors (3) about 240, `RegisterBlockCommand` about 110, `GetIncrement` / `GetDistributedValue` / `IsShared` about 260 (they move into `CommandInput`). Total removed **about 1,140**.

**Core's own commands:** `purge` (751) and `rename` (378) become registrations, saving about 350 together, and the eight lambdas lose their `minArgs` and in-lambda guards, about 120.

**GUI (measured, then estimated):** its 9 commands are classes, not lambdas, 2,463 packaged characters (plus about 17 each for the registration line). Replacing them with signatures saves about 1,300: the four one-line `view/*` commands about 150 each, `view/go` 110, `view/notify` 170 (the level mapping stays), `screen/content`, `screen/script`, `screen/scripts` 120 to 150 each.

**Autopilot (measured, then estimated):** 10 command classes, 3,266 packaged characters. `yaw`, `pitch` and `roll` save about 190 each, `propulsion/thrust` 190, `dock` 150, `calibrate` 200 (its 1,207 characters are mostly logic that stays), the rest 90 to 110. About 1,500 in total.

| | Characters |
|---|---|
| Core: added | +1,970 |
| Core: removed | -1,140 |
| Core: own commands migrated | -470 |
| **Core net (ships in every script)** | **about +360** |
| **Mother OS net** | **about -8,150 plus 360, so about -7,800** (97.1k to about 89.3k) |
| Mother GUI net | about +360 less about 1,300, so about -900 (97.8k to about 96.9k; headroom 825 today) |
| Autopilot net | about +360 less about 1,500, so about -1,150 (95.6k to about 94.4k; headroom 3,743) |

Core's +360 is the cost of the parser. It is more than paid back in every script, but only once that script's commands have moved over. Until then each script grows by the Core net: for GUI that is +360 against 825 of headroom, so migrate GUI in the same change as the parser.

`BaseModuleCommand` (232) and the `TerminalCommand` option helpers can only be deleted once OS, GUI and Autopilot have no class commands. The table assumes the helpers are gone (they are in the 1,140). Counting `BaseModuleCommand`, Core net falls to about +130.

Not counted, further upside: the four cockpit commands collapse to one helper called four times, and the piston/rotor/hinge sets share one generic mechanical helper. Not counted, further cost: `help <name>` (about 70).

### Constraints

- **Net near zero in Core is the rule.** `SignatureCommand` replaces `LambdaCommand` and `BlockCommand<T>` (both deleted) and Core's own commands migrate in the same change: `help`, `halt`, `boot`, `clear`, `print`, `set`, `get`, `var/set`, `ping`, `rename`, `purge`.
- **GUI has 825 characters of headroom.** It must not take the parser alone. Land the parser together with GUI's `ViewModule` and `DisplayExtensionModule` command migrations (9 class commands) and measure.
- **Verify with the pilot.** Migrate `piston/distance`, `rotor/rotate`, `hinge/rotate`, run the build, and read the `ScriptOptimizer: a -> b characters` line before and after. Replace the estimate above with that.
- Hand-write the tokenizer (no `Regex`), in the style of `SplitInputIntoTerms`. Build with C# 6 only. Parse at registration; measure boot cost since roughly 100 commands register at boot.

## Plan

0. **Baseline.** Rebuild OS, GUI and MAPS in Release and record the optimizer lines (as `core-size-reduction.md` asks). Decide the open questions.
1. **Parser and binding.** `CommandSignature` (parse, bind, usage), `CommandInput`, `SignatureCommand`. Unit tests first.
2. **Registration API.** `RegisterCommand(signature, handler)` on `BaseModule` and `Mother`. `BlockSet<T>.Command(signature, handler)` overload. Rebuild `BlockCommand<T>` on it.
3. **Core migration.** Move the Core commands, delete `LambdaCommand` and the `minArgs` overload, measure each script.
4. **Pilot in OS.** `piston/distance`, `rotor/rotate`, `hinge/rotate`. Parity tests against the old classes (same inputs, same output).
5. **Bulk OS and Autopilot migration**, in this order: mechanical (limits, speeds, angles), cockpit pairs, light, wheel, sorter (uses `*` and options), screen, terminal block. Collapse clones as they go.
6. **Cleanup.** Delete `BaseModuleCommand` helpers when unused (`GetIncrementalValue`, `GetDistributedValue`, `IsSharedMode`) and the matching `TerminalCommand` statics if nothing else calls them. Add `help <name>`. Update `README.md` and `CHANGELOG.md`.

Each step leaves every script building and under 100,000 characters. Step 5 also scans maintained blueprints for calls that depend on ignored extras (strict mode, section 6).

## Tests

- `CommandSignatureTests`: table of signatures and inputs. Required, optional, default, array, flag, value option, typed, bad type, missing, extra, unknown option, `--flag=false`, quoted values, negative numbers, invalid signature (rejected at registration).
- `SignatureCommandTests`: usage text, handler receives bound values, typed accessors.
- `BlockSetTests`: block resolved from the first token, `BlockNotFound`, shared and incremental helpers.
- Parity tests for each migrated command against its old class until the class is deleted. Include every option the old class accepted (must still pass) and a stray option and a stray argument (must now be rejected with the usage or `InvalidOption` message).
- A boot test that registers every module's commands so an invalid signature fails the test, not the game.
- Existing `FakeModuleCommand` and the `Harness/Command.cs` helper cover the fixtures.

## Open questions

1. ~~Strict or lenient?~~ Decided: strict. Unknown options and extra arguments are rejected (see 6).
2. **Types.** `int`, `float` (accepts `%`), `bool` and the `a|b|c` choice are in the design. Add a `color` type for `ColorHelper.GetColor` (`light/color`, `screen/color`, `screen/bgcolor`, `screen/print --color`)? Today an invalid color is only reported by `light/color`.
3. **Handler return shape for block commands.** Detail string formatted into `BlockUpdated` (as sketched), a full message, or both via a signature on the helper.
4. **Block target token.** Rely on position (first token is the block name), or mark it explicitly in the signature, for example `{piston:block}`?
5. **Signature descriptions.** Skip for budget reasons (recommended), or allow them and strip them in the `ScriptOptimizer` post-build step so they cost nothing packaged.
6. **Remote signatures.** Should `help <name>` for a command owned by another script ask that script, or only print the name?
7. **`--share=false`.** Fixing it (flags use `GetBoolFromString`) changes today's `ContainsKey` meaning. Accept?
8. ~~Wildcard~~ Decided: `{--*}` is in (design 8). **Choice** `a|b|c` is still open: accept it (5 uses, about 60 packaged characters), or keep the enum checks hand-written?
9. **`rotor/lock` default.** Deferred. Until decided, the migrated signature keeps today's behaviour, `{--stop=true:bool}`.
10. ~~Dead `gyro/override`~~ Decided for Autopilot: keep the command, leave the registration commented out while flight controls are in progress (see the Autopilot inventory). OS has the same unregistered class; not decided, so it is left as it is.
