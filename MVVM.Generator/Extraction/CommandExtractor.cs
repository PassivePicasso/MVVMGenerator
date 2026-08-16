using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Diagnostics;
using MVVM.Generator.Models;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Extraction;

/// <summary>
/// Validates and models [AutoCommand] methods. Validation order and messages
/// match the previous symbol-based handler.
/// </summary>
internal static class CommandExtractor
{
    public static CommandModel Extract(IMethodSymbol methodSymbol)
    {
        var usings = new List<string> { "using System.Windows.Input;" };

        if (methodSymbol.Parameters.Length > 0)
            NamespaceExtractor.AddNamespaceUsings(usings, methodSymbol.Parameters[0].Type);

        var isAsync = IsAsync(methodSymbol);
        if (isAsync)
            usings.Add("using System.Threading.Tasks;");

        var canExecuteName = CanExecuteName(methodSymbol);
        var (canExecuteMember, isProperty) = CanExecuteMember(methodSymbol);

        var dependencies = canExecuteMember != null
            ? DependencyAnalyzer.GetDependencies(canExecuteMember, null)
            : (IReadOnlyList<string>)[];

        if (dependencies.Count > 0)
            usings.Add("using System.ComponentModel;");

        var additionalAttributes = AdditionalAttributes(usings, methodSymbol);

        var name = methodSymbol.Name;

        return new CommandModel(
            MethodName: name,
            FieldName: $"{name.Substring(0, 1).ToLower()}{name.Substring(1)}Command",
            ClassName: $"{name}CommandClass",
            OwnerTypeName: methodSymbol.ContainingType.Name,
            IsStatic: methodSymbol.IsStatic,
            IsAsync: isAsync,
            IsOverrideOfCommand: IsOverrideOfCommand(methodSymbol),
            ParameterTypeName: methodSymbol.Parameters.Length == 1
                ? methodSymbol.Parameters[0].Type.Name
                : null,
            CanExecuteName: canExecuteName,
            CanExecuteIsProperty: isProperty,
            Dependencies: EquatableArray.From(dependencies),
            AdditionalAttributes: EquatableArray.From(additionalAttributes),
            Usings: EquatableArray.From(usings));
    }

    public static bool Validate(IMethodSymbol methodSymbol, List<DiagnosticInfo> diagnostics)
    {
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.NotPublic, methodSymbol, methodSymbol.Name));
            return false;
        }

        if (methodSymbol.Parameters.Length > 1)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidMethodSignature, methodSymbol,
                methodSymbol.Name,
                $"Method has {methodSymbol.Parameters.Length} parameters, maximum allowed is 1."));
            return false;
        }

        if (!IsValidReturnType(methodSymbol.ReturnType))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidMethodSignature, methodSymbol,
                methodSymbol.Name,
                $"Return type must be void or Task, found {methodSymbol.ReturnType}."));
            return false;
        }

        var (canExecuteMember, isProperty) = CanExecuteMember(methodSymbol);
        if (canExecuteMember != null)
        {
            return isProperty
                ? ValidateCanExecuteProperty(methodSymbol, (IPropertySymbol)canExecuteMember, diagnostics)
                : ValidateCanExecuteMethod(methodSymbol, (IMethodSymbol)canExecuteMember, diagnostics);
        }

        if (!string.IsNullOrEmpty(CanExecuteName(methodSymbol)))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature, methodSymbol,
                CanExecuteName(methodSymbol),
                "Member not found. Expected a method or property with this name."));
            return false;
        }

        return true;
    }

    private static bool ValidateCanExecuteMethod(
        IMethodSymbol commandMethod, IMethodSymbol canExecuteMethod, List<DiagnosticInfo> diagnostics)
    {
        if (canExecuteMethod.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature, canExecuteMethod,
                canExecuteMethod.Name,
                $"Return type must be bool, found {canExecuteMethod.ReturnType}."));
            return false;
        }

        if (canExecuteMethod.Parameters.Length != commandMethod.Parameters.Length)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature, canExecuteMethod,
                canExecuteMethod.Name,
                $"Parameter count mismatch. Expected {commandMethod.Parameters.Length}, found {canExecuteMethod.Parameters.Length}."));
            return false;
        }

        for (var i = 0; i < commandMethod.Parameters.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    commandMethod.Parameters[i].Type, canExecuteMethod.Parameters[i].Type))
                continue;

            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature, canExecuteMethod,
                canExecuteMethod.Name,
                $"Parameter type mismatch at position {i}. Expected {commandMethod.Parameters[i].Type}, found {canExecuteMethod.Parameters[i].Type}."));
            return false;
        }

        return true;
    }

    private static bool ValidateCanExecuteProperty(
        IMethodSymbol commandMethod, IPropertySymbol canExecuteProperty, List<DiagnosticInfo> diagnostics)
    {
        if (canExecuteProperty.Type.SpecialType != SpecialType.System_Boolean)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature, canExecuteProperty,
                canExecuteProperty.Name,
                $"Property type must be bool, found {canExecuteProperty.Type}."));
            return false;
        }

        if (commandMethod.Parameters.Length > 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature, canExecuteProperty,
                canExecuteProperty.Name,
                "Properties cannot be used as CanExecute for commands with parameters. Use a method instead."));
            return false;
        }

        return true;
    }

    private static string CanExecuteName(IMethodSymbol methodSymbol)
    {
        var attributeData = methodSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == nameof(AutoCommandAttribute));

        if (attributeData?.ConstructorArguments.Length > 0
            && attributeData.ConstructorArguments[0].Value is string canExecuteMethodName)
        {
            return canExecuteMethodName;
        }

        return string.Empty;
    }

    private static (ISymbol? Symbol, bool IsProperty) CanExecuteMember(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Parameters.Length > 0)
            return (CanExecuteMethod(methodSymbol), false);

        var property = CanExecuteProperty(methodSymbol);
        if (property != null) return (property, true);

        return (CanExecuteMethod(methodSymbol), false);
    }

    private static IMethodSymbol? CanExecuteMethod(IMethodSymbol methodSymbol)
    {
        var name = CanExecuteName(methodSymbol);
        if (string.IsNullOrEmpty(name)) return null;

        return methodSymbol.ContainingType.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == name);
    }

    private static IPropertySymbol? CanExecuteProperty(IMethodSymbol methodSymbol)
    {
        var name = CanExecuteName(methodSymbol);
        if (string.IsNullOrEmpty(name)) return null;

        return methodSymbol.ContainingType.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name == name);
    }

    private static List<string> AdditionalAttributes(List<string> usings, IMethodSymbol methodSymbol)
    {
        var results = new List<string>();

        foreach (var ad in methodSymbol.GetAttributes()
                     .Where(ad => ad.AttributeClass?.Name == nameof(AddAttributeAttribute)))
        {
            if (ad.ConstructorArguments[0].Value is not INamedTypeSymbol attributeType) continue;

            var args = ad.ConstructorArguments[1].Values
                .Select(v => v.Value)
                .Where(v => v != null)
                .ToArray();

            usings.Add($"using {attributeType.ContainingNamespace.ToDisplayString()};");

            var name = attributeType.Name;
            name = name.Substring(0, name.Length - "Attribute".Length);

            var decorator = name;
            if (args.Length > 0)
                decorator += $"({string.Join(", ", args.Select(a => a is string ? $"\"{a}\"" : a!.ToString()))})";

            results.Add($"""
        [{decorator}]
""");
        }

        return results;
    }

    private static bool IsOverrideOfCommand(IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.IsOverride) return false;

        return methodSymbol.OverriddenMethod?.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == nameof(AutoCommandAttribute)) ?? false;
    }

    private static bool IsValidReturnType(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_Void || IsTask(type);
    }

    private static bool IsAsync(IMethodSymbol method) => IsTask(method.ReturnType);

    private static bool IsTask(ITypeSymbol type)
    {
        return type.Name == "Task"
            && type.ContainingNamespace?.ToString() == "System.Threading.Tasks";
    }
}
