using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    partial class Program
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
                public const string CommandNotFound = "Command not found: {0}";
                public const string NoArgumentsProvided = "No arguments provided";
                public const string InvalidCommandFormat = "Invalid command format.";
            }

            /// <summary>
            /// The Mother instance.
            /// </summary>
            //readonly Mother Mother;

            /// <summary>
            /// The Clock core module.
            /// </summary>
            Clock Clock;

            /// <summary>
            /// The Log core module.
            /// </summary>
            Log Log;

            /// <summary>
            /// The WaypointRoutineQueue instance. Used to hold commands 
            /// queued for execution within a flight plan.
            /// </summary>
            public WaypointRoutineQueue WaypointRoutineQueue;

            /// <summary>
            /// List of all registered commands from core modules, extension modules, 
            /// and custom commands defined by the player in CustomData.
            /// </summary>
            readonly List<IModuleCommand> ModuleCommands = new List<IModuleCommand>();

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="mother"></param>
            public CommandBus(Mother mother) : base(mother)
            {
                //Mother = mother;
                WaypointRoutineQueue = new WaypointRoutineQueue();  
            }

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

                // Commands
                RegisterCommand(new HelpCommand(this));

                // Routes
                Mother.GetModule<IntergridMessageService>().Router.AddRoute("command", request => HandleIncomingCommandRequest(request));
            }

            /// <summary>
            /// Handles incoming command requests from other grids. This endpoint 
            /// is used to receive incoming commands and run them locally.
            /// </summary>
            /// <param name="request"></param>
            /// <returns></returns>
            public Response HandleIncomingCommandRequest(Request request)
            {
                string command = request.BString("Command").Trim();
                string originName = request.HString("OriginName");

                Mother.GetModule<Log>().Info($"REQ: {originName}> {command}");
                Mother.Print($"REQ: {originName}> {command}", false);

                if (command != null)
                {
                    bool success = Mother.GetModule<CommandBus>().RunTerminalCommand(command);

                    Response.ResponseStatusCodes status = success 
                        ? Response.ResponseStatusCodes.COMMAND_EXECUTED 
                        : Response.ResponseStatusCodes.ERROR;

                    return Mother.GetModule<IntergridMessageService>().CreateResponse(request, status);
                }

                return null;
            }

            /// <summary>
            /// Registers a module command with the CommandBus. Mother exposes 
            /// this method to modules to simplify command registration.
            /// </summary>
            /// <param name="command"></param>
            public void RegisterCommand(IModuleCommand command)
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
                    HandleRoutine(new TerminalRoutine(commandString));
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Processes and executes a routine (multiple routines in a sequence).
            /// </summary>
            /// <param name="terminalRoutine"></param>
            public void HandleRoutine(TerminalRoutine terminalRoutine)
            {
                var target = terminalRoutine.Target;
                var commands = terminalRoutine.Commands;

                if (target == "self" || string.IsNullOrEmpty(target))
                {
                    ProcessCommandsSequentially(commands);
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
            /// Processes a list of commands sequentially, with an optional initial delay.
            /// </summary>
            /// <param name="commands"></param>
            /// <param name="initialDelay"></param>
            private void ProcessCommandsSequentially(List<TerminalCommand> commands, double initialDelay = 0)
            {
                if (commands == null || commands.Count == 0) return;

                TerminalCommand currentCommand = commands[0];
                List<TerminalCommand> remainingCommands = commands.Skip(1).ToList();

                Action executeCurrentCommand = () =>
                {
                    if (currentCommand.Name.ToLower() == "wait" && currentCommand.Arguments.Count > 0)
                    {
                        double waitTime;

                        if (double.TryParse(currentCommand.Arguments[0], out waitTime))
                        {
                            Mother.Print($"> wait {waitTime}");
                            Clock.QueueForLater(() => ProcessCommandsSequentially(remainingCommands), waitTime);
                        }
                        else
                        {
                            ProcessCommandsSequentially(remainingCommands); // Skip invalid wait and continue
                        }
                    }
                    else
                    {
                        HandleCommand(currentCommand); // Execute regular command
                        ProcessCommandsSequentially(remainingCommands);  // Immediately process next command
                    }
                };

                // Apply the initial delay if provided
                if (initialDelay > 0)
                    Clock.QueueForLater(executeCurrentCommand, initialDelay);
                else
                    executeCurrentCommand();
            }

            /// <summary>
            /// Processes and executes a single command.  Commands can only be run 
            /// locally on a grid. Remote commands are considered Routines.
            /// </summary>
            /// <param name="command"></param>
            /// <returns></returns>
            public bool HandleCommand(TerminalCommand command)
            {
                string commandString = command.CommandString;

                // Handle the "wait" command
                if (command.Name.ToLower() == "wait" && command.Arguments.Count > 0)
                {
                    double waitTime;

                    if (double.TryParse(command.Arguments[0], out waitTime))
                    {
                        // Queue the next command after the wait time
                        Clock.QueueForLater(() =>
                        {
                            if (command.Arguments.Count > 1)
                            {
                                // Combine the rest of the arguments as the command to run after waiting
                                string delayedCommand = string.Join(" ", command.Arguments.Skip(1).ToArray());
                                RunTerminalCommand(delayedCommand);
                            }
                        }, waitTime);

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                // Execute the command from the config commands
                if (Mother.ConfigCommands.ContainsKey(commandString))
                    return RunTerminalCommand(Mother.ConfigCommands[commandString]);

                // or execute a command registered by a module
                foreach (IModuleCommand moduleCommand in ModuleCommands)
                {
                    if (moduleCommand.GetCommandName() == command.Name)
                    {
                        var logString = "> " + commandString;

                        Log.Info(logString);
                        Mother.Print(logString);

                        string output = moduleCommand.Execute(command);

                        Mother.Print(output, false);
                        return true;
                    }
                }

                Mother.Print(MessageFormatter.Format(Messages.CommandNotFound, command.CommandString), false);

                return false;
            }

            /// <summary>
            /// Add a routine to the queue, for execution at a waypoint in a flight plan.
            /// </summary>
            /// <param name="waypoint"></param>
            /// <param name="routine"></param>
            public void AddRoutineForWaypoint(IWaypoint waypoint, string routine)
            {
                WaypointRoutineQueue.AddRoutineForWaypoint(waypoint, routine);
            }

            /// <summary>
            /// Runs all queued routines for a waypoint.
            /// </summary>
            /// <param name="waypointName"></param>
            public void RunRoutineForWaypoint(string waypointName)
            {
                string command = WaypointRoutineQueue.GetRoutineForWaypoint(waypointName);

                if (command != "")
                {
                    RunTerminalCommand(command);

                    // Remove routines from the queue after execution
                    WaypointRoutineQueue.RemoveRoutineForWaypoint(waypointName);
                }
            }
        }
    }
}
