using System;

namespace IngameScript
{
    /// <summary>
    /// Command to set the grid's custom name.
    /// Optionally appends a random integer for uniqueness.
    /// </summary>
    public class RenameCommand : BaseModuleCommand
    {
        /// <summary>
        /// The Mother instance.
        /// </summary>
        readonly Mother Mother;

        /// <summary>
        /// Random number generator for unique naming.
        /// </summary>
        readonly Random Random = new Random();

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "rename";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public RenameCommand(Mother mother)
        {
            Mother = mother;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            if (command.Arguments.Count < 1)
                return CommandBus.Messages.NoArgumentsProvided;

            string newName = command.Arguments[0];

            bool isUnique = TerminalCommand.GetBoolFromString(command.GetOption("unique"));

            if (isUnique)
                newName = $"{newName}-{Random.Next(10000, 99999)}";

            // Set the grid's custom name
            Mother.CubeGrid.CustomName = newName;

            // Update Mother's name reference
            Mother.Name = newName;

            // Store the name in the programmable block's custom data
            var config = Mother.GetModule<Configuration>();
            config.Raw.Set("general", "name", newName);
            Mother.ProgrammableBlock.CustomData = config.Raw.ToString();

            // Broadcast the name change to other grids
            var igms = Mother.GetModule<IntergridMessageService>();
            igms.ConstructPing(); // Notify scripts on this construct
            igms.Ping();          // Notify remote grids

            return $"Grid name set to: {newName}";
        }
    }
}
