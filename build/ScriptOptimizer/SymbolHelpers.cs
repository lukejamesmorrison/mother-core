using Microsoft.CodeAnalysis;

static class SymbolHelpers
{
    // The symbol a reference points at, with reduced extension methods and constructed generics folded back
    // onto their declaration, so every reference to one member compares equal.
    public static ISymbol Canonical(ISymbol symbol) =>
        (symbol is IMethodSymbol method ? method.ReducedFrom ?? method : symbol).OriginalDefinition;

    public static IEnumerable<ISymbol> Referenced(SymbolInfo info) =>
        info.Symbol != null ? new[] { info.Symbol } : info.CandidateSymbols;

    public static bool ImplementsInterface(ISymbol symbol)
    {
        var type = symbol.ContainingType;
        if (type == null)
            return false;

        foreach (var contract in type.AllInterfaces)
            foreach (var member in contract.GetMembers())
                if (SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), symbol))
                    return true;

        return false;
    }

    // Members whose signature or accessibility is dictated by something else: overrides, virtual and abstract
    // members, and interface implementations.
    public static bool IsBoundByContract(ISymbol symbol) =>
        symbol.IsOverride || symbol.IsVirtual || symbol.IsAbstract ||
        (symbol is IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 }) ||
        (symbol is IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 }) ||
        ImplementsInterface(symbol);
}
