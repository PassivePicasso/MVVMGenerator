using Microsoft.CodeAnalysis;

using System.Collections.Generic;
using System.Linq;

namespace MVVM.Generator.Utilities
{
    public static class NamespaceExtractor
    {
        public static void AddNamespaceUsings(List<string> usings, ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return;

            // Add the namespace of the type itself
            if (typeSymbol.ContainingNamespace != null && !typeSymbol.ContainingNamespace.IsGlobalNamespace)
                usings.Add($"using {typeSymbol.ContainingNamespace};");

            // Recursively add namespaces for nested types and their containing types
            if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                foreach (var typeArgSymbol in namedTypeSymbol.TypeArguments)
                    AddNamespaceUsings(usings, typeArgSymbol);

                if (namedTypeSymbol.ContainingType != null)
                {
                    AddNamespaceUsings(usings, namedTypeSymbol.ContainingType);
                    AddStaticUsingForContainingType(usings, namedTypeSymbol.ContainingType);
                }
            }
            else if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                // Handle array types
                AddNamespaceUsings(usings, arrayTypeSymbol.ElementType);
            }
        }

        private static void AddStaticUsingForContainingType(List<string> usings, INamedTypeSymbol containingType)
        {
            if (containingType == null) return;

            // Add static using for the containing type
            if (containingType.ContainingNamespace != null && !containingType.ContainingNamespace.IsGlobalNamespace)
                usings.Add($"using static {containingType.ContainingNamespace}.{containingType.Name};");

            // Recursively add static usings for parent containing types
            if (containingType.ContainingType != null)
                AddStaticUsingForContainingType(usings, containingType.ContainingType);
        }
    }
}
