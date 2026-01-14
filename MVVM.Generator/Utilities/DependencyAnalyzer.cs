using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MVVM.Generator.Utilities;

/// <summary>
/// Analyzes method/property bodies to find referenced fields and properties
/// for automatic CanExecuteChanged dependency discovery.
/// </summary>
public static class DependencyAnalyzer
{
    private const string LogPrefix = "DependencyAnalyzer: ";

    /// <summary>
    /// Finds all property names that a method or property depends on.
    /// Maps backing fields (e.g., _isPlaying) to their property names (IsPlaying).
    /// </summary>
    public static IReadOnlyList<string> GetDependencies(ISymbol canExecuteSymbol, SemanticModel? semanticModel)
    {
        LogManager.Log($"{LogPrefix}Analyzing dependencies for {canExecuteSymbol.Name}");

        var dependencies = new HashSet<string>();
        var containingType = canExecuteSymbol.ContainingType;

        // Get the syntax node for analysis
        var syntaxRef = canExecuteSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            LogManager.Log($"{LogPrefix}No syntax reference found for {canExecuteSymbol.Name}");
            return dependencies.ToList();
        }

        var syntax = syntaxRef.GetSyntax();

        // Find all identifier references in the body
        var identifiers = GetBodyIdentifiers(syntax);

        foreach (var identifier in identifiers)
        {
            var name = identifier.Identifier.Text;

            // Check if it's a field with underscore prefix (backing field pattern)
            if (name.StartsWith("_"))
            {
                var propertyName = FieldToPropertyName(name);
                // Verify the property exists
                if (containingType.GetMembers(propertyName).OfType<IPropertySymbol>().Any())
                {
                    dependencies.Add(propertyName);
                    LogManager.Log($"{LogPrefix}Found backing field dependency: {name} -> {propertyName}");
                }
            }

            // Check if it's a direct property reference
            var property = containingType.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault();
            if (property != null)
            {
                dependencies.Add(name);
                LogManager.Log($"{LogPrefix}Found property dependency: {name}");
            }
        }

        LogManager.Log($"{LogPrefix}Total dependencies for {canExecuteSymbol.Name}: {dependencies.Count}");
        return dependencies.ToList();
    }

    /// <summary>
    /// Gets all identifier names from the body of a method or property.
    /// </summary>
    private static IEnumerable<IdentifierNameSyntax> GetBodyIdentifiers(SyntaxNode syntax)
    {
        // Handle expression-bodied members: => expression
        var arrowClause = syntax.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().FirstOrDefault();
        if (arrowClause != null)
        {
            return arrowClause.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>();
        }

        // Handle block-bodied methods
        var block = syntax.DescendantNodes().OfType<BlockSyntax>().FirstOrDefault();
        if (block != null)
        {
            return block.DescendantNodes().OfType<IdentifierNameSyntax>();
        }

        // Handle property getters
        var getter = syntax.DescendantNodes().OfType<AccessorDeclarationSyntax>()
            .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter != null)
        {
            if (getter.ExpressionBody != null)
                return getter.ExpressionBody.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>();
            if (getter.Body != null)
                return getter.Body.DescendantNodes().OfType<IdentifierNameSyntax>();
        }

        return Enumerable.Empty<IdentifierNameSyntax>();
    }

    /// <summary>
    /// Converts a backing field name to its corresponding property name.
    /// _isPlaying -> IsPlaying
    /// _currentFrame -> CurrentFrame
    /// </summary>
    private static string FieldToPropertyName(string fieldName)
    {
        if (fieldName.StartsWith("_") && fieldName.Length > 1)
        {
            // Remove underscore and capitalize first letter
            return char.ToUpperInvariant(fieldName[1]) + fieldName.Substring(2);
        }
        return fieldName;
    }
}
