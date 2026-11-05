using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

// Finds explicit local types that can be replaced with "var" without changing the program:
// the declared type must be exactly the type of the initializer (no conversion), and "var" must be shorter.
static class VarRewriter
{
    const int VarLength = 3;

    public static List<TextSpan> FindEdits(ScriptCompilation script)
    {
        var model = script.Compilation.GetSemanticModel(script.Tree);
        var edits = new List<TextSpan>();

        foreach (var node in script.Tree.GetRoot().DescendantNodes())
        {
            switch (node)
            {
                case VariableDeclarationSyntax declaration when IsLocal(declaration):
                    if (CanUseVar(model, declaration))
                        edits.Add(declaration.Type.Span);
                    break;

                case ForEachStatementSyntax forEach:
                    if (CanUseVar(model, forEach))
                        edits.Add(forEach.Type.Span);
                    break;
            }
        }

        return edits;
    }

    static bool IsLocal(VariableDeclarationSyntax declaration) =>
        declaration.Parent switch
        {
            LocalDeclarationStatementSyntax local => !local.IsConst,
            ForStatementSyntax => true,
            UsingStatementSyntax => true,
            _ => false
        };

    static bool IsCandidateType(TypeSyntax type) =>
        !type.IsVar && type is not RefTypeSyntax && type.Span.Length > VarLength;

    static bool CanUseVar(SemanticModel model, VariableDeclarationSyntax declaration)
    {
        if (!IsCandidateType(declaration.Type) || declaration.Variables.Count != 1)
            return false;

        var value = declaration.Variables[0].Initializer?.Value;
        if (value == null)
            return false;

        var declared = DeclaredType(model, declaration.Type);
        if (declared == null)
            return false;

        var info = model.GetTypeInfo(value);
        return Same(declared, info.Type) && Same(declared, info.ConvertedType);
    }

    static bool CanUseVar(SemanticModel model, ForEachStatementSyntax forEach)
    {
        if (!IsCandidateType(forEach.Type))
            return false;

        var declared = DeclaredType(model, forEach.Type);
        if (declared == null)
            return false;

        var info = model.GetForEachStatementInfo(forEach);
        return Same(declared, info.ElementType) && info.ElementConversion.IsIdentity;
    }

    static ITypeSymbol? DeclaredType(SemanticModel model, TypeSyntax type)
    {
        var symbol = model.GetTypeInfo(type).Type;
        return symbol == null || symbol.TypeKind is TypeKind.Error or TypeKind.Dynamic ? null : symbol;
    }

    static bool Same(ITypeSymbol declared, ITypeSymbol? other) =>
        other != null && SymbolEqualityComparer.Default.Equals(declared, other);
}
