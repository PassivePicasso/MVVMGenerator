using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Utilities;

public static class SymbolExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetTypeMembersRecursively(this INamespaceSymbol namespaceSymbol)
    {
        foreach (var namespaceMember in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var typeMember in GetTypeMembersRecursively(namespaceMember))
            {
                yield return typeMember;
            }
        }

        foreach (var typeMember in namespaceSymbol.GetTypeMembers())
        {
            yield return typeMember;

            foreach (var nestedType in typeMember.GetTypeMembersRecursively())
            {
                yield return nestedType;
            }
        }
    }

    public static IEnumerable<INamedTypeSymbol> GetTypeMembersRecursively(this INamedTypeSymbol typeSymbol)
    {
        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            yield return nestedType;

            foreach (var nestedNestedType in nestedType.GetTypeMembersRecursively())
            {
                yield return nestedNestedType;
            }
        }
    }
}