using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using MVVM.Generator.Attributes;
using MVVM.Generator.Interfaces;

namespace MVVM.Generator.Utilities;

public class AttributeProcessor : IDependencyAnalyzer
{
    private const string AutoNotifyAttributeName = nameof(AutoNotifyAttribute);
    private const string DependsOnAttributeName = nameof(DependsOnAttribute);

    private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableDictionary<string, ImmutableHashSet<string>>> _dependencyCache = new(SymbolEqualityComparer.Default);

    public ImmutableDictionary<string, ImmutableHashSet<string>> AnalyzeDependencies(
        INamedTypeSymbol symbol,
        SourceProductionContext context)
    {
        Trace.AutoFlush = true;
        Trace.Listeners.Add(new DefaultTraceListener());
        Trace.Listeners.Add(new TextWriterTraceListener("generator-debug.log"));
        Trace.WriteLine($"Analyzing dependencies for {symbol.Name}");
        var dependencyMap = new Dictionary<string, HashSet<string>>();


        var autoDependencies = BuildAutoDependencies(symbol);
        Trace.WriteLine($"Auto dependencies: {string.Join(", ", autoDependencies.SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key} -> {v}")))}");

        var manualDependencies = BuildManualDependencies(symbol);
        Trace.WriteLine($"Manual dependencies: {string.Join(", ", manualDependencies
            .SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key} -> {v}")))}");

        // Merge dependencies and log results
        foreach (var kvp in autoDependencies.Concat(manualDependencies))
        {
            var property = kvp.Key;
            if (!dependencyMap.ContainsKey(property))
            {
                dependencyMap[property] = new HashSet<string>();
            }
            var before = dependencyMap[property].Count;
            dependencyMap[property].UnionWith(kvp.Value);
        }
        Trace.WriteLine($"Merged dependencies: {string.Join(", ", dependencyMap.SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key} -> {v}")))}");

        return dependencyMap.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToImmutableHashSet());
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

    private ImmutableDictionary<string, ImmutableHashSet<string>> BuildManualDependencies(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>();
        var fieldToPropertyMap = BuildFieldToPropertyMap(typeSymbol);

        foreach (var member in typeSymbol.GetMembers())
        {
            var dependsOnAttribute = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == DependsOnAttributeName);

            if (dependsOnAttribute == null) continue;

            var dependencyNames = dependsOnAttribute.ConstructorArguments
                .FirstOrDefault()
                .Values
                .Select(v => v.Value?.ToString())
                .Where(v => v != null)
                .ToList()!;

            foreach (var propertyName in dependencyNames)
            {
                var resolvedNames = ResolvePropertyName(propertyName!, fieldToPropertyMap);
                foreach (var resolvedName in resolvedNames)
                {
                    if (!builder.ContainsKey(resolvedName))
                    {
                        builder[resolvedName] = [member.Name];
                    }
                    else
                    {
                        builder[resolvedName] = builder[resolvedName].Add(member.Name);
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    private Dictionary<string, string> BuildFieldToPropertyMap(INamedTypeSymbol typeSymbol)
    {
        var map = new Dictionary<string, string>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IFieldSymbol field &&
                field.GetAttributes().Any(a => a.AttributeClass?.Name == AutoNotifyAttributeName))
            {
                var propertyName = PropertyGenerator.GetPropertyName(field);

                map[field.Name] = propertyName;
                map[propertyName] = propertyName; // Allow direct property name reference
            }
        }
        return map;
    }

    private IEnumerable<string> ResolvePropertyName(string name, Dictionary<string, string> fieldToPropertyMap)
    {
        if (fieldToPropertyMap.TryGetValue(name, out var propertyName))
        {
            return [propertyName];
        }
        return [name];
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