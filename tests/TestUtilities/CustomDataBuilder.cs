using System.Collections.Generic;
using System.Text;

namespace MotherCore.Tests.TestUtilities
{
    /// <summary>
    /// Builds a valid INI-formatted CustomData string with [variables] and [commands] sections.
    /// Eliminates repeated manual string construction across ConfigurationTests and related test files.
    /// </summary>
    public class CustomDataBuilder
    {
        /// <summary>
        /// Stores variables as name-value pairs. Variables are added separately from commands 
        /// and will be included in the [variables] section of the final INI string.
        /// </summary>
        readonly List<(string Name, string Value)> _variables = new List<(string, string)>();

        /// <summary>
        /// Stores commands as name-value pairs. Commands are added separately from variables 
        /// and will be included in the [commands] section of the final INI string.
        /// </summary>
        readonly List<(string Name, string Value)> _commands = new List<(string, string)>();

        /// <summary>
        /// Adds a variable to the builder. Variables are stored in a list and will be 
        /// included in the [variables] section of the final INI string.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public CustomDataBuilder WithVariable(string name, string value)
        {
            _variables.Add((name, value));
            return this;
        }

        /// <summary>
        /// Adds a command to the builder. Commands are stored in a separate list and will be 
        /// included in the [commands] section of the final INI string.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public CustomDataBuilder WithCommand(string name, string value)
        {
            _commands.Add((name, value));
            return this;
        }

        /// <summary>
        /// Constructs the final CustomData string in INI format, combining all added 
        /// variables and commands.
        /// </summary>
        /// <returns></returns>
        public string Build()
        {
            var sb = new StringBuilder();

            sb.AppendLine("[variables]");

            foreach (var (name, value) in _variables)
                sb.AppendLine($"{name}={value}");

            sb.AppendLine();
            sb.AppendLine("[commands]");

            foreach (var (name, value) in _commands)
                sb.AppendLine($"{name}={value}");

            return sb.ToString().TrimEnd();
        }
    }
}
