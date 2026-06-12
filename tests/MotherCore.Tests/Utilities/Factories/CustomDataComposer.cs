using System.Collections.Generic;
using System.Text;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Factory-style helper for producing a valid INI-formatted CustomData payload.
    /// Eliminates repeated manual string construction across tests while exposing
    /// a section/key API similar to MyIni for setup and expected-value lookups.
    /// </summary>
    public class CustomDataComposer
    {
        readonly List<string> _sectionOrder = new List<string>();
        readonly Dictionary<string, List<string>> _keyOrderBySection = new Dictionary<string, List<string>>();
        readonly Dictionary<string, Dictionary<string, string>> _valuesBySection = new Dictionary<string, Dictionary<string, string>>();

        void EnsureSection(string section)
        {
            if (_valuesBySection.ContainsKey(section))
                return;

            _sectionOrder.Add(section);
            _keyOrderBySection[section] = new List<string>();
            _valuesBySection[section] = new Dictionary<string, string>();
        }

        /// <summary>
        /// Sets a key within the supplied section, replacing any existing value.
        /// </summary>
        public CustomDataComposer Set(string section, string name, string value)
        {
            EnsureSection(section);

            if (!_valuesBySection[section].ContainsKey(name))
                _keyOrderBySection[section].Add(name);

            _valuesBySection[section][name] = value;

            return this;
        }

        /// <summary>
        /// Reads the composed value for a given section/key pair.
        /// Returns an empty string when the pair has not been defined.
        /// </summary>
        public string Get(string section, string name)
        {
            if (!_valuesBySection.ContainsKey(section))
                return string.Empty;

            return _valuesBySection[section].ContainsKey(name)
                ? _valuesBySection[section][name]
                : string.Empty;
        }

        /// <summary>
        /// Adds a variable to the composed CustomData payload. Variables are stored in a list and will be 
        /// included in the [variables] section of the final INI string.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public CustomDataComposer WithVariable(string name, string value)
        {
            return With("variables", name, value);
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
            return With("commands", name, value);
        }

        /// <summary>
        /// Adds a generic section/key/value pair to the composed CustomData payload.
        /// </summary>
        public CustomDataComposer With(string section, string name, string value)
        {
            return Set(section, name, value);
        }

        /// <summary>
        /// Constructs the final CustomData string in INI format, combining all added 
        /// variables and commands.
        /// </summary>
        /// <returns></returns>
        public string Build()
        {
            var sb = new StringBuilder();

            for (int sectionIndex = 0; sectionIndex < _sectionOrder.Count; sectionIndex++)
            {
                string section = _sectionOrder[sectionIndex];

                sb.AppendLine($"[{section}]");

                foreach (var key in _keyOrderBySection[section])
                    sb.AppendLine($"{key}={_valuesBySection[section][key]}");

                if (sectionIndex < _sectionOrder.Count - 1)
                    sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }
}
