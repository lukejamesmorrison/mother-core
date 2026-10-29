using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

// Replaces long constructed collection types such as Dictionary<string,object> with one-character
// subclasses ("class A:Dictionary<string,object>{}"), which a programmable block script cannot do with a
// "using" alias. Only declarations and "new" expressions are rewritten, never casts, "is", "as" or type
// arguments, so a type test still sees the original type. Sites that stop compiling (for example "new
// List<string>(items)", since constructors are not inherited, or a value coming from ToList()) are put
// back, so what remains is always a script that compiles.
static class AliasRewriter
{
    const int MaxRounds = 30;

    static readonly HashSet<string> Aliasable = new()
    {
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.Stack`1",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.LinkedList`1"
    };

    sealed class Candidate
    {
        public required INamedTypeSymbol Type { get; init; }
        public string Text { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Active { get; set; } = true;
        public List<Site> Sites { get; } = new();

        // "public" is needed while the script still has public types, and is removed from top-level types
        // by RemoveKeywords.ps1, so the saving is measured without it.
        public string Declaration => $"public class {Name}:{Text}{{}}";

        // Characters saved by the active sites, minus "class N:Type{}".
        public int Net => Sites.Where(s => s.Active).Sum(s => s.Length - Name.Length - (s.SpaceAfter ? 1 : 0)) - (Text.Length + Name.Length + 9);
    }

    sealed class Site
    {
        public required Candidate Owner { get; init; }
        public required TypeSyntax Node { get; init; }
        public bool SpaceAfter { get; init; }
        public bool Active { get; set; } = true;
        public int Start => Node.SpanStart;
        public int Length => Node.Span.Length;
    }

    public sealed record Result(string Script, int Aliases, int Sites);

    public static Result? Rewrite(string script, ScriptCompilation compilation, IReadOnlyList<MetadataReference> references)
    {
        var root = compilation.Tree.GetRoot();
        var model = compilation.Compilation.GetSemanticModel(compilation.Tree);

        var program = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == "Program");
        if (program == null || program.CloseBraceToken.Span.End - ScriptCompilation.Prefix.Length > script.Length)
            return null;

        var insertAt = program.CloseBraceToken.Span.End;
        var candidates = FindCandidates(script, root, model, compilation.Compilation.Assembly);
        if (candidates.Count == 0)
            return null;

        for (var round = 0; round < MaxRounds; round++)
        {
            var active = candidates.Where(c => c.Active).ToList();
            if (active.Count == 0)
                return null;

            var built = Build(script, active, insertAt);
            var errors = ScriptCompilation.Create(built.Text, references).Errors();

            if (errors.Count == 0)
            {
                var unprofitable = active.Where(c => c.Net <= 0).ToList();
                if (unprofitable.Count == 0)
                    return new Result(built.Text, active.Count, active.Sum(c => c.Sites.Count(s => s.Active)));

                foreach (var candidate in unprofitable)
                    Deactivate(candidate);
                continue;
            }

            if (!Revert(errors, built, root, model, candidates))
            {
                Console.WriteLine($"ScriptOptimizer: collection aliases abandoned, {errors.Count} compile error(s) could not be traced to a rewritten site");
                foreach (var d in errors.Take(3))
                    Console.WriteLine("  " + ScriptCompilation.Describe(d));
                return null;
            }
        }

        return null;
    }

    static void Deactivate(Candidate candidate)
    {
        candidate.Active = false;
        foreach (var site in candidate.Sites)
            site.Active = false;
    }

    static List<Candidate> FindCandidates(string script, SyntaxNode root, SemanticModel model, IAssemblySymbol assembly)
    {
        var byType = new Dictionary<ITypeSymbol, Candidate>(SymbolEqualityComparer.Default);
        var offset = ScriptCompilation.Prefix.Length;

        foreach (var generic in root.DescendantNodes().OfType<GenericNameSyntax>())
        {
            if (!IsRewritableContext(generic, model) ||
                model.GetSymbolInfo(generic).Symbol is not INamedTypeSymbol type ||
                !IsAliasable(type, assembly))
                continue;

            if (!byType.TryGetValue(type, out var candidate))
                byType[type] = candidate = new Candidate { Type = type, Text = generic.ToString() };

            if (generic.Span.Length < candidate.Text.Length)
                candidate.Text = generic.ToString();

            var end = generic.Span.End - offset;
            candidate.Sites.Add(new Site
            {
                Owner = candidate,
                Node = generic,
                SpaceAfter = end < script.Length && SyntaxFacts.IsIdentifierPartCharacter(script[end])
            });
        }

        var used = new HashSet<string>(root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText));
        var names = Names(used).GetEnumerator();

        var ordered = byType.Values.Where(c => c.Sites.Count > 1).OrderByDescending(c => c.Sites.Sum(s => s.Length)).ToList();
        foreach (var candidate in ordered)
        {
            names.MoveNext();
            candidate.Name = names.Current;
        }

        return ordered.Where(c => c.Net > 0).ToList();
    }

    // Single characters first (the packager uses the same kind of names, up to U+07A4), then pairs. Pairs draw on every
    // letter, not only the unused ones, because a script that has used up the single characters would get no pairs.
    public static IEnumerable<string> Names(HashSet<string> used)
    {
        // The game compiles with Roslyn 2.9 on .NET Framework 4.8 (Unicode 8.0), so letters added later are not
        // identifiers there: U+0560, U+0588, U+05EF and U+0860 onward are left out.
        var letters = Enumerable.Range('A', 0x860 - 'A')
            .Select(i => (char)i)
            .Where(c => char.IsLetter(c) && SyntaxFacts.IsIdentifierStartCharacter(c)
                && c != '\u0560' && c != '\u0588' && c != '\u05EF')
            .ToList();

        foreach (var c in letters)
            if (!used.Contains(c.ToString()))
                yield return c.ToString();

        foreach (var a in letters)
            foreach (var b in letters)
                if (!used.Contains(a.ToString() + b))
                    yield return a.ToString() + b;
    }

    static bool IsAliasable(INamedTypeSymbol type, IAssemblySymbol assembly) =>
        type.IsGenericType &&
        Aliasable.Contains(type.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + type.OriginalDefinition.MetadataName) &&
        !type.TypeArguments.Any(a => CannotBeNamedAtTopLevel(a, assembly));

    // The subclass is declared at the top level, so it cannot mention type parameters or nested types.
    static bool CannotBeNamedAtTopLevel(ITypeSymbol type, IAssemblySymbol assembly) =>
        type switch
        {
            ITypeParameterSymbol => true,
            IErrorTypeSymbol => true,
            IArrayTypeSymbol array => CannotBeNamedAtTopLevel(array.ElementType, assembly),
            INamedTypeSymbol named => (named.ContainingType != null && SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, assembly)) ||
                                      named.TypeArguments.Any(a => CannotBeNamedAtTopLevel(a, assembly)),
            _ => false
        };

    // Declarations and "new" without arguments. Casts, "is", "as", typeof, generic type arguments, foreach
    // variables (an explicit conversion that can fail at run time) and lambda parameters are left alone.
    static bool IsRewritableContext(GenericNameSyntax generic, SemanticModel model)
    {
        switch (generic.Parent)
        {
            case VariableDeclarationSyntax declaration when declaration.Type == generic:
                return declaration.Parent is not (EventFieldDeclarationSyntax or FixedStatementSyntax);

            case ParameterSyntax parameter when parameter.Type == generic:
                return parameter.Parent?.Parent is MethodDeclarationSyntax or ConstructorDeclarationSyntax && !IsBoundByContract(parameter.Parent!.Parent, model);

            case MethodDeclarationSyntax method when method.ReturnType == generic:
                return !IsBoundByContract(method, model);

            case PropertyDeclarationSyntax property when property.Type == generic:
                return !IsBoundByContract(property, model);

            case ObjectCreationExpressionSyntax creation when creation.Type == generic:
                return creation.ArgumentList == null || creation.ArgumentList.Arguments.Count == 0;

            default:
                return false;
        }
    }

    static bool IsBoundByContract(SyntaxNode? member, SemanticModel model)
    {
        var symbol = member == null ? null : model.GetDeclaredSymbol(member);
        return symbol != null && SymbolHelpers.IsBoundByContract(symbol);
    }

    sealed record BuiltPart(int OriginalStart, int OriginalLength, string Text, Site? Site);

    sealed class Built
    {
        public string Text { get; init; } = "";
        public List<BuiltPart> Parts { get; init; } = new();
        public int DeclarationsStart { get; init; }
        public List<(int Start, int End, Candidate Candidate)> DeclarationRanges { get; init; } = new();
    }

    static Built Build(string script, List<Candidate> active, int insertAt)
    {
        var offset = ScriptCompilation.Prefix.Length;
        var parts = new List<BuiltPart>();

        foreach (var site in active.SelectMany(c => c.Sites).Where(s => s.Active))
            parts.Add(new BuiltPart(site.Start, site.Length, site.Owner.Name + (site.SpaceAfter ? " " : ""), site));

        var declarations = new StringBuilder();
        var ranges = new List<(int, int, Candidate)>();
        foreach (var candidate in active)
        {
            var start = declarations.Length;
            declarations.Append(candidate.Declaration);
            ranges.Add((start, declarations.Length, candidate));
        }

        parts.Add(new BuiltPart(insertAt, 0, declarations.ToString(), null));
        parts = parts.OrderBy(p => p.OriginalStart).ToList();

        var sb = new StringBuilder(script.Length);
        var copied = 0;
        var declarationsStart = 0;
        var shifted = new List<(int, int, Candidate)>();

        foreach (var part in parts)
        {
            var start = part.OriginalStart - offset;
            sb.Append(script, copied, start - copied);

            if (part.Site == null)
            {
                declarationsStart = sb.Length;
                shifted = ranges.Select(r => (declarationsStart + r.Item1, declarationsStart + r.Item2, r.Item3)).ToList();
            }

            sb.Append(part.Text);
            copied = start + part.OriginalLength;
        }

        sb.Append(script, copied, script.Length - copied);

        return new Built { Text = sb.ToString(), Parts = parts, DeclarationsStart = declarationsStart, DeclarationRanges = shifted };
    }

    // Finds the rewritten sites behind the compile errors and puts them back. Returns false if nothing was reverted.
    static bool Revert(List<Diagnostic> errors, Built built, SyntaxNode root, SemanticModel model, List<Candidate> candidates)
    {
        var offset = ScriptCompilation.Prefix.Length;
        var sites = candidates.Where(c => c.Active).SelectMany(c => c.Sites).Where(s => s.Active).ToList();
        var changed = false;

        foreach (var error in errors)
        {
            if (!error.Location.IsInSource)
                continue;

            var position = error.Location.SourceSpan.Start - offset;
            if (position < 0 || position >= built.Text.Length)
                continue;

            var declaration = built.DeclarationRanges.FirstOrDefault(r => position >= r.Start && position < r.End);
            if (declaration.Candidate != null)
            {
                changed |= declaration.Candidate.Active;
                Deactivate(declaration.Candidate);
                continue;
            }

            var (original, hit) = ToOriginal(position, built);
            if (hit != null)
            {
                changed |= hit.Active;
                hit.Active = false;
                continue;
            }

            var token = root.FindToken(Math.Min(original + offset, root.FullSpan.End - 1));
            var unit = token.Parent?.AncestorsAndSelf().FirstOrDefault(n => (n is StatementSyntax and not BlockSyntax) || n is MemberDeclarationSyntax);
            if (unit == null || unit is BaseTypeDeclarationSyntax)
                continue;

            var regions = new List<TextSpan>();
            if (unit is MemberDeclarationSyntax member)
                regions.Add(SignatureRegion(member));

            foreach (var symbol in SymbolsIn(unit, model, token.SpanStart))
                regions.AddRange(DeclarationRegions(symbol));

            var before = sites.Count(s => s.Active);
            foreach (var site in sites.Where(s => s.Active && regions.Any(r => r.Contains(s.Node.Span))))
                site.Active = false;

            if (sites.Count(s => s.Active) == before)
                foreach (var site in sites.Where(s => s.Active && unit.Span.Contains(s.Node.Span)))
                    site.Active = false;

            changed |= sites.Count(s => s.Active) != before;
        }

        return changed;
    }

    static (int Original, Site? Hit) ToOriginal(int position, Built built)
    {
        var offset = ScriptCompilation.Prefix.Length;
        var delta = 0;

        foreach (var part in built.Parts)
        {
            var start = part.OriginalStart - offset + delta;
            var end = start + part.Text.Length;

            if (position < start)
                break;

            if (position < end && part.Site != null)
                return (part.OriginalStart - offset, part.Site);

            delta += part.Text.Length - part.OriginalLength;
        }

        return (position - delta, null);
    }

    static IEnumerable<ISymbol> SymbolsIn(SyntaxNode unit, SemanticModel model, int errorPosition)
    {
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var node in unit.DescendantNodesAndSelf())
        {
            if (node is not (IdentifierNameSyntax or ObjectCreationExpressionSyntax))
                continue;

            foreach (var symbol in SymbolHelpers.Referenced(model.GetSymbolInfo(node)))
                symbols.Add(SymbolHelpers.Canonical(symbol));
        }

        if (unit is ReturnStatementSyntax or YieldStatementSyntax && model.GetEnclosingSymbol(errorPosition) is { } enclosing)
            symbols.Add(SymbolHelpers.Canonical(enclosing));

        return symbols.Where(s => s is not (INamedTypeSymbol or INamespaceSymbol));
    }

    // Where a symbol's own type is written: a variable's whole declaration (type and initializer), a
    // parameter, or a method's signature.
    static IEnumerable<TextSpan> DeclarationRegions(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            switch (reference.GetSyntax())
            {
                case VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }:
                    yield return declaration.Span;
                    break;
                case ParameterSyntax parameter:
                    yield return parameter.Span;
                    break;
                case MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax:
                    yield return SignatureRegion((MemberDeclarationSyntax)reference.GetSyntax());
                    break;
            }
        }
    }

    static TextSpan SignatureRegion(MemberDeclarationSyntax member)
    {
        var end = member switch
        {
            MethodDeclarationSyntax m => m.Body?.SpanStart ?? m.ExpressionBody?.SpanStart ?? m.Span.End,
            ConstructorDeclarationSyntax c => c.Body?.SpanStart ?? c.ExpressionBody?.SpanStart ?? c.Span.End,
            _ => member.Span.End
        };

        return TextSpan.FromBounds(member.SpanStart, end);
    }
}
