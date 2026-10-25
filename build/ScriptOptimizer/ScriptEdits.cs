using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

// A text edit on the packaged script. Start is a position in the wrapped text (prefix, script, suffix),
// the way Roslyn reports it, so passes can use syntax tree positions directly.
readonly record struct ScriptEdit(int Start, int Length, string Text);

static class ScriptEdits
{
    public static ScriptEdit Replace(string script, TextSpan span, string text)
    {
        var start = span.Start - ScriptCompilation.Prefix.Length;
        var before = start > 0 ? script[start - 1] : ' ';
        var after = span.End - ScriptCompilation.Prefix.Length < script.Length ? script[span.End - ScriptCompilation.Prefix.Length] : ' ';

        // The minifier leaves no space where none is needed, so keep two identifiers apart if the edit joins them.
        var separate = text.Length == 0
            ? IsIdentifier(before) && IsIdentifier(after)
            : IsIdentifier(text[text.Length - 1]) && IsIdentifier(after);

        return new ScriptEdit(span.Start, span.Length, separate ? text + " " : text);
    }

    public static string Apply(string script, IEnumerable<ScriptEdit> edits)
    {
        var sb = new StringBuilder(script);
        var offset = ScriptCompilation.Prefix.Length;

        foreach (var edit in edits.OrderByDescending(e => e.Start))
            sb.Remove(edit.Start - offset, edit.Length).Insert(edit.Start - offset, edit.Text);

        return sb.ToString();
    }

    static bool IsIdentifier(char c) => SyntaxFacts.IsIdentifierPartCharacter(c);
}
