using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Diagnostics;
using MVVM.Generator.Models;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Extraction;

/// <summary>
/// Validates and models [AutoCommand] methods. CanExecute members are resolved
/// through CanExecuteResolver so the generator and analyzer agree.
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

        var canExecuteName = CanExecuteResolver.SuppliedName(methodSymbol);
        var (canExecuteMember, isProperty) = CanExecuteResolver.Find(methodSymbol, canExecuteName);

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

        var canExecuteName = CanExecuteResolver.SuppliedName(methodSymbol);
        if (string.IsNullOrEmpty(canExecuteName)) return true;

        return ValidateCanExecute(methodSymbol, canExecuteName, diagnostics);
    }

    private static bool ValidateCanExecute(
        IMethodSymbol methodSymbol, string canExecuteName, List<DiagnosticInfo> diagnostics)
    {
        var resolution = CanExecuteResolver.Resolve(methodSymbol, canExecuteName);

        if (resolution.IsValid) return true;

        if (resolution.Member == null)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature, methodSymbol,
                canExecuteName,
                "Member not found. Expected a method or property with this name."));
            return false;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature,
            resolution.Member,
            resolution.Member.Name,
            DescribeFailure(methodSymbol, resolution)));
        return false;
    }

    private static string DescribeFailure(IMethodSymbol command, CanExecuteResolution resolution)
    {
        if (resolution.IsProperty)
        {
            var property = (IPropertySymbol)resolution.Member!;
            return $"Property type must be bool, found {property.Type}.";
        }

        var method = (IMethodSymbol)resolution.Member!;

        return resolution.Failure switch
        {
            CanExecuteFailure.NotBoolean =>
                $"Return type must be bool, found {method.ReturnType}.",
            CanExecuteFailure.ParameterCountMismatch =>
                $"Parameter count mismatch. Expected {command.Parameters.Length}, found {method.Parameters.Length}.",
            _ =>
                $"Parameter type mismatch at position {resolution.ParameterIndex}. "
                + $"Expected {command.Parameters[resolution.ParameterIndex].Type}, "
                + $"found {method.Parameters[resolution.ParameterIndex].Type}.",
        };
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
