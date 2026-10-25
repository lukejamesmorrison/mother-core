using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

// Drops "public" from methods, properties and fields that nothing outside their own type uses. Members
// default to private, so this saves 7 characters each. Overrides, virtual and abstract members, interface
// implementations, constructors and members the compiler looks up by name stay as they are.
static class AccessNarrower
{
    static readonly HashSet<string> LookedUpByName = new() { "GetEnumerator", "MoveNext", "Current", "Add", "Dispose", "Reset" };

    public static List<ScriptEdit> FindEdits(string script, ScriptCompilation compilation)
    {
        var model = compilation.Compilation.GetSemanticModel(compilation.Tree);
        var root = compilation.Tree.GetRoot();

        var references = new Dictionary<ISymbol, List<int>>(SymbolEqualityComparer.Default);
        foreach (var node in root.DescendantNodes())
        {
            if (node is not (IdentifierNameSyntax or GenericNameSyntax))
                continue;

            foreach (var symbol in SymbolHelpers.Referenced(model.GetSymbolInfo(node)))
            {
                var key = SymbolHelpers.Canonical(symbol);
                if (!references.TryGetValue(key, out var positions))
                    references[key] = positions = new List<int>();
                positions.Add(node.SpanStart);
            }
        }

        var edits = new List<ScriptEdit>();
        foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (member.Parent is not (ClassDeclarationSyntax or StructDeclarationSyntax))
                continue;

            var owner = (TypeDeclarationSyntax)member.Parent;
            if (owner.Identifier.Text == "Program")
                continue;

            var (modifiers, symbols) = Describe(model, member);
            var keyword = modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PublicKeyword));
            if (keyword.IsKind(SyntaxKind.None) || symbols.Count == 0)
                continue;

            if (!symbols.All(s => CanNarrow(s, owner.Span, references)))
                continue;

            // "get;private set;" is only valid on a property that is more accessible than its accessor.
            var accessorModifiers = member is PropertyDeclarationSyntax { AccessorList: { } accessors }
                ? accessors.Accessors.SelectMany(a => a.Modifiers).ToList()
                : new List<SyntaxToken>();

            if (accessorModifiers.Any(m => !m.IsKind(SyntaxKind.PrivateKeyword)))
                continue;

            foreach (var token in accessorModifiers.Prepend(keyword))
                edits.Add(ScriptEdits.Replace(script, TokenWithSpace(token), ""));
        }

        return edits;
    }

    static TextSpan TokenWithSpace(SyntaxToken token)
    {
        var trailingIsWhitespace = token.TrailingTrivia.All(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia));
        return TextSpan.FromBounds(token.SpanStart, trailingIsWhitespace ? token.FullSpan.End : token.Span.End);
    }

    static (SyntaxTokenList Modifiers, List<ISymbol> Symbols) Describe(SemanticModel model, MemberDeclarationSyntax member)
    {
        var symbols = new List<ISymbol>();

        switch (member)
        {
            case MethodDeclarationSyntax method:
                AddIfNotNull(symbols, model.GetDeclaredSymbol(method));
                return (method.Modifiers, symbols);

            case PropertyDeclarationSyntax property:
                AddIfNotNull(symbols, model.GetDeclaredSymbol(property));
                return (property.Modifiers, symbols);

            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                    AddIfNotNull(symbols, model.GetDeclaredSymbol(variable));
                return (field.Modifiers, symbols);

            default:
                return (default, symbols);
        }
    }

    static void AddIfNotNull(List<ISymbol> symbols, ISymbol? symbol)
    {
        if (symbol != null)
            symbols.Add(symbol);
    }

    static bool CanNarrow(ISymbol symbol, TextSpan owner, Dictionary<ISymbol, List<int>> references)
    {
        if (SymbolHelpers.IsBoundByContract(symbol) || LookedUpByName.Contains(symbol.Name))
            return false;

        if (symbol is IMethodSymbol { MethodKind: not MethodKind.Ordinary } || symbol is IPropertySymbol { IsIndexer: true })
            return false;

        return !references.TryGetValue(SymbolHelpers.Canonical(symbol), out var positions) || positions.All(owner.Contains);
    }
}
