using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.CodeAnalysis;
using MVVM.Generator.Attributes;
using MVVM.Generator.Interfaces;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

internal class AutoNotifyGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoNotifyAttribute>
{
    private const string AttrUsageName = nameof(AttributeUsageAttribute);
    private const string AttrTargetName = nameof(AttributeTargets);
    private const string AttrTypeName = nameof(AutoNotifyAttribute);
    private const string INCCName = nameof(INotifyCollectionChanged);

    private readonly IDependencyAnalyzer _dependencyAnalyzer;
    private readonly PropertyGenerator _propertyGenerator;
    private readonly DiagnosticDescriptor _circularDependencyDescriptor;

    public AutoNotifyGenerator()
    {
        _dependencyAnalyzer = new AttributeProcessor();
        _propertyGenerator = new PropertyGenerator();

        _circularDependencyDescriptor = new DiagnosticDescriptor(
            id: "MVVM001",
            title: "Circular property dependency detected",
            messageFormat: "Property '{0}' has a circular dependency chain",
            category: "MVVM",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }

    protected override void AddUsings(List<string> usings, IFieldSymbol fieldSymbol)
    {
        usings.Add("using System.ComponentModel;");
        usings.Add("using System.Runtime.CompilerServices;");
        NamespaceExtractor.AddNamespaceUsings(usings, fieldSymbol.Type);

        foreach (var fieldAttribute in fieldSymbol.GetAttributes())
        {
            if (fieldAttribute?.AttributeClass?.Name == AttrTypeName) continue;
            var targets = fieldAttribute?.AttributeClass?.GetAttributes()
                .FirstOrDefault(aca => aca?.AttributeClass?.Name == AttrUsageName)?.ConstructorArguments
                .FirstOrDefault(ad => ad.Type?.Name == AttrTargetName)
                .Value;
            if (targets == null) continue;

            var result = (AttributeTargets)(int)targets;
            var validOnProperty = result.HasFlag(AttributeTargets.Property);
            if (validOnProperty && fieldAttribute?.AttributeClass != null)
                NamespaceExtractor.AddNamespaceUsings(usings, fieldAttribute.AttributeClass);
        }

        if (fieldSymbol.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
            foreach (var typeArgSymbol in namedTypeSymbol.TypeArguments)
                NamespaceExtractor.AddNamespaceUsings(usings, typeArgSymbol);

        var attributeData = fieldSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == AttrTypeName);
        var hasChangeHandler = attributeData?.NamedArguments
            .Any(na => na.Key == nameof(AutoNotifyAttribute.CollectionChangedHandlerName)) ?? false;
        var isNotifyingCollection = fieldSymbol.Type.AllInterfaces.Any(i => i.Name == INCCName);
        if (isNotifyingCollection && hasChangeHandler)
            usings.Add("using System.Collections.Specialized;");
    }

    protected override void AddProperties(List<string> properties, IFieldSymbol fieldSymbol)
    {
        var dependencies = _dependencyAnalyzer.AnalyzeDependencies(fieldSymbol.ContainingType);

        if (HasCircularDependencies(dependencies))
        {
            // context.ReportDiagnostic(Diagnostic.Create(
            //     _circularDependencyDescriptor,
            //     fieldSymbol.Locations.FirstOrDefault(),
            //     fieldSymbol.Name));
            return;
        }

        _propertyGenerator.AddProperties(properties, fieldSymbol, dependencies);
    }

    protected override void AddInterfaces(List<string> interfaces, IFieldSymbol fieldSymbol)
    {
        if (!interfaces.Contains("INotifyPropertyChanged"))
            interfaces.Add("INotifyPropertyChanged");
    }

    protected override void AddInterfaceImplementations(List<string> impls, IFieldSymbol fieldSymbol)
    {
        if (!impls.Any(i => i.Contains("PropertyChanged")))
        {
            impls.Add($$"""

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
""");
        }
    }

    private bool HasCircularDependencies(ImmutableDictionary<string, ImmutableHashSet<string>> dependencies)
    {
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        bool HasCycle(string property)
        {
            if (stack.Contains(property))
                return true;
            if (visited.Contains(property))
                return false;

            visited.Add(property);
            stack.Add(property);

            if (dependencies.TryGetValue(property, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (HasCycle(dep))
                        return true;
                }
            }

            stack.Remove(property);
            return false;
        }

        return dependencies.Keys.Any(HasCycle);
    }
}