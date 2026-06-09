using System.Collections.Generic;
using System.Text;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Factory-style helper for producing a valid INI-formatted CustomData payload
    /// with [variables] and [commands] sections.
    /// Eliminates repeated manual string construction across ConfigurationTests and related test files.
    /// </summary>
    public class CustomDataComposer
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
        /// Adds a variable to the composed CustomData payload. Variables are stored in a list and will be 
        /// included in the [variables] section of the final INI string.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public CustomDataComposer WithVariable(string name, string value)
        {
            _variables.Add((name, value));

            return this;
        }

        /// <summary>
        /// Adds a command to the composed CustomData payload. Commands are stored in a separate list and will be 
        /// included in the [commands] section of the final INI string.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public CustomDataComposer WithCommand(string name, string value)
        {
            _commands.Add((name, value));

            return this;
        }

        //public CustomDataComposer With(string section, string name, string value)
        //{
        //    if (section == "variables")
        //        return WithVariable(name, value);
        //    else if (section == "commands")
        //        return WithCommand(name, value);
        //    else
        //        throw new ArgumentException($"Unknown section: {section}");

        //    return this;
        //}

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
