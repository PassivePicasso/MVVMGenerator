using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

internal class AutoCommandGenerator : AttributeGeneratorHandler<IMethodSymbol, AutoCommandAttribute>
{
    protected override void AddUsings(List<string> usings, IMethodSymbol symbol)
    {
        usings.Add("using System.Windows.Input;");
        usings.Add("using Newtonsoft.Json;");
        if (symbol.Parameters.Length > 0)
        {
            NamespaceExtractor.AddNamespaceUsings(usings, symbol.Parameters[0].Type);
        }
    }

    string GetCommandClassName(IMethodSymbol symbol) => $$"""{{symbol.Name}}CommandClass""";
    protected override void AddNestedClasses(List<string> definitions, IMethodSymbol symbol)
    {
        if (IsOverrideWithAutoCommand(symbol)) return;
        if (symbol.DeclaredAccessibility != Accessibility.Public) return;
        if (symbol.Parameters.Length > 1) return;

        var className = GetCommandClassName(symbol);
        var canExecuteMethodName = GetCanExecuteMethodName(symbol);

        string methodCall;
        string canExecute;
        string callerSource = symbol.IsStatic ? symbol.ContainingType.Name : "_owner";

        methodCall = $"""
                {callerSource}.{symbol.Name}();
""";
        canExecute = !string.IsNullOrEmpty(canExecuteMethodName)
            ? $"""
                return {callerSource}.{canExecuteMethodName}();
"""
            : """
                return true;
""";

        if (symbol.Parameters.Length == 1)
        {
            string parameterType = symbol.Parameters[0].Type.Name;
            methodCall = $$"""
                if(parameter is not {{parameterType}} typedParameter) return;
                    {{callerSource}}.{{symbol.Name}}(typedParameter);
""";

            canExecute = !string.IsNullOrEmpty(canExecuteMethodName)
                       ? $$"""
                if(parameter is not {{parameterType}} typedParameter) return false;
                    return {{callerSource}}.{{canExecuteMethodName}}(typedParameter);
"""
                       : $"""
                return parameter is {parameterType};
""";
        }

        var ownerField = symbol.IsStatic
            ? """

"""
            : $$"""
            readonly {{symbol.ContainingType.Name}} _owner;

""";

        var ctorBody = symbol.IsStatic ? string.Empty : $"""
                _owner = owner;
""";
        var constructor = $$"""
            public {{className}}({{(symbol.IsStatic ? string.Empty : $"{symbol.ContainingType.Name} owner")}})
            {
{{ctorBody}}
            }
""";

        definitions.Add($$"""
        public class {{className}} : ICommand
        {
            public event EventHandler CanExecuteChanged = delegate { };
{{ownerField}}
{{constructor}}
            public bool CanExecute(object? parameter) 
            {
{{canExecute}}
            }

            public void Execute(object? parameter)
            {
{{methodCall}} 
            }
        }
""");
    }

    public string GetCanExecuteMethodName(IMethodSymbol methodSymbol)
    {
        var attributeData = methodSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == nameof(AutoCommandAttribute));

        if (attributeData == null)
        {
            return string.Empty;
        }

        if (attributeData.ConstructorArguments.Length > 0)
        {
            var canExecuteMethodNode = attributeData.ConstructorArguments[0];
            if (canExecuteMethodNode.Value is string canExecuteMethodName)
            {
                var containingType = methodSymbol.ContainingType;
                var canExecuteMethod = containingType.GetMembers()
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.Name == canExecuteMethodName);

                if (canExecuteMethod == null)
                {
                    throw new InvalidOperationException($"Method '{canExecuteMethodName}' not found on type '{containingType.Name}'.");
                }

                // Verify the number of parameters
                if (canExecuteMethod.Parameters.Length != methodSymbol.Parameters.Length)
                {
                    throw new InvalidOperationException($"Method '{canExecuteMethodName}' has a different number of parameters than '{methodSymbol.Name}'.");
                }

                // Ensure the return type is bool
                if (canExecuteMethod.ReturnType.SpecialType != SpecialType.System_Boolean)
                {
                    throw new InvalidOperationException($"Method '{canExecuteMethodName}' does not return a boolean.");
                }

                return canExecuteMethodName;
            }
        }

        return string.Empty;
    }
    private bool IsOverrideWithAutoCommand(IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.IsOverride)
        {
            return false;
        }

        var overriddenMethod = methodSymbol.OverriddenMethod;
        if (overriddenMethod == null)
        {
            return false;
        }

        return overriddenMethod.GetAttributes().Any(attr => attr.AttributeClass?.Name == nameof(AutoCommandAttribute));
    }
    string GetFieldName(IMethodSymbol symbol) => $$"""{{symbol.Name.Substring(0, 1).ToLower()}}{{symbol.Name.Substring(1)}}Command""";

    protected override void AddFields(List<string> definitions, IMethodSymbol symbol)
        => definitions.Add($$"""
        [JsonIgnore]
        private ICommand? {{GetFieldName(symbol)}};
""");

    protected override void AddProperties(List<string> definitions, IMethodSymbol symbol)
    {
        var className = GetCommandClassName(symbol);
        var fieldName = GetFieldName(symbol);
        definitions.Add($$"""
                    [JsonIgnore]
                    public ICommand {{symbol.Name}}Command => {{fieldName}} ??= new {{className}}({{(symbol.IsStatic ? string.Empty : "this")}});
            """);
    }
}
