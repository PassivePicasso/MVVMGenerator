using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Diagnostics;
using MVVM.Generator.Models;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Extraction;

/// <summary>
/// Builds the complete ClassModel for one attributed class.
/// </summary>
/// <remarks>
/// Reproduces the previous gating exactly: a generator ran only if at least one
/// of its members validated, and it then processed every member carrying its
/// attribute -- including ones that failed validation.
/// </remarks>
internal static class ClassModelExtractor
{
    public static ClassModel? Extract(INamedTypeSymbol classSymbol)
    {
        var notifyFields = MembersWith<IFieldSymbol>(classSymbol, nameof(AutoNotifyAttribute));
        var commandMethods = MembersWith<IMethodSymbol>(classSymbol, nameof(AutoCommandAttribute));
        var dependencyFields = MembersWith<IFieldSymbol>(classSymbol, nameof(AutoDPropAttribute));
        var styledFields = MembersWith<IFieldSymbol>(classSymbol, nameof(AutoSPropAttribute));

        if (notifyFields.Length == 0
            && commandMethods.Length == 0
            && dependencyFields.Length == 0
            && styledFields.Length == 0)
        {
            return null;
        }

        var diagnostics = new List<DiagnosticInfo>();

        var dependencies = notifyFields.Length > 0
            ? new AttributeProcessor().AnalyzeDependencies(classSymbol, default)
            : ImmutableDictionary<string, ImmutableHashSet<string>>.Empty;

        var notifyActive = ValidateAll(notifyFields, field => ValidateNotify(field, dependencies, diagnostics));
        var commandActive = ValidateAll(commandMethods, method => CommandExtractor.Validate(method, diagnostics));

        var model = new ClassModel(
            Namespace: classSymbol.ContainingNamespace.ToDisplayString(),
            ClassName: classSymbol.Name,
            HintName: $"{classSymbol.Name}.ViewModel.cs",
            BaseImplementsInpc: NotifyFieldExtractor.BaseImplementsInpc(classSymbol),
            NotifyFields: notifyActive
                ? EquatableArray.From(notifyFields.Select(f => NotifyFieldExtractor.Extract(f, dependencies)))
                : EquatableArray<NotifyFieldModel>.Empty,
            Commands: commandActive
                ? EquatableArray.From(commandMethods.Select(CommandExtractor.Extract))
                : EquatableArray<CommandModel>.Empty,
            DependencyProperties: EquatableArray.From(
                dependencyFields.Select(f => BackingPropertyExtractor.Extract(f, "using System.Windows;"))),
            StyledProperties: EquatableArray.From(
                styledFields.Select(f => BackingPropertyExtractor.Extract(f, "using Avalonia;"))),
            Diagnostics: EquatableArray.From(diagnostics));

        return model.HasContent || !model.Diagnostics.IsEmpty ? model : null;
    }

    private static ImmutableArray<TSymbol> MembersWith<TSymbol>(INamedTypeSymbol classSymbol, string attributeName)
        where TSymbol : ISymbol
    {
        return classSymbol.GetMembers()
            .Where(member => member.GetAttributes()
                .Any(attribute => attribute.AttributeClass?.Name == attributeName))
            .OfType<TSymbol>()
            .ToImmutableArray();
    }

    // Every member is validated so its diagnostics are recorded, then the
    // generator runs if any one of them passed.
    private static bool ValidateAll<TSymbol>(ImmutableArray<TSymbol> symbols, System.Func<TSymbol, bool> validate)
    {
        var anyValid = false;
        foreach (var symbol in symbols)
        {
            if (validate(symbol)) anyValid = true;
        }
        return anyValid;
    }

    private static bool ValidateNotify(
        IFieldSymbol fieldSymbol,
        ImmutableDictionary<string, ImmutableHashSet<string>> dependencies,
        List<DiagnosticInfo> diagnostics)
    {
        if (fieldSymbol.Type.IsStatic)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoNotify.StaticType, fieldSymbol,
                fieldSymbol.Name, fieldSymbol.Type.Name));
            return false;
        }

        foreach (var dependency in dependencies.GetValueOrDefault(fieldSymbol.Name, ImmutableHashSet<string>.Empty))
        {
            if (dependencies.ContainsKey(dependency)) continue;

            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoNotify.DependencyNotFound, fieldSymbol,
                fieldSymbol.Name, dependency));
            return false;
        }

        if (HasCircularDependencies(dependencies))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoNotify.CircularDependency, fieldSymbol,
                fieldSymbol.Name));
            return false;
        }

        return true;
    }

    private static bool HasCircularDependencies(ImmutableDictionary<string, ImmutableHashSet<string>> dependencies)
    {
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        bool HasCycle(string property)
        {
            if (stack.Contains(property)) return true;
            if (visited.Contains(property)) return false;

            visited.Add(property);
            stack.Add(property);

            if (dependencies.TryGetValue(property, out var deps))
            {
                foreach (var dependency in deps)
                {
                    if (HasCycle(dependency)) return true;
                }
            }

            stack.Remove(property);
            return false;
        }

        return dependencies.Keys.Any(HasCycle);
    }
}
