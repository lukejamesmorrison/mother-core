using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Removes line breaks the minifier left in the script. A break is only needed to keep two tokens apart, so
// it is deleted when it sits next to punctuation, and becomes a single space between two identifiers or
// between operators that would merge into another one (for example "+" and "+"). Breaks that end a "//"
// comment (the header) are kept, since the comment would otherwise swallow the code after it.
static class NewlineStripper
{
    const string Operators = "+-&|/<>?*%=!.:";

    public static string Strip(string script)
    {
        var text = ScriptCompilation.Prefix + script + ScriptCompilation.Suffix;
        var root = ScriptCompilation.Parse(script).GetRoot();
        var sb = new StringBuilder(text.Length);
        var copied = 0;

        foreach (var token in root.DescendantTokens())
        {
            foreach (var list in new[] { token.LeadingTrivia, token.TrailingTrivia })
            {
                var afterComment = false;

                foreach (var trivia in list)
                {
                    var isBreak = trivia.IsKind(SyntaxKind.EndOfLineTrivia);

                    if (isBreak && !afterComment)
                    {
                        sb.Append(text, copied, trivia.SpanStart - copied);
                        copied = trivia.Span.End;

                        var previous = sb.Length > 0 ? sb[sb.Length - 1] : ' ';
                        var next = copied < text.Length ? text[copied] : ' ';

                        if (NeedsSpace(previous, next))
                            sb.Append(' ');
                    }

                    afterComment = trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsDirective;
                }
            }
        }

        sb.Append(text, copied, text.Length - copied);

        return sb.ToString(ScriptCompilation.Prefix.Length, sb.Length - ScriptCompilation.Prefix.Length - ScriptCompilation.Suffix.Length);
    }

    static bool NeedsSpace(char previous, char next) =>
        (IsIdentifier(previous) && IsIdentifier(next)) ||
        (Operators.IndexOf(previous) >= 0 && Operators.IndexOf(next) >= 0) ||
        (char.IsDigit(previous) && next == '.');

    static bool IsIdentifier(char c) => SyntaxFacts.IsIdentifierPartCharacter(c);
}
