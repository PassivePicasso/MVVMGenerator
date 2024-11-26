using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Diagnostics;
using MVVM.Generator.Interfaces;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;
internal class AutoNotifyGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoNotifyAttribute>
{
    private const string LogPrefix = "AutoNotifyGenerator: ";
    private const string AttrUsageName = nameof(AttributeUsageAttribute);
    private const string AttrTargetName = nameof(AttributeTargets);
    private const string AttrTypeName = nameof(AutoNotifyAttribute);
    private const string INCCName = nameof(INotifyCollectionChanged);

    private readonly IDependencyAnalyzer _dependencyAnalyzer;
    private readonly PropertyGenerator _propertyGenerator;
    private ImmutableDictionary<string, ImmutableHashSet<string>>? _cachedDependencies;

    public AutoNotifyGenerator()
    {
        LogManager.Log($"{LogPrefix}Initializing generator");
        _dependencyAnalyzer = new AttributeProcessor();
        _propertyGenerator = new PropertyGenerator();
    }

    public override bool ValidateSymbol<TVS>(TVS symbol)
    {
        LogManager.Log($"{LogPrefix}Validating symbol {symbol?.GetType().Name}");

        IFieldSymbol fieldSymbol = symbol as IFieldSymbol;
        if (fieldSymbol == null)
        {
            Debugger.Break();
            LogManager.LogError($"{LogPrefix}Symbol is not an IFieldSymbol");
            return false;
        }

        if (fieldSymbol.Type.IsStatic)
        {
            LogManager.LogError($"{LogPrefix}Static type detected: {fieldSymbol.Name} of type {fieldSymbol.Type.Name}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoNotify.StaticType,
                fieldSymbol.Locations.FirstOrDefault(),
                fieldSymbol.Name,
                fieldSymbol.Type.Name));
            return false;
        }

        LogManager.Log($"{LogPrefix}Analyzing dependencies for {fieldSymbol.ContainingType.Name}");
        _cachedDependencies = _dependencyAnalyzer.AnalyzeDependencies(fieldSymbol.ContainingType, Context);

        // Check for missing dependencies
        foreach (var dep in _cachedDependencies.GetValueOrDefault(fieldSymbol.Name, ImmutableHashSet<string>.Empty))
        {
            if (!_cachedDependencies.ContainsKey(dep))
            {
                LogManager.LogError($"{LogPrefix}Missing dependency: {dep} for field {fieldSymbol.Name}");
                Context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.Generator.AutoNotify.DependencyNotFound,
                    fieldSymbol.Locations.FirstOrDefault(),
                    fieldSymbol.Name,
                    dep));
                return false;
            }
        }

        // Check for circular dependencies
        if (HasCircularDependencies(_cachedDependencies))
        {
            LogManager.LogError($"{LogPrefix}Circular dependency detected for field {fieldSymbol.Name}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoNotify.CircularDependency,
                fieldSymbol.Locations.FirstOrDefault(),
                fieldSymbol.Name));
            return false;
        }

        LogManager.Log($"{LogPrefix}Successfully validated {fieldSymbol.Name}");
        return true;
    }

    protected override void AddUsings(List<string> usings, IFieldSymbol fieldSymbol)
    {
        LogManager.Log($"{LogPrefix}Adding usings for {fieldSymbol.Name}");
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
        if (_cachedDependencies == null)
            return;

        LogManager.Log($"{LogPrefix}Generating properties for {fieldSymbol.Name}");
        _propertyGenerator.AddProperties(properties, fieldSymbol, _cachedDependencies);
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
        LogManager.Log($"{LogPrefix}Checking for circular dependencies");
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