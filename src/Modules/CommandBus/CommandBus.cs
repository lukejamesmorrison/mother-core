using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
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
            public const string CommandNotFound = "Command not found: {0}";
            /// <summary>
            /// Message indicating that no arguments were provided for a command.
            /// </summary>
            public const string NoArgumentsProvided = "No arguments provided";
            /// <summary>
            /// Message indicating that a command was executed successfully.
            /// </summary>
            public const string InvalidCommandFormat = "Invalid command format.";
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
        /// List of all registered commands from core modules, extension modules, 
        /// and custom commands defined by the player in CustomData.
        /// </summary>
        public readonly List<IModuleCommand> ModuleCommands = new List<IModuleCommand>();

        /// <summary>
        /// Registry of commands available on other Mother Core instances on the construct.
        /// Maps script EntityId to set of command names.
        /// </summary>
        public readonly Dictionary<long, HashSet<string>> ConstructCommands = new Dictionary<long, HashSet<string>>();

        /// <summary>
        /// Registry of important commands (prefixed with !) from other Mother Core instances.
        /// Maps script EntityId to set of important command names (without the ! prefix).
        /// </summary>
        public readonly Dictionary<long, HashSet<string>> ImportantConstructCommands = new Dictionary<long, HashSet<string>>();

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

            // Clear remote commands on boot
            ConstructCommands.Clear();
            ImportantConstructCommands.Clear();

            // Commands
            RegisterCommand(new HelpCommand(this));

            // Routes
            AddRoute("command", request => HandleIncomingCommandRequest(request, "REQ"));
            AddRoute("localcmd", request => HandleIncomingCommandRequest(request, "CREQ"));
        }

        /// <summary>
        /// Handles an incoming command request from another grid or construct instance.
        /// Extracts the command, logs it with the given prefix, executes it, 
        /// and returns an appropriate response.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="logPrefix">The log prefix to distinguish request origin (e.g. "REQ" or "CREQ").</param>
        /// <returns></returns>
        Response HandleIncomingCommandRequest(Request request, string logPrefix)
        {
            string command = request.BString("Command").Trim();
            string originName = request.HString("OriginName");

            Log.Info($"{logPrefix}: {originName}> {command}");
            Mother.Print($"{logPrefix}: {originName}> {command}", false);

            var ims = Mother.GetModule<IntergridMessageService>();

            if (!string.IsNullOrEmpty(command))
            {
                bool success = RunTerminalCommand(command);

                var status = success 
                    ? Response.ResponseStatusCodes.COMMAND_EXECUTED 
                    : Response.ResponseStatusCodes.ERROR;

                return ims.CreateResponse(request, status);
            }

            return ims.CreateResponse(request, Response.ResponseStatusCodes.ERROR);
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
        /// Runs a single or multiple routines depending on format.
        /// </summary>
        /// <param name="commandString"></param>
        /// <returns></returns>
        public bool RunTerminalCommand(string commandString)
        {
            if (commandString.Length > 0)
            {
                // Substitute variables before parsing
                commandString = Mother.SubstituteVariables(commandString);

                HandleRoutine(new TerminalRoutine(commandString));
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

            if (target == "self" || string.IsNullOrEmpty(target))
            {
                LaunchRoutineCoroutines(terminalRoutine);
            }
            else
            {
                // Remote routine handling
                terminalRoutine.Unpack(Mother.ConfigCommands);
                var printString = $"> @{target} {terminalRoutine.UnpackedRoutineString}";

                if (target == "*")
                    Mother.GetModule<IntergridMessageService>().SendRequestToAllFromRoutine(terminalRoutine);
                else
                    Mother.GetModule<IntergridMessageService>().SendRequestFromRoutine(target, terminalRoutine);

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
            {
                foreach (var group in routine.ParallelGroups)
                    Clock.AddCoroutine(ExecuteCommandGroupCoroutine(group));
            }
            else
            {
                Clock.AddCoroutine(ExecuteCommandGroupCoroutine(routine.Commands));
            }
        }

        /// <summary>
        /// Coroutine that executes a group of commands sequentially. Each command 
        /// is processed one at a time. If a command resolves to a config command, 
        /// its expanded steps are inlined into this coroutine so that wait 
        /// commands properly block subsequent steps. Parallel groups within
        /// expanded config commands are launched as separate coroutines.
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        IEnumerable<double> ExecuteCommandGroupCoroutine(List<TerminalCommand> commands)
        {
            foreach (var command in commands)
            {
                // Yield through each step produced by the command
                foreach (double wait in ExecuteCommandCoroutine(command))
                    yield return wait;
            }
        }

        /// <summary>
        /// Coroutine that executes a single command. If the command is a wait, 
        /// it yields the wait duration. If the command resolves to a config 
        /// command, the expanded routine is inlined — its steps are yielded 
        /// through directly so waits block correctly. If the expanded config 
        /// command contains parallel groups, each group is launched as a 
        /// separate coroutine. Module commands and other primitives are 
        /// executed immediately.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        IEnumerable<double> ExecuteCommandCoroutine(TerminalCommand command)
        {
            // Handle the "wait" command
            if (command.Name.ToLower() == "wait" && command.Arguments.Count > 0)
            {
                double waitTime;

                if (double.TryParse(command.Arguments[0], out waitTime))
                {
                    Mother.Print($"> wait {waitTime}");
                    yield return waitTime;
                }

                yield break;
            }

            // Resolve config commands — inline their expanded steps
            string configCommandValue = ResolveConfigCommand(command);

            if (configCommandValue != null)
            {
                string expanded = Mother.SubstituteVariables(configCommandValue);
                var routine = new TerminalRoutine(expanded);

                if (routine.HasParallelGroups)
                {
                    // Parallel groups within a config command are launched
                    // as separate coroutines
                    foreach (var group in routine.ParallelGroups)
                        Clock.AddCoroutine(ExecuteCommandGroupCoroutine(group));
                }
                else
                {
                    // Inline the expanded steps into this coroutine
                    foreach (double wait in ExecuteCommandGroupCoroutine(routine.Commands))
                        yield return wait;
                }

                yield break;
            }

            // Execute module commands and other primitives immediately
            ExecutePrimitiveCommand(command);
            yield return 0;
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
                        Mother.ConfigCommands[localName], command.Options);
                }
            }

            // Check if an important command exists on the construct (unless force local)
            // If so, don't resolve locally - let ExecutePrimitiveCommand delegate to construct
            if (!command.IsForceLocal && FindInstanceWithImportantCommand(command.Name) != 0)
                return null;

            // Standard config commands (check both with and without ! prefix)
            if (Mother.ConfigCommands.ContainsKey(command.Name))
                return Mother.SubstituteCommandParameters(
                    Mother.ConfigCommands[command.Name], command.Options);

            // Check for important command (! prefixed in config) - only reached if not on construct
            string importantKey = "!" + command.Name;
            if (Mother.ConfigCommands.ContainsKey(importantKey))
                return Mother.SubstituteCommandParameters(
                    Mother.ConfigCommands[importantKey], command.Options);

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
                    Mother.GetModule<IntergridMessageService>()
                        .SendConstructCommand(importantScriptId, commandString);
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
                    Mother.GetModule<IntergridMessageService>()
                        .SendConstructCommand(remoteScriptId, commandString);
                    return;
                }
            }

            Mother.Print(MessageFormatter.Format(Messages.CommandNotFound, command.CommandString), false);
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
