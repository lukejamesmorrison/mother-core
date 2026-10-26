using System.Text;
using Microsoft.CodeAnalysis;

// Post-build optimizer for the packaged programmable block script.
// Usage: ScriptOptimizer <script.cs> <references-file>
// The references file lists one assembly path per line. On any problem the script is left untouched
// and the tool exits with 0, so the build still produces a valid (just larger) script.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ScriptOptimizer <script.cs> <references-file>");
    return 1;
}

var scriptPath = args[0];
var referencesPath = args[1];

if (!File.Exists(scriptPath) || !File.Exists(referencesPath))
{
    Console.WriteLine($"ScriptOptimizer: skipped, missing input ({scriptPath} or {referencesPath})");
    return 0;
}

var bytes = File.ReadAllBytes(scriptPath);
var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
var script = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));

var references = File.ReadAllLines(referencesPath)
    .Select(l => l.Trim())
    .Where(l => l.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(l))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Select(l => (MetadataReference)MetadataReference.CreateFromFile(l))
    .ToList();

var baseline = ScriptCompilation.Create(script, references).Errors();
if (baseline.Count > 0)
{
    Console.WriteLine($"ScriptOptimizer: skipped, the script has {baseline.Count} compile error(s) before optimizing");
    foreach (var d in baseline.Take(5))
        Console.WriteLine("  " + ScriptCompilation.Describe(d));
    return 0;
}

// Keeps a pass's result only if it still compiles, so a faulty pass costs size, never a broken script.
string Accept(string pass, string before, string after)
{
    if (after == before)
        return before;

    var errors = ScriptCompilation.Create(after, references).Errors();
    if (errors.Count == 0)
        return after;

    Console.WriteLine($"ScriptOptimizer: skipped {pass}, it produced {errors.Count} compile error(s)");
    foreach (var d in errors.Take(5))
        Console.WriteLine("  " + ScriptCompilation.Describe(d));
    return before;
}

var current = script;
var summary = new List<string>();

// "var" runs first: once a type is aliased, "var" is no longer shorter than the declared type.
var varEdits = VarRewriter.FindEdits(ScriptCompilation.Create(current, references));
var withVar = ScriptEdits.Apply(current, varEdits.Select(span => ScriptEdits.Replace(current, span, "var")));
var afterVar = Accept("var", current, withVar);
summary.Add($"var for {(afterVar == current ? 0 : varEdits.Count)} locals {current.Length} -> {afterVar.Length}");
current = afterVar;

var aliases = AliasRewriter.Rewrite(current, ScriptCompilation.Create(current, references), references);
if (aliases != null)
{
    var afterAliases = Accept("collection aliases", current, aliases.Script);
    summary.Add($"{(afterAliases == current ? 0 : aliases.Aliases)} collection aliases at {(afterAliases == current ? 0 : aliases.Sites)} sites {current.Length} -> {afterAliases.Length}");
    current = afterAliases;
}

var helpers = CallHelperRewriter.Rewrite(current, ScriptCompilation.Create(current, references));
if (helpers != null)
{
    var afterHelpers = Accept("call helpers", current, helpers.Script);
    summary.Add($"{(afterHelpers == current ? 0 : helpers.Helpers)} call helpers at {(afterHelpers == current ? 0 : helpers.Sites)} sites {current.Length} -> {afterHelpers.Length}");
    current = afterHelpers;
}

var narrowEdits = AccessNarrower.FindEdits(current, ScriptCompilation.Create(current, references));
var afterNarrow = Accept("public narrowing", current, ScriptEdits.Apply(current, narrowEdits));
summary.Add($"{(afterNarrow == current ? 0 : narrowEdits.Count)} members narrowed from public {current.Length} -> {afterNarrow.Length}");
current = afterNarrow;

var compact = Accept("line break removal", current, NewlineStripper.Strip(current));
summary.Add($"line breaks {current.Length} -> {compact.Length}");

var encoding = new UTF8Encoding(hasBom);
File.WriteAllBytes(scriptPath, encoding.GetPreamble().Concat(encoding.GetBytes(compact)).ToArray());
Console.WriteLine($"ScriptOptimizer: {script.Length} -> {compact.Length} characters ({string.Join(", ", summary)})");
return 0;
