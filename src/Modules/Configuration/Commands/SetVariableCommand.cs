namespace IngameScript
{
    /// <summary>
    /// Command to set or update the value of a variable at runtime.
    /// 
    /// Usage: var/set VARIABLE_NAME value [--save]
    /// 
    /// Updates the variable in memory so that subsequent command substitutions
    /// use the new value. If the --save option is provided, the variable is
    /// also persisted to the programmable block's custom data.
    /// </summary>
    public class SetVariableCommand : BaseModuleCommand
    {
        /// <summary>
        /// The Configuration core module.
        /// </summary>
        readonly Configuration Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "var/set";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="module"></param>
        public SetVariableCommand(Configuration module)
        {
            Module = module;
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            if (command.Arguments.Count < 2)
                return CommandBus.Messages.NoArgumentsProvided;

            string name = command.Arguments[0];
            string value = command.Arguments[1];
            bool save = command.Options.ContainsKey("save");

            Module.SetVariable(name, value, save);

            return save
                ? $"${name} = \"{value}\" (saved)"
                : $"${name} = \"{value}\"";
        }
    }
}
