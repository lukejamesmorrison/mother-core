using IngameScript;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Redirects <see cref="CoreTestProgram.Echo"/> to an in-memory list so tests can assert
    /// on strings printed by <c>Mother.Print</c> without wiring a <c>Terminal</c> module.
    /// </summary>
    /// <remarks>
    /// In normal harness usage this is created automatically by
    /// <see cref="Script.Boot"/> and cleared after boot so tests can assert on
    /// post-boot output without extra setup:
    /// <code>
    /// var script = new Script().Boot();
    /// script.Bus.RunTerminalCommand("nonexistent");
    /// script.Clock.RunToIdle();
    ///
    /// script.AssertPrinted("Command not found");
    /// </code>
    /// </remarks>
    public class PrintCapture
    {
        /// <summary>
        /// Every message that was passed to <c>Program.Echo</c> since construction.
        /// </summary>
        public List<string> Lines { get; } = new List<string>();

        /// <summary>
        /// Redirects <c>Program.Echo</c> for the script's underlying program.
        /// </summary>
        public PrintCapture(IScript script)
        {
            if (script == null)
                throw new System.ArgumentNullException(nameof(script));

            var program = script.ProgramInstance;

            if (program == null)
                throw new System.InvalidOperationException("Expected a booted script with a non-null program instance.");

            program.Echo = message => Lines.Add(message);
        }

        /// <summary>
        /// Returns <c>true</c> if any captured line contains <paramref name="fragment"/>.
        /// </summary>
        public bool Contains(string fragment) =>
            Lines.Any(l => l.Contains(fragment));

        /// <summary>
        /// Asserts that at least one captured line contains <paramref name="fragment"/>.
        /// Throws an NUnit assertion failure if the fragment is not found.
        /// </summary>
        public void AssertPrinted(string fragment)
        {
            Assert.That(Contains(fragment), Is.True,
                $"Expected output to contain \"{fragment}\" but it was not found.\n" +
                $"Captured lines:\n{string.Join("\n", Lines)}");
        }

        /// <summary>
        /// Clears all captured lines.
        /// </summary>
        public void Clear() => Lines.Clear();
    }
}
