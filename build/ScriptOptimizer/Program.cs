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

var original = ScriptCompilation.Create(script, references);
var baseline = original.Errors();
if (baseline.Count > 0)
{
    Console.WriteLine($"ScriptOptimizer: skipped, the script has {baseline.Count} compile error(s) before optimizing");
    foreach (var d in baseline.Take(5))
        Console.WriteLine("  " + ScriptCompilation.Describe(d));
    return 0;
}

var edits = VarRewriter.FindEdits(original);
var offset = ScriptCompilation.Prefix.Length;
var sb = new StringBuilder(script);
foreach (var span in edits.OrderByDescending(e => e.Start))
{
    var start = span.Start - offset;
    var next = start + span.Length < script.Length ? script[start + span.Length] : ' ';
    var needsSpace = char.IsLetterOrDigit(next) || next == '_';
    sb.Remove(start, span.Length).Insert(start, needsSpace ? "var " : "var");
}
var optimized = sb.ToString();

var errors = ScriptCompilation.Create(optimized, references).Errors();
if (errors.Count > 0)
{
    Console.WriteLine($"ScriptOptimizer: skipped, optimizing produced {errors.Count} compile error(s)");
    foreach (var d in errors.Take(5))
        Console.WriteLine("  " + ScriptCompilation.Describe(d));
    return 0;
}

var encoding = new UTF8Encoding(hasBom);
File.WriteAllBytes(scriptPath, encoding.GetPreamble().Concat(encoding.GetBytes(optimized)).ToArray());
Console.WriteLine($"ScriptOptimizer: var for {edits.Count} locals, {script.Length} -> {optimized.Length} characters");
return 0;
