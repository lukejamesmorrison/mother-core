using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Compiles a packaged programmable block script the way the game does: the script is the body of
// Program, followed by the "}" that closes it, followed by the top-level types. The packager omits the
// closing "}" of the last type, so the suffix supplies it, and also closes the namespace.
sealed class ScriptCompilation
{
    const string Usings =
        "using System;using System.Collections;using System.Collections.Generic;using System.Linq;using System.Text;" +
        "using System.Collections.Immutable;using Sandbox.ModAPI.Ingame;using Sandbox.ModAPI.Interfaces;" +
        "using SpaceEngineers.Game.ModAPI.Ingame;using VRage;using VRage.Game;using VRage.Game.Components;" +
        "using VRage.Game.GUI.TextPanel;using VRage.Game.ModAPI.Ingame;using VRage.Game.ModAPI.Ingame.Utilities;" +
        "using VRage.Game.ObjectBuilders.Definitions;using VRage.ObjectBuilders;using VRageMath;";

    public const string Prefix = Usings + "namespace IngameScript{public class Program:MyGridProgram{";
    public const string Suffix = "}}";

    static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp6);

    public SyntaxTree Tree { get; }
    public CSharpCompilation Compilation { get; }

    ScriptCompilation(SyntaxTree tree, CSharpCompilation compilation)
    {
        Tree = tree;
        Compilation = compilation;
    }

    public static ScriptCompilation Create(string script, IEnumerable<MetadataReference> references)
    {
        var tree = CSharpSyntaxTree.ParseText(Prefix + script + Suffix, ParseOptions);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
        return new ScriptCompilation(tree, CSharpCompilation.Create("script", new[] { tree }, references, options));
    }

    public List<Diagnostic> Errors() =>
        Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    // Not in the canonical "error CSxxxx" format, which MSBuild would treat as a failure of the Exec task.
    public static string Describe(Diagnostic d) =>
        $"{d.Id} at line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}";
}
