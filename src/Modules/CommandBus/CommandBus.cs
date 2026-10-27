using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IngameScript
{
    /// <summary>
    /// Named execution outcomes used for command-bus observability in tests.
    /// </summary>
    public enum CommandExecutionOutcome
    {
        // The module has been excuted.
        ModuleExecuted,
        // The command has been scheduled to wait.
        WaitScheduled,
        // The command has been delegated to another construct instance with an important command.
        DelegatedToImportantConstruct,
        // The command has been delegated to another construct instance with a non-important command.
        DelegatedToConstruct,
        // The command has been sent to a remote grid for execution.
        RemoteRoutineSent,
        // The command was not found in any module or construct instance.
        CommandNotFound,
    }

    /// <summary>
    /// The CommandBus is responsible for handling and executing commands for Mother's modules. 
    /// It can handle commands queued in time, as well as within a flight plan using the 
    /// Clock module. It is able to interpret a command/routine and execute it
    /// locally, or remotely if a target is provided for the routine.
    /// </summary>
    public class CommandBus : BaseCoreModule
    {
        /// <summary>
        /// Messages used by the CommandBus.
        /// </summary>
        public static class Messages
        {
            /// <summary>
            /// Message indicating that a command was not found.
            /// </summary>
            public const string CommandNotFound = "Command not found: {0}",
            /// <summary>
            /// Message indicating that no arguments were provided for a command.
            /// </summary>
            NoArgumentsProvided = "No arguments provided",
            /// <summary>
            /// Message indicating that a command was executed successfully.
            /// </summary>
            InvalidCommandFormat = "Invalid command format.",
            /// <summary>
            /// Printed when a step fails and its routine stops. {0} is the step
            /// as written, {1} is why it failed.
            /// </summary>
            CommandFailed = "Command failed: {0} ({1})";
        }

        /// <summary>
        /// Shared by the steps of one coroutine so a failed step can stop it.
        /// Each coroutine launched from a parallel group has its own state, so
        /// groups stay independent.
        /// </summary>
        class RoutineState
        {
            /// <summary>
            /// Set by a failing step. Every enclosing loop stops when it is set.
            /// </summary>
            public bool Failed;

            /// <summary>
            /// The open if blocks, innermost last. Each is a set of flags:
            /// 1 the current branch runs, 2 a branch already ran or the block
            /// sits in a skipped parent (else will not run), 4 else was seen.
            /// </summary>
            public readonly List<int> Ifs = new List<int>();

            /// <summary>
            /// False while any open if block is in a branch that does not run.
            /// </summary>
            public bool Running
            {
                get
                {
                    foreach (var flags in Ifs)
                        if ((flags & 1) == 0)
                            return false;

                    return true;
                }
            }
        }
        
        /// <summary>
        /// The Clock core module.
        /// </summary>
        Clock Clock;

        /// <summary>
        /// The Log core module.
        /// </summary>
        Log Log;

        /// <summary>
        /// The IntergridMessageService core module.
        /// </summary>
        IntergridMessageService IMS;

        /// <summary>
        /// List of all registered commands from core modules, extension modules, 
        /// and custom commands defined by the player in CustomData.
        /// </summary>
        public readonly List<IModuleCommand> ModuleCommands = new List<IModuleCommand>();

        /// <summary>
        /// Registry of commands available on other Mother Core instances on the construct.
        /// Maps script EntityId to set of command names.
        /// </summary>
        public readonly Dictionary<long, HashSet<string>> ConstructCommands = new Dictionary<long, HashSet<string>>(),

        /// <summary>
        /// Registry of important commands (prefixed with !) from other Mother Core instances.
        /// Maps script EntityId to set of important command names (without the ! prefix).
        /// </summary>
        ImportantConstructCommands = new Dictionary<long, HashSet<string>>();

        /// <summary>
        /// Execution counts for each command.
        /// </summary>
        readonly Dictionary<string, int> ExecutionCounts = new Dictionary<string, int>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public CommandBus(Mother mother) : base(mother) { }

        /// <summary>
        /// Boot the module. We register a route so that other grids may send 
        /// commands to this grid, reference other modules, and register
        /// commands.
        /// </summary>
        public override void Boot()
        {
            // Modules
            Clock = Mother.GetModule<Clock>();
            Log = Mother.GetModule<Log>();
            IMS = Mother.GetModule<IntergridMessageService>();

            // Clear remote commands on boot
            ConstructCommands.Clear();
            ImportantConstructCommands.Clear();
            ExecutionCounts.Clear();

            // Commands
            RegisterCommand("help", 0, c =>
            {
                var sb = new StringBuilder();

                ModuleCommands.ForEach(moduleCommand => {
                    sb.Append(moduleCommand.GetCommandName())
                    .Append('\n');
                });

                return $"{sb}";
            });

            RegisterCommand("halt", 0, c =>
            {
                Clock.Halt();

                return "Halted.";
            });

            // Routes
            // External commands from remote grids - relay handles routing
            AddRoute("command", request => HandleExternalCommandRequest(request));
            // Internal commands from construct instances - execute directly
            AddRoute("localcmd", request => HandleIncomingCommandRequest(request));
        }

        /// <summary>
        /// Handles an incoming command request from a remote grid.
        /// Only the relay processes these - it either executes locally or forwards
        /// to the correct construct instance via localcmd.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Response HandleExternalCommandRequest(Request request)
        {
            // Only relay handles external commands
            if (!IMS.IsRelay)
                return null;
            
            string commandStr = request.BString(Literals.Command).Trim();
            
            if (string.IsNullOrEmpty(commandStr))
                return IMS.CreateResponse(request, Response.ResponseStatusCodes.ERROR);
            
            LogCommand("REQ", request.HString(Literals.OriginName), commandStr);
            
            // Check if another construct instance owns this command (important commands first)
            var terminalCommand = new TerminalCommand(commandStr);
            long targetScriptId = FindInstanceWithImportantCommand(terminalCommand.Name);

            if (targetScriptId == 0)
                targetScriptId = FindInstanceWithCommand(terminalCommand.Name);
            
            if (targetScriptId != 0)
            {
                IMS.SendConstructCommand(targetScriptId, commandStr);
                return IMS.CreateResponse(request, Response.ResponseStatusCodes.COMMAND_EXECUTED);
            }
            
            // Relay owns the command - execute locally
            return ExecuteAndRespond(request, commandStr);
        }

        /// <summary>
        /// Handles an incoming command request from another construct instance.
        /// Extracts the command, logs it, executes it, and returns a response.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Response HandleIncomingCommandRequest(Request request)
        {
            string command = request.BString(Literals.Command).Trim();

            if (string.IsNullOrEmpty(command))
                return IMS.CreateResponse(request, Response.ResponseStatusCodes.ERROR);

            LogCommand("CREQ", request.HString(Literals.OriginName), command);

            return ExecuteAndRespond(request, command);
        }

        /// <summary>
        /// Executes a command and returns the appropriate response.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        Response ExecuteAndRespond(Request request, string command)
        {
            bool success = RunTerminalCommand(command);
            var status = success
                ? Response.ResponseStatusCodes.COMMAND_EXECUTED
                : Response.ResponseStatusCodes.ERROR;

            return IMS.CreateResponse(request, status);
        }

        /// <summary>
        /// Logs a command with a prefix and origin name.
        /// </summary>
        void LogCommand(string prefix, string originName, string command)
        {
            var line = $"{prefix}: {originName}> {command}";

            Log.Info(line);
            Mother.Print(line, false);
        }

        /// <summary>
        /// Registers a module command with the CommandBus. Mother exposes 
        /// this method to modules to simplify command registration.
        /// </summary>
        /// <param name="command"></param>
        new public void RegisterCommand(IModuleCommand command)
        {
            ModuleCommands.Add(command);
        }

        /// <summary>
        /// Registers a lambda command with the CommandBus. Declared here so the
        /// command goes straight to <see cref="ModuleCommands"/> instead of the
        /// module's own command list.
        /// </summary>
        /// <param name="name">The command name.</param>
        /// <param name="minArgs">Fewer arguments than this are answered with <paramref name="usage"/>.</param>
        /// <param name="run">Runs the command and returns the response text.</param>
        /// <param name="usage">Response for too few arguments. Defaults to <see cref="Messages.NoArgumentsProvided"/>.</param>
        new public void RegisterCommand(string name, int minArgs, Func<TerminalCommand, string> run, string usage = null)
        {
            RegisterCommand(new LambdaCommand(name, minArgs, run, usage));
        }

        /// <summary>
        /// Runs a single or multiple routines depending on format.
        /// </summary>
        /// <param name="commandString"></param>
        /// <returns></returns>
        public bool RunTerminalCommand(string commandString)
        {
            if (!string.IsNullOrWhiteSpace(commandString))
            {
                var routine = new TerminalRoutine(commandString);

                // Variables in a local routine are resolved as each step runs.
                // A remote routine never runs here, so the typed text is resolved
                // now. Config command bodies stay raw for the remote to resolve.
                if (routine.Target != Literals.Self)
                    routine = new TerminalRoutine(Mother.SubstituteVariables(commandString));

                HandleRoutine(routine);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processes and executes a routine. If the routine contains parallel
        /// groups, each group is launched as a separate coroutine for concurrent
        /// execution. Otherwise, all commands in the routine are executed
        /// sequentially within a single coroutine. Individual commands within
        /// the sequence may themselves expand to parallel groups when resolved
        /// as config commands.
        /// </summary>
        /// <param name="terminalRoutine"></param>
        void HandleRoutine(TerminalRoutine terminalRoutine)
        {
            var target = terminalRoutine.Target;

            // Ensure Clock is available for coroutine execution
            if (Clock == null)
                Clock = Mother.GetModule<Clock>();

            if (target == Literals.Self || string.IsNullOrEmpty(target))
                LaunchRoutineCoroutines(terminalRoutine);
            else
            {
                // Remote routine handling
                terminalRoutine.Unpack(Mother.ConfigCommands);
                var printString = $"> @{target} {terminalRoutine.UnpackedRoutineString}";

                CountExecution(CommandExecutionOutcome.RemoteRoutineSent, string.Empty);

                if (target == "*")
                    IMS.SendRequestToAllFromRoutine(terminalRoutine);
                else
                    IMS.SendRequestFromRoutine(target, terminalRoutine);

                Log.Info(printString);
                Mother.Print(printString);
            }
        }

        /// <summary>
        /// Launches coroutines for a routine's commands. If the routine has
        /// parallel groups, each group runs as a separate coroutine. Otherwise,
        /// all commands are executed sequentially within a single coroutine.
        /// Individual commands within the sequence may themselves expand to
        /// parallel groups when resolved as config commands.
        /// </summary>
        /// <param name="routine"></param>
        void LaunchRoutineCoroutines(TerminalRoutine routine)
        {
            if (routine.HasParallelGroups)
                LaunchParallelGroups(routine, true);
            else
                Clock.AddCoroutine(ExecuteCommandGroupCoroutine(routine.Commands, new RoutineState(), true));
        }

        /// <summary>
        /// Launches each parallel group of a routine as its own coroutine with
        /// its own state, so a failure in one group never stops another.
        /// </summary>
        /// <param name="routine"></param>
        /// <param name="resolve">Whether the steps still need variable resolution.</param>
        void LaunchParallelGroups(TerminalRoutine routine, bool resolve)
        {
            foreach (var group in routine.ParallelGroups)
                Clock.AddCoroutine(ExecuteCommandGroupCoroutine(group, new RoutineState(), resolve));
        }

        /// <summary>
        /// Coroutine that executes a group of commands sequentially. Each command 
        /// is processed one at a time. If a command resolves to a config command, 
        /// its expanded steps are inlined into this coroutine so that wait 
        /// commands properly block subsequent steps. Parallel groups within
        /// expanded config commands are launched as separate coroutines. Stops
        /// as soon as a step marks the state as failed.
        /// 
        /// The if, else and end steps are handled here. Steps in a branch that
        /// does not run are skipped before anything about them is resolved. An
        /// if block still open when the group ends is closed, so a called command
        /// never leaves a block open for its caller.
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="state"></param>
        /// <param name="resolve">False when the steps are already resolved.</param>
        /// <returns></returns>
        IEnumerable<double> ExecuteCommandGroupCoroutine(List<TerminalCommand> commands, RoutineState state, bool resolve)
        {
            // Blocks below this belong to the caller
            int floor = state.Ifs.Count;

            foreach (var command in commands)
            {
                string name = command.Name;

                if (name == "if" || name == "elseif" || name == "else" || name == "end")
                {
                    Func<string> poll;
                    string error = RunIfStep(command, state, floor, resolve, out poll);

                    // A condition that needs another script's answer pauses the routine, not the script
                    while (error == null && poll != null)
                    {
                        string answer = poll();

                        if (answer == null)
                            yield return 0;
                        else if (answer == "1" || answer == "0")
                        {
                            ApplyIf(name, state.Ifs, answer == "1");
                            poll = null;
                        }
                        else
                            error = answer;
                    }

                    if (error != null)
                        Fail(state, command, error);
                }
                else if (state.Running)
                {
                    // Yield through each step produced by the command
                    foreach (double wait in ExecuteCommandCoroutine(command, state, resolve))
                        yield return wait;
                }

                if (state.Failed)
                    break;
            }

            state.Ifs.RemoveRange(floor, state.Ifs.Count - floor);
        }

        /// <summary>
        /// Handles an if, else or end step.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="state"></param>
        /// <param name="floor">How many open blocks belong to the caller, and cannot be closed here.</param>
        /// <param name="resolve">Whether the step still needs variable resolution.</param>
        /// <param name="poll">Set when the condition waits for a reply from another script. Poll it until it has the result, then call ApplyIf.</param>
        /// <returns>Why the step failed, or null.</returns>
        string RunIfStep(TerminalCommand command, RoutineState state, int floor, bool resolve, out Func<string> poll)
        {
            var ifs = state.Ifs;
            int top = ifs.Count - 1;
            string name = command.Name;
            poll = null;

            if (name == "if")
            {
                // The condition of a skipped if is never evaluated
                if (!state.Running)
                {
                    ifs.Add(2);
                    return null;
                }
            }
            else if (top < floor)
                return name + " without if";
            else if (name == "end")
            {
                ifs.RemoveAt(top);
                return null;
            }
            else if ((ifs[top] & 4) != 0)
                return name + " after else";
            else if (name == "else")
            {
                ifs[top] = (ifs[top] & 2) == 0 ? 5 : 6;
                return null;
            }
            else if ((ifs[top] & 2) != 0)
            {
                // A branch already ran, or the parent is skipped: elseif is not evaluated
                ifs[top] = 2;
                return null;
            }

            TerminalRoutine body;
            string error = resolve ? ResolveStep(ref command, out body) : null;

            if (error != null)
                return error;

            int result = Mother.GetModule<ConditionModule>().Begin(command, out error, out poll);

            if (error == null && poll == null)
                ApplyIf(name, ifs, result > 0);

            return error;
        }

        /// <summary>
        /// Opens an if block, or sets the branch of an elseif, from the result
        /// of its condition. The block is the top one, as nothing else changes
        /// the stack while a condition waits.
        /// </summary>
        void ApplyIf(string name, List<int> ifs, bool result)
        {
            if (name == "if")
                ifs.Add(result ? 3 : 0);
            else
                ifs[ifs.Count - 1] = result ? 3 : 0;
        }

        /// <summary>
        /// Fails the routine and prints why.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="command"></param>
        /// <param name="error"></param>
        void Fail(RoutineState state, TerminalCommand command, string error)
        {
            state.Failed = true;

            // Print also writes to the log
            Mother.Print(MessageFormatter.Format(Messages.CommandFailed, command.CommandString, error), false);
        }

        /// <summary>
        /// Coroutine that executes a single command. Variables are resolved
        /// first, so the name, wait time, options and command string are all
        /// current. If the command is a wait, it yields the wait duration. If
        /// the command resolves to a config command, the expanded routine is
        /// inlined - its steps are yielded through directly so waits block
        /// correctly. If the expanded config command contains parallel groups,
        /// each group is launched as a separate coroutine. Module commands and
        /// other primitives are executed immediately.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="state"></param>
        /// <param name="resolve">False when the step is already resolved.</param>
        /// <returns></returns>
        IEnumerable<double> ExecuteCommandCoroutine(TerminalCommand command, RoutineState state, bool resolve)
        {
            if (resolve)
            {
                TerminalRoutine body;
                string error = ResolveStep(ref command, out body);

                if (error != null)
                {
                    Fail(state, command, error);
                    yield break;
                }

                if (body != null)
                {
                    // Steps from a resolved value are not resolved a second time
                    foreach (double wait in RunBody(body, state, false))
                        yield return wait;

                    yield break;
                }
            }

            // Handle the "wait" command
            if (command.Name.ToLower() == Literals.Wait && command.Arguments.Count > 0)
            {
                double waitTime;

                if (double.TryParse(command.Arguments[0], out waitTime))
                {
                    Mother.Print($"> {Literals.Wait} {waitTime}");
                    CountExecution(CommandExecutionOutcome.WaitScheduled, command.Name);
                    yield return waitTime;
                }

                yield break;
            }

            // Resolve config commands - inline their expanded steps
            string configCommandValue = ResolveConfigCommand(command);

            if (configCommandValue != null)
            {
                var routine = new TerminalRoutine(configCommandValue);

                foreach (double wait in RunBody(routine, state, true))
                    yield return wait;

                yield break;
            }

            // Execute module commands and other primitives immediately
            ExecutePrimitiveCommand(command);
            yield return 0;
        }

        /// <summary>
        /// Runs a routine inside the current coroutine. Parallel groups are
        /// launched as separate coroutines, otherwise the steps are inlined so
        /// that waits block the caller.
        /// </summary>
        /// <param name="routine"></param>
        /// <param name="state"></param>
        /// <param name="resolve">Whether the steps still need variable resolution.</param>
        /// <returns></returns>
        IEnumerable<double> RunBody(TerminalRoutine routine, RoutineState state, bool resolve)
        {
            if (routine.HasParallelGroups)
                LaunchParallelGroups(routine, resolve);
            else
                foreach (double wait in ExecuteCommandGroupCoroutine(routine.Commands, state, resolve))
                    yield return wait;
        }

        /// <summary>
        /// Resolves variables in a step immediately before it runs. A step with
        /// no variables is left unchanged, as the same instance. Otherwise the
        /// resolved text either replaces the command, or, when it holds no steps,
        /// several steps or parallel groups, is returned as a routine to run in
        /// its place.
        /// </summary>
        /// <param name="command">The step. Replaced when it resolves to one command.</param>
        /// <param name="routine">The routine to run instead, or null.</param>
        /// <returns>Why the step failed, or null.</returns>
        string ResolveStep(ref TerminalCommand command, out TerminalRoutine routine)
        {
            routine = null;

            string resolved, error;

            if (command.CommandString.IndexOf('$') < 0)
                return null;

            if (!Mother.TrySubstituteVariables(command.CommandString, out resolved, out error))
                return error;

            try
            {
                var parsed = new TerminalRoutine(resolved);

                if (parsed.Target != Literals.Self)
                    return "a variable cannot send a step to another grid (@" + parsed.Target + ")";

                if (parsed.HasParallelGroups || parsed.Commands.Count != 1)
                    routine = parsed;
                else
                    command = parsed.Commands[0];
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return null;
        }

        /// <summary>
        /// Resolves a command to its config command value if one exists. 
        /// Handles underscore-prefixed local commands and standard config 
        /// commands, applying parameter substitution.
        /// 
        /// Important commands (! prefixed) on the construct take priority over
        /// local config commands, unless the command is force local (!! prefix).
        /// </summary>
        /// <param name="command"></param>
        /// <returns>The expanded config command string, or null if not a config command.</returns>
        string ResolveConfigCommand(TerminalCommand command)
        {
            string commandString = command.CommandString;

            // Underscore-prefixed local commands
            if (commandString.StartsWith("_"))
            {
                string localName = commandString.Substring(1);

                if (Mother.ConfigCommands.ContainsKey(localName))
                {
                    Mother.Print($"Executing local command: {localName}", false);
                    return Mother.SubstituteCommandParameters(
                        Mother.ConfigCommands[localName], command.Options, false);
                }
            }

            // Check if an important command exists on the construct (unless force local)
            // If so, don't resolve locally - let ExecutePrimitiveCommand delegate to construct
            if (!command.IsForceLocal && FindInstanceWithImportantCommand(command.Name) != 0)
                return null;

            // Standard config commands (check both with and without ! prefix)
            if (Mother.ConfigCommands.ContainsKey(command.Name))
                return Mother.SubstituteCommandParameters(
                    Mother.ConfigCommands[command.Name], command.Options, false);

            // Check for important command (! prefixed in config) - only reached if not on construct
            string importantKey = "!" + command.Name;
            if (Mother.ConfigCommands.ContainsKey(importantKey))
                return Mother.SubstituteCommandParameters(
                    Mother.ConfigCommands[importantKey], command.Options, false);

            return null;
        }

        /// <summary>
        /// Executes a primitive (non-config, non-wait) command. This includes 
        /// module commands, construct commands, and unresolved commands.
        /// 
        /// Command priority:
        /// 1. If command has !! prefix (IsForceLocal), always run locally
        /// 2. If an important command (! prefix) exists on construct, delegate to that instance
        /// 3. Otherwise, run locally if available, or delegate to construct
        /// </summary>
        /// <param name="command"></param>
        void ExecutePrimitiveCommand(TerminalCommand command)
        {
            string commandString = command.CommandString;

            // Check for important commands on the construct first (unless force local)
            if (!command.IsForceLocal)
            {
                long importantScriptId = FindInstanceWithImportantCommand(command.Name);
                if (importantScriptId != 0)
                {
                    CountExecution(CommandExecutionOutcome.DelegatedToImportantConstruct, command.Name);
                    IMS.SendConstructCommand(importantScriptId, commandString);
                    return;
                }
            }

            // Execute a command registered by a module
            foreach (IModuleCommand moduleCommand in ModuleCommands)
            {
                if (moduleCommand.GetCommandName() == command.Name)
                {
                    var printString = "> " + commandString;

                    Mother.Print(printString);

                    string output = moduleCommand.Execute(command);

                    CountExecution(CommandExecutionOutcome.ModuleExecuted, command.Name);

                    Mother.Print(output, false);

                    return;
                }
            }

            // Check if command exists on the construct (non-important) - skip if force local
            if (!command.IsForceLocal)
            {
                long remoteScriptId = FindInstanceWithCommand(command.Name);
                if (remoteScriptId != 0)
                {
                    CountExecution(CommandExecutionOutcome.DelegatedToConstruct, command.Name);
                    IMS.SendConstructCommand(remoteScriptId, commandString);
                    return;
                }
            }

            CountExecution(CommandExecutionOutcome.CommandNotFound, command.Name);
            Mother.Print(MessageFormatter.Format(Messages.CommandNotFound, command.CommandString), false);
        }

        /// <summary>
        /// Get the execution count of a specific command.
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="outcome"></param>
        /// <returns></returns>
        public int GetExecutionCount(
            string commandName,
            CommandExecutionOutcome outcome = CommandExecutionOutcome.ModuleExecuted)
        {
            int count;
            return ExecutionCounts.TryGetValue(GetExecutionKey(outcome, commandName), out count)
                ? count
                : 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outcome"></param>
        /// <param name="commandName"></param>
        void CountExecution(CommandExecutionOutcome outcome, string commandName)
        {
            string key = GetExecutionKey(outcome, commandName);

            if (ExecutionCounts.ContainsKey(key))
                ExecutionCounts[key]++;
            else
                ExecutionCounts[key] = 1;
        }

        /// <summary>
        /// Get the execution key based on the command name and outcome.
        /// </summary>
        /// <param name="outcome"></param>
        /// <param name="commandName"></param>
        /// <returns></returns>
        string GetExecutionKey(CommandExecutionOutcome outcome, string commandName)
        {
            return (int)outcome + "|" + (commandName ?? string.Empty);
        }

        /// <summary>
        /// Gets the list of command names from this instance for sharing with other scripts.
        /// </summary>
        /// <returns></returns>
        public List<string> GetSelfCommandNames()
        {
            var names = new List<string>(ModuleCommands.Count + Mother.ConfigCommands.Count);
            
            for (int i = 0; i < ModuleCommands.Count; i++)
                names.Add(ModuleCommands[i].GetCommandName());
            
            foreach (var key in Mother.ConfigCommands.Keys)
                names.Add(key);
            
            return names;
        }

        /// <summary>
        /// Registers commands from a remote script. Commands prefixed with ! are
        /// stored separately as important commands and take priority over local commands.
        /// </summary>
        /// <param name="scriptId">The EntityId of the remote script.</param>
        /// <param name="commands">List of command names available on that script.</param>
        public void RegisterRemoteCommands(long scriptId, List<string> commands)
        {
            if (scriptId == Mother.Id) return;
            
            var normalCommands = new HashSet<string>();
            var importantCommands = new HashSet<string>();

            commands.ForEach(cmd =>
            {
                if (cmd.StartsWith("!"))
                    // Store without the ! prefix
                    importantCommands.Add(cmd.Substring(1));
                else
                    normalCommands.Add(cmd);
            });
            
            ConstructCommands[scriptId] = normalCommands;
            ImportantConstructCommands[scriptId] = importantCommands;
        }

        /// <summary>
        /// Finds the script that has a specific command (non-important).
        /// Returns 0 if not found.
        /// </summary>
        /// <param name="commandName"></param>
        /// <returns></returns>
        public long FindInstanceWithCommand(string commandName)
        {
            foreach (var entry in ConstructCommands)
            {
                if (entry.Value.Contains(commandName))
                    return entry.Key;
            }
            return 0;
        }

        /// <summary>
        /// Finds the script that has a specific important command (! prefixed).
        /// Returns 0 if not found.
        /// </summary>
        /// <param name="commandName"></param>
        /// <returns></returns>
        public long FindInstanceWithImportantCommand(string commandName)
        {
            foreach (var entry in ImportantConstructCommands)
                if (entry.Value.Contains(commandName))
                    return entry.Key;

            return 0;
        }
    }
}
