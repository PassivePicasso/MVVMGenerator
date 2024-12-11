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

        var canExecuteMethod = GetCanExecuteMethod(methodSymbol);
        if (canExecuteMethod != null)
        {
            LogManager.Log($"{LogPrefix}Validating CanExecute method for {methodSymbol.Name}");
            if (!ValidateCanExecuteMethod(methodSymbol, canExecuteMethod))
            {
                return false;
            }
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

        var className = GetCommandClassName(symbol);
        var canExecuteMethodName = GetCanExecuteMethodName(symbol);

        _commandClassGenerator.AddCommandClass(context.NestedClasses, symbol, className, canExecuteMethodName);
        context.Fields.Add($$"""

        private ICommand? {{GetFieldName(symbol)}};
""");

        var fieldName = GetFieldName(symbol);
        context.Properties.Add($$"""

        public ICommand {{symbol.Name}}Command => {{fieldName}} ??= new {{className}}({{(symbol.IsStatic ? string.Empty : "this")}});
""");
    }

    private string GetCommandClassName(IMethodSymbol symbol) => $"{symbol.Name}CommandClass";

    private string GetFieldName(IMethodSymbol symbol)
        => $"{symbol.Name.Substring(0, 1).ToLower()}{symbol.Name.Substring(1)}Command";

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
}