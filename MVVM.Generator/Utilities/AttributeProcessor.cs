using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using MVVM.Generator.Attributes;
using System.Linq;
using MVVM.Generator.Interfaces;

namespace MVVM.Generator.Utilities;

public class AttributeProcessor : IDependencyAnalyzer
{
    private const string AutoNotifyAttributeName = nameof(AutoNotifyAttribute);
    private const string DependsOnAttributeName = nameof(DependsOnAttribute);

    private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableDictionary<string, ImmutableHashSet<string>>> _dependencyCache = new(SymbolEqualityComparer.Default);

    public ImmutableDictionary<string, ImmutableHashSet<string>> AnalyzeDependencies(INamedTypeSymbol typeSymbol)
    {
        return _dependencyCache.GetOrAdd(typeSymbol, symbol =>
        {
            var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>();
            var manualDependencies = BuildManualDependencies(symbol);
            var autoDependencies = BuildAutoDependencies(symbol);

            // Merge manual and auto dependencies
            foreach (var kvp in manualDependencies.Concat(autoDependencies))
            {
                var property = kvp.Key;
                var dependencies = kvp.Value;
                if (!builder.ContainsKey(property))
                {
                    builder[property] = dependencies;
                }
                else
                {
                    builder[property] = builder[property].Union(dependencies);
                }
            }

            return builder.ToImmutable();
        });
    }

    private ImmutableDictionary<string, ImmutableHashSet<string>> BuildManualDependencies(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>();

        foreach (var member in typeSymbol.GetMembers())
        {
            var dependsOnAttribute = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == DependsOnAttributeName);

            if (dependsOnAttribute == null) continue;

            var propertyNames = dependsOnAttribute.ConstructorArguments
                .FirstOrDefault()
                .Values
                .Select(v => v.Value?.ToString())
                .Where(v => v != null)
                .ToImmutableHashSet()!;

            foreach (var propertyName in propertyNames)
            {
                if (!builder.ContainsKey(propertyName))
                {
                    builder[propertyName] = ImmutableHashSet.Create(member.Name);
                }
                else
                {
                    builder[propertyName] = builder[propertyName].Add(member.Name);
                }
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableDictionary<string, ImmutableHashSet<string>> BuildAutoDependencies(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>();
#pragma warning disable RS1024 // Symbols should be compared for equality
        var autoNotifyFields = typeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == AutoNotifyAttributeName))
            .ToDictionary(f => f, GetAutoNotifyPropertyName);
#pragma warning restore RS1024 // Symbols should be compared for equality


        foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsImplicitlyDeclared) continue;

            var syntax = property.DeclaringSyntaxReferences
                .FirstOrDefault()
                ?.GetSyntax() as PropertyDeclarationSyntax;
            if (syntax == null) continue;

            var referencedSymbols = syntax.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => property.ContainingType.GetMembers(id.Identifier.Text).FirstOrDefault())
                .Where(s => s != null)
                .Distinct(SymbolEqualityComparer.Default)
                .ToList();

            foreach (var field in autoNotifyFields)
            {
                if (referencedSymbols.Contains(field.Key, SymbolEqualityComparer.Default))
                {
                    if (!builder.ContainsKey(field.Value))
                    {
                        builder[field.Value] = [property.Name];
                    }
                    else
                    {
                        builder[field.Value] = builder[field.Value].Add(property.Name);
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    private static string GetAutoNotifyPropertyName(IFieldSymbol fieldSymbol)
    {
        var name = fieldSymbol.Name;
        if (name.StartsWith("_"))
            name = name.Substring(1);
        else if (name.StartsWith("s_"))
            name = name.Substring(2);
        return char.ToUpper(name[0]) + name.Substring(1);
    }
}