using System;

namespace IngameScript
{
    /// <summary>
    /// Command whose behaviour is a delegate. A module registers it with a lambda 
    /// that closes over the module, so a simple command needs no class of its own: 
    /// <c>RegisterCommand("clear", 0, c => { ClearConsole(); return ""; })</c>.
    /// Keep a real command class for commands that hold their own state.
    /// </summary>
    public class LambdaCommand : BaseModuleCommand
    {
        /// <summary>
        /// The minimum number of arguments required to run the command. If 
        /// fewer are provided, the usage text is returned instead.
        /// </summary>
        readonly int MinArgs;
        /// <summary>
        /// The usage text returned when too few arguments are provided. If null, the
        /// default message <see cref="CommandBus.Messages.NoArgumentsProvided"/> 
        /// is returned instead.
        /// </summary>
        readonly string Usage;
        /// <summary>
        /// The delegate that runs the command. It is passed the 
        /// <see cref="TerminalCommand"/>. 
        /// </summary>
        readonly Func<TerminalCommand, string> Run;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The command name, ie. view/up.</param>
        /// <param name="minArgs">Fewer arguments than this are answered with the usage text and the delegate is not called.</param>
        /// <param name="run">Runs the command and returns the response text.</param>
        /// <param name="usage">Response for too few arguments. Defaults to <see cref="CommandBus.Messages.NoArgumentsProvided"/>.</param>
        public LambdaCommand(string name, int minArgs, Func<TerminalCommand, string> run, string usage = null)
        {
            Name = name;
            MinArgs = minArgs;
            Run = run;
            Usage = usage;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            if (command.Arguments.Count < MinArgs)
                return Usage ?? CommandBus.Messages.NoArgumentsProvided;

            return Run(command);
        }
    }
}
