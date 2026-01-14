using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Diagnostics;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

internal class AutoCommandGenerator : AttributeGeneratorHandler<IMethodSymbol, AutoCommandAttribute>
{
    private const string LogPrefix = "AutoCommandGenerator: ";
    private readonly CommandClassGenerator _commandClassGenerator = new();

    public override bool ValidateSymbol<T>(T symbol)
    {
        LogManager.Log($"{LogPrefix}Validating symbol {symbol?.GetType().Name}");

        var methodSymbol = symbol as IMethodSymbol;
        if (methodSymbol == null)
            return false;
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            LogManager.LogError($"{LogPrefix}Method {methodSymbol.Name} is not public");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.NotPublic,
                methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name));
            return false;
        }

        if (methodSymbol.Parameters.Length > 1)
        {
            LogManager.LogError($"{LogPrefix}Method {methodSymbol.Name} has invalid parameter count: {methodSymbol.Parameters.Length}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.InvalidMethodSignature,
                methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name,
                $"Method has {methodSymbol.Parameters.Length} parameters, maximum allowed is 1."));
            return false;
        }

        if (!IsValidReturnType(methodSymbol.ReturnType))
        {
            LogManager.LogError($"{LogPrefix}Method {methodSymbol.Name} has invalid return type: {methodSymbol.ReturnType}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.InvalidMethodSignature,
                methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name,
                $"Return type must be void or Task, found {methodSymbol.ReturnType}."));
            return false;
        }

        var (canExecuteMember, isProperty) = GetCanExecuteMember(methodSymbol);
        if (canExecuteMember != null)
        {
            LogManager.Log($"{LogPrefix}Validating CanExecute {(isProperty ? "property" : "method")} for {methodSymbol.Name}");
            if (isProperty)
            {
                if (!ValidateCanExecuteProperty(methodSymbol, (IPropertySymbol)canExecuteMember))
                    return false;
            }
            else
            {
                if (!ValidateCanExecuteMethod(methodSymbol, (IMethodSymbol)canExecuteMember))
                    return false;
            }
        }
        else if (!string.IsNullOrEmpty(GetCanExecuteMethodName(methodSymbol)))
        {
            // CanExecute name was specified but not found as either method or property
            LogManager.LogError($"{LogPrefix}CanExecute member not found for {methodSymbol.Name}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature,
                methodSymbol.Locations.FirstOrDefault(),
                GetCanExecuteMethodName(methodSymbol),
                "Member not found. Expected a method or property with this name."));
            return false;
        }

        LogManager.Log($"{LogPrefix}Successfully validated {methodSymbol.Name}");
        return true;
    }

    protected override void Execute(ClassGenerationContext context, IMethodSymbol symbol)
    {
        LogManager.Log($"{LogPrefix}Adding usings for {symbol.Name}");
        context.Usings.Add("using System.Windows.Input;");
        if (symbol.Parameters.Length > 0)
        {
            NamespaceExtractor.AddNamespaceUsings(context.Usings, symbol.Parameters[0].Type);
        }
        if (IsAsyncCommand(symbol))
        {
            context.Usings.Add("using System.Threading.Tasks;");
        }

        LogManager.Log($"{LogPrefix}Generating command class for {symbol.Name}");
        if (IsOverrideWithAutoCommand(symbol)) return;

        var fieldName = $"{symbol.Name.Substring(0, 1).ToLower()}{symbol.Name.Substring(1)}Command";
        var className = $"{symbol.Name}CommandClass";
        var canExecuteName = GetCanExecuteMethodName(symbol);
        var (canExecuteMember, isProperty) = GetCanExecuteMember(symbol);

        // Extract dependencies from CanExecute member for automatic CanExecuteChanged
        IReadOnlyList<string>? dependencies = null;
        if (canExecuteMember != null)
        {
            dependencies = DependencyAnalyzer.GetDependencies(canExecuteMember, null);
            if (dependencies.Count > 0)
            {
                context.Usings.Add("using System.ComponentModel;");
                LogManager.Log($"{LogPrefix}Found {dependencies.Count} dependencies for {symbol.Name}: {string.Join(", ", dependencies)}");
            }
        }

        _commandClassGenerator.AddCommandClass(context.NestedClasses, symbol, className, canExecuteName, isProperty, dependencies);
        context.Fields.Add($$"""

        private ICommand? {{fieldName}};
""");

        var property = $$"""
        public ICommand {{symbol.Name}}Command => {{fieldName}} ??= new {{className}}({{(symbol.IsStatic ? string.Empty : "this")}});
""";

        foreach(var attribute in GetAdditionalAttributes(context, symbol))
        {
            property = $@"{attribute}
{property}";
        }

        context.Properties.Add(property);
    }

    private string GetCanExecuteMethodName(IMethodSymbol methodSymbol)
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

    private IMethodSymbol? GetCanExecuteMethod(IMethodSymbol methodSymbol)
    {
        var canExecuteMethodName = GetCanExecuteMethodName(methodSymbol);
        if (string.IsNullOrEmpty(canExecuteMethodName)) return null;

        return methodSymbol.ContainingType.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == canExecuteMethodName);
    }

    private IPropertySymbol? GetCanExecuteProperty(IMethodSymbol methodSymbol)
    {
        var canExecuteName = GetCanExecuteMethodName(methodSymbol);
        if (string.IsNullOrEmpty(canExecuteName)) return null;

        return methodSymbol.ContainingType.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name == canExecuteName);
    }

    private (ISymbol? symbol, bool isProperty) GetCanExecuteMember(IMethodSymbol methodSymbol)
    {
        // For parameterized commands, CanExecute must be a method
        if (methodSymbol.Parameters.Length > 0)
        {
            return (GetCanExecuteMethod(methodSymbol), false);
        }

        // For parameterless commands, prefer property over method for better MVVM binding
        var property = GetCanExecuteProperty(methodSymbol);
        if (property != null)
            return (property, true);

        var method = GetCanExecuteMethod(methodSymbol);
        return (method, false);
    }

    private bool ValidateCanExecuteMethod(IMethodSymbol commandMethod, IMethodSymbol canExecuteMethod)
    {
        LogManager.Log($"{LogPrefix}Validating CanExecute method {canExecuteMethod.Name}");
        if (canExecuteMethod.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            LogManager.LogError($"{LogPrefix}Invalid return type for CanExecute method {canExecuteMethod.Name}: {canExecuteMethod.ReturnType}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature,
                canExecuteMethod.Locations.FirstOrDefault(),
                canExecuteMethod.Name,
                $"Return type must be bool, found {canExecuteMethod.ReturnType}."));
            return false;
        }

        if (canExecuteMethod.Parameters.Length != commandMethod.Parameters.Length)
        {
            LogManager.LogError($"{LogPrefix}Parameter count mismatch in CanExecute method {canExecuteMethod.Name}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature,
                canExecuteMethod.Locations.FirstOrDefault(),
                canExecuteMethod.Name,
                $"Parameter count mismatch. Expected {commandMethod.Parameters.Length}, found {canExecuteMethod.Parameters.Length}."));
            return false;
        }

        for (int i = 0; i < commandMethod.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                commandMethod.Parameters[i].Type,
                canExecuteMethod.Parameters[i].Type))
            {
                Context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature,
                    canExecuteMethod.Locations.FirstOrDefault(),
                    canExecuteMethod.Name,
                    $"Parameter type mismatch at position {i}. Expected {commandMethod.Parameters[i].Type}, found {canExecuteMethod.Parameters[i].Type}."));
                return false;
            }
        }

        return true;
    }

    private bool ValidateCanExecuteProperty(IMethodSymbol commandMethod, IPropertySymbol canExecuteProperty)
    {
        LogManager.Log($"{LogPrefix}Validating CanExecute property {canExecuteProperty.Name}");

        // Property must return bool
        if (canExecuteProperty.Type.SpecialType != SpecialType.System_Boolean)
        {
            LogManager.LogError($"{LogPrefix}Invalid return type for CanExecute property {canExecuteProperty.Name}: {canExecuteProperty.Type}");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature,
                canExecuteProperty.Locations.FirstOrDefault(),
                canExecuteProperty.Name,
                $"Property type must be bool, found {canExecuteProperty.Type}."));
            return false;
        }

        // Property can only be used with parameterless commands
        if (commandMethod.Parameters.Length > 0)
        {
            LogManager.LogError($"{LogPrefix}Property CanExecute cannot be used with parameterized commands");
            Context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.Generator.AutoCommand.InvalidCanExecuteSignature,
                canExecuteProperty.Locations.FirstOrDefault(),
                canExecuteProperty.Name,
                $"Properties cannot be used as CanExecute for commands with parameters. Use a method instead."));
            return false;
        }

        return true;
    }

    private bool IsOverrideWithAutoCommand(IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.IsOverride) return false;

        var overriddenMethod = methodSymbol.OverriddenMethod;
        return overriddenMethod?.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == nameof(AutoCommandAttribute)) ?? false;
    }

    private bool IsValidReturnType(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_Void ||
               (type.Name == "Task" && type.ContainingNamespace?.ToString() == "System.Threading.Tasks");
    }

    private bool IsAsyncCommand(IMethodSymbol method)
    {
        return method.ReturnType.Name == "Task" &&
               method.ReturnType.ContainingNamespace?.ToString() == "System.Threading.Tasks";
    }

    private IEnumerable<string> GetAdditionalAttributes(ClassGenerationContext context, IMethodSymbol methodSymbol)
    {
        var additionalAttributes = methodSymbol.GetAttributes()
            .Where(ad => ad.AttributeClass?.Name == nameof(AddAttributeAttribute))
            .Select(ad =>
            {
                var attributeType = ad.ConstructorArguments[0].Value as INamedTypeSymbol;
                var args = ad.ConstructorArguments[1].Values.Select(v => v.Value).Where(v => v != null).ToArray();

                if (attributeType != null)
                {
                    var namespaceName = attributeType.ContainingNamespace.ToDisplayString();
                    context.Usings.Add($"using {namespaceName};");
                    string name = attributeType.Name;
                    name = name.Substring(0, name.Length - "Attribute".Length);
                    var decorator = $"{name}";
                    if (args.Length > 0)
                        decorator += $"({string.Join(", ", args.Select(a => a is string ? $"\"{a}\"" : a.ToString()))})";

                    return $"""
        [{decorator}]
""";
                }

                return string.Empty;
            })
            .Where(attr => !string.IsNullOrEmpty(attr))
            .ToArray();

        return additionalAttributes;
    }
}
