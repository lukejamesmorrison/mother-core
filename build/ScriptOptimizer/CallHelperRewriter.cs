using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Routes repeated calls to a few BCL methods through one-character extension methods declared in a single
// static class after Program: "string.IsNullOrEmpty(s)" becomes "s.I()", "s.Substring(1)" becomes "s.S(1)" and
// "d.ContainsKey(k)" becomes "d.C(k)". Only exact methods from an allow-list are rewritten, and a helper is only
// emitted when the characters it saves repay its declaration. Calls on a null literal, calls with named or optional
// arguments, calls through interfaces (a different symbol) and extension methods are left alone. The extra call per
// use adds script instructions, so the allow-list stays short and each addition should be measured in game.
static class CallHelperRewriter
{
    static readonly HashSet<string> Allowed = new()
    {
        "System.String.IsNullOrEmpty",
        "System.String.Substring",
        "System.Collections.Generic.Dictionary`2.ContainsKey",
        "System.Collections.Generic.Dictionary`2.TryGetValue",
        "System.Double.TryParse"
    };

    static readonly string[] TypeParameterNames = { "K", "V", "W", "X" };

    static readonly HashSet<string> ImportedNamespaces = Regex.Matches(ScriptCompilation.Usings, @"using ([\w.]+);")
        .Select(m => m.Groups[1].Value)
        .ToHashSet();

    // "static class N{" and "}", without "public": RemoveKeywords.ps1 strips it from top-level types.
    const int ClassOverhead = 15;

    sealed class Helper
    {
        public required IMethodSymbol Method { get; init; }
        public string Name { get; set; } = "";
        public string Declaration { get; set; } = "";
        public List<Site> Sites { get; } = new();

        // Characters saved before subtracting the helper's own name at each site.
        public int Gross => Sites.Sum(s => s.Gross);
        public int Net => Gross - Sites.Count * Name.Length - Declaration.Length;
    }

    sealed record Site(Helper Owner, InvocationExpressionSyntax Node, int Gross);

    public sealed record Result(string Script, int Helpers, int Sites);

    public static Result? Rewrite(string script, ScriptCompilation compilation)
    {
        var root = compilation.Tree.GetRoot();
        var model = compilation.Compilation.GetSemanticModel(compilation.Tree);

        var byMethod = new Dictionary<IMethodSymbol, Helper>(SymbolEqualityComparer.Default);
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var found = Classify(invocation, model);
            if (found == null)
                continue;

            var (method, gross) = found.Value;
            if (!byMethod.TryGetValue(method, out var helper))
                byMethod[method] = helper = new Helper { Method = method };

            helper.Sites.Add(new Site(helper, invocation, gross));
        }

        if (byMethod.Count == 0)
            return null;

        var used = new HashSet<string>(root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText));
        var names = AliasRewriter.Names(used).GetEnumerator();
        names.MoveNext();
        var className = names.Current;

        var helpers = new List<Helper>();
        foreach (var helper in byMethod.Values.OrderByDescending(h => h.Gross))
        {
            names.MoveNext();
            helper.Name = names.Current;

            var declaration = Declare(helper.Method, helper.Name);
            if (declaration == null)
                continue;

            helper.Declaration = declaration;
            if (helper.Net > 0)
                helpers.Add(helper);
        }

        if (helpers.Sum(h => h.Net) - className.Length - ClassOverhead <= 0)
            return null;

        var active = helpers.SelectMany(h => h.Sites).ToDictionary(s => s.Node);
        var rewritten = new Rewriter(active).Visit(root);
        var text = rewritten.ToFullString();
        var rewrittenScript = text.Substring(ScriptCompilation.Prefix.Length, text.Length - ScriptCompilation.Prefix.Length - ScriptCompilation.Suffix.Length);

        var program = ScriptCompilation.Parse(rewrittenScript).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == "Program");
        if (program == null)
            return null;

        var insertAt = program.CloseBraceToken.Span.End - ScriptCompilation.Prefix.Length;
        var declarations = $"static class {className}{{{string.Join("", helpers.Select(h => h.Declaration))}}}";

        return new Result(rewrittenScript.Insert(insertAt, declarations), helpers.Count, helpers.Sum(h => h.Sites.Count));
    }

    sealed class Rewriter : CSharpSyntaxRewriter
    {
        readonly Dictionary<InvocationExpressionSyntax, Site> _sites;

        public Rewriter(Dictionary<InvocationExpressionSyntax, Site> sites) => _sites = sites;

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (!_sites.TryGetValue(node, out var site))
                return visited;

            var name = site.Owner.Name;

            if (!site.Owner.Method.IsStatic)
            {
                var renamed = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(name));
                return visited.Expression switch
                {
                    MemberAccessExpressionSyntax access => visited.WithExpression(access.WithName(renamed.WithTriviaFrom(access.Name))),
                    MemberBindingExpressionSyntax binding => visited.WithExpression(binding.WithName(renamed.WithTriviaFrom(binding.Name))),
                    _ => visited
                };
            }

            var arguments = visited.ArgumentList.Arguments;
            var receiver = arguments[0].Expression.ToString();
            if (!IsPrimary(node.ArgumentList.Arguments[0].Expression))
                receiver = "(" + receiver + ")";

            var rest = string.Join(",", arguments.Skip(1).Select(a => a.ToString()));
            return SyntaxFactory.ParseExpression($"{receiver}.{name}({rest})").WithTriviaFrom(visited);
        }
    }

    static (IMethodSymbol Method, int Gross)? Classify(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { ReducedFrom: null, IsGenericMethod: false } method ||
            method.Parameters.Any(p => p.IsOptional || p.IsParams))
            return null;

        var definition = method.OriginalDefinition;
        if (!Allowed.Contains(definition.ContainingType.ContainingNamespace.ToDisplayString() + "." + definition.ContainingType.MetadataName + "." + definition.Name))
            return null;

        if (definition.IsStatic)
            return ClassifyStatic(invocation, model, definition);

        if (!definition.ContainingType.IsReferenceType)
            return null;

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Expression: not BaseExpressionSyntax, Name: IdentifierNameSyntax n } => n,
            MemberBindingExpressionSyntax { Name: IdentifierNameSyntax n } => n,
            _ => null
        };

        return name == null ? null : (definition, name.Span.Length);
    }

    // The first argument becomes the receiver of an extension method, so it must be a string that is not null.
    static (IMethodSymbol Method, int Gross)? ClassifyStatic(InvocationExpressionSyntax invocation, SemanticModel model, IMethodSymbol definition)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (invocation.Expression is not MemberAccessExpressionSyntax || arguments.Count == 0 ||
            arguments.Any(a => a.NameColon != null) || !arguments[0].RefKindKeyword.IsKind(SyntaxKind.None) ||
            definition.Parameters[0].Type.SpecialType != SpecialType.System_String)
            return null;

        var first = arguments[0].Expression;
        if (model.GetTypeInfo(first).Type?.SpecialType != SpecialType.System_String)
            return null;

        var rest = arguments.Skip(1).Sum(a => a.Span.Length) + Math.Max(0, arguments.Count - 2);
        var rewritten = first.Span.Length + (IsPrimary(first) ? 0 : 2) + rest + 3;
        return (definition, invocation.Span.Length - rewritten);
    }

    // Expressions that can take ".Name(...)" without parentheses.
    static bool IsPrimary(ExpressionSyntax expression) =>
        expression.Kind() is SyntaxKind.IdentifierName or SyntaxKind.GenericName or SyntaxKind.SimpleMemberAccessExpression or
            SyntaxKind.InvocationExpression or SyntaxKind.ElementAccessExpression or SyntaxKind.ParenthesizedExpression or
            SyntaxKind.StringLiteralExpression or SyntaxKind.InterpolatedStringExpression or SyntaxKind.ThisExpression;

    static string? Declare(IMethodSymbol method, string name)
    {
        var owner = method.ContainingType;
        if (owner.TypeParameters.Length > TypeParameterNames.Length)
            return null;

        var map = new Dictionary<ITypeParameterSymbol, string>(SymbolEqualityComparer.Default);
        for (var i = 0; i < owner.TypeParameters.Length; i++)
            map[owner.TypeParameters[i]] = TypeParameterNames[i];

        var returnType = TypeText(method.ReturnType, map);
        var ownerType = TypeText(owner, map);
        if (returnType == null || ownerType == null)
            return null;

        var generics = map.Count == 0 ? "" : "<" + string.Join(",", map.Values) + ">";
        var parameters = new List<string>();
        var arguments = new List<string>();

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = method.Parameters[i];
            var type = TypeText(parameter.Type, map);
            var modifier = parameter.RefKind switch
            {
                RefKind.None => "",
                RefKind.Out => "out ",
                RefKind.Ref => "ref ",
                _ => null
            };

            if (type == null || modifier == null)
                return null;

            // The receiver is "a" for both shapes: the first parameter of a static method, "this" otherwise.
            var letter = (char)('a' + i + (method.IsStatic ? 0 : 1));
            parameters.Add(method.IsStatic && i == 0 ? $"this {type} {letter}" : $"{modifier}{type} {letter}");
            arguments.Add(modifier + letter);
        }

        if (!method.IsStatic)
            parameters.Insert(0, $"this {ownerType} a");

        var target = method.IsStatic ? ownerType : "a";
        return $"public static {returnType} {name}{generics}({string.Join(",", parameters)})=>{target}.{method.Name}({string.Join(",", arguments)});";
    }

    static string? TypeText(ITypeSymbol type, Dictionary<ITypeParameterSymbol, string> map)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return map.TryGetValue(parameter, out var mapped) ? mapped : null;

            case IArrayTypeSymbol { Rank: 1 } array:
                return TypeText(array.ElementType, map) is { } element ? element + "[]" : null;

            case INamedTypeSymbol { ContainingType: null } named:
                var text = named.SpecialType != SpecialType.None
                    ? named.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes))
                    : (ImportedNamespaces.Contains(named.ContainingNamespace.ToDisplayString()) ? "" : named.ContainingNamespace.ToDisplayString() + ".") + named.Name;

                if (named.TypeArguments.Length == 0)
                    return text;

                var arguments = named.TypeArguments.Select(a => TypeText(a, map)).ToList();
                return arguments.Any(a => a == null) ? null : $"{text}<{string.Join(",", arguments)}>";

            default:
                return null;
        }
    }
}
