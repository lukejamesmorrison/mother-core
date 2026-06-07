using IngameScript;
using System.Collections.Generic;
using System.Linq;

namespace MotherCore.Tests.TestUtilities
{
    /// <summary>
    /// Redirects <see cref="Program.Echo"/> to an in-memory list so tests can assert
    /// on strings printed by <c>Mother.Print</c> without wiring a <c>Terminal</c> module.
    /// </summary>
    /// <remarks>
    /// Construct one instance per test and pass the <c>Program</c> from a
    /// <see cref="TestSession"/> before calling <see cref="TestSession.Boot"/>, or
    /// immediately after boot (before any commands run):
    /// <code>
    /// var session = new TestSession().Boot();
    /// var capture = new PrintCapture(session);
    ///
    /// session.Bus.RunTerminalCommand("nonexistent");
    /// session.Clock.RunToIdle();
    ///
    /// Assert.That(capture.Contains("Command not found"), Is.True);
    /// </code>
    /// </remarks>
    public class PrintCapture
    {
        /// <summary>Every message that was passed to <c>Program.Echo</c> since construction.</summary>
        public List<string> Lines { get; } = new List<string>();

        /// <summary>
        /// Redirects <c>Program.Echo</c> for the session's underlying program.
        /// </summary>
        public PrintCapture(ITestSession session)
        {
            ((Sandbox.ModAPI.IMyGridProgram)session.Mother.Program).Echo = message => Lines.Add(message);
        }

        /// <summary>Returns <c>true</c> if any captured line contains <paramref name="fragment"/>.</summary>
        public bool Contains(string fragment) =>
            Lines.Any(l => l.Contains(fragment));

        /// <summary>Clears all captured lines.</summary>
        public void Clear() => Lines.Clear();
    }
}
