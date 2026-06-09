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
    /// Construct one instance per test and pass the <c>Program</c> from a
    /// <see cref="Script"/> before calling <see cref="Script.Boot"/>, or
    /// immediately after boot (before any commands run):
    /// <code>
    /// var script = new Script().Boot();
    /// var capture = new PrintCapture(script);
    ///
    /// script.Bus.RunTerminalCommand("nonexistent");
    /// script.Clock.RunToIdle();
    ///
    /// Assert.That(capture.Contains("Command not found"), Is.True);
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
            ((Sandbox.ModAPI.IMyGridProgram) script.Mother.Program).Echo = message => Lines.Add(message);
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
        public void ShouldHavePrinted(string fragment)
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
