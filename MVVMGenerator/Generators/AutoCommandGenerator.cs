using Microsoft.CodeAnalysis;

using MVVM.Generator.Generators;

using MVVMGenerator.Attributes;

using System.Collections.Generic;
using System.Linq;

namespace MVVMGenerator.Generators
{
    [Generator]
    internal class AutoCommandGenerator : AttributeGeneratorHandler<IMethodSymbol, AutoCommandAttribute>
    {
        protected override void AddUsings(List<string> usings, IMethodSymbol symbol, SemanticModel model)
        {
            usings.Add("using System;");
            usings.Add("using System.Windows.Input;");
            usings.Add("using System.Text.Json.Serialization;");
            if (symbol.Parameters.Length > 0)
            {
                usings.Add($"using {symbol.Parameters[0].Type.ContainingNamespace};");
            }
        }

        string GetCommandClassName(IMethodSymbol symbol) => $$"""{{symbol.Name}}CommandClass""";
        protected override void AddNestedClasses(List<string> definitions, IMethodSymbol symbol, SemanticModel model)
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public) return;
            if (symbol.Parameters.Length > 1) return;

            var className = GetCommandClassName(symbol);
            var canExecuteMethodName = GetCanExecuteMethodName(symbol);

            string methodCall;
            string canExecute;

            if (symbol.IsStatic)
            {
                methodCall = $"\t\t\t\t{symbol.ContainingType.Name}.{symbol.Name}();";
                canExecute = !string.IsNullOrEmpty(canExecuteMethodName)
                    ? $"\t\t\t\treturn {symbol.ContainingType.Name}.{canExecuteMethodName}();"
                    : "\t\t\t\treturn true;";
            }
            else
            {
                methodCall = $"\t\t\t\t_owner.{symbol.Name}();";
                canExecute = !string.IsNullOrEmpty(canExecuteMethodName)
                    ? $"\t\t\t\treturn _owner.{canExecuteMethodName}();"
                    : "\t\t\t\treturn true;";
            }

            if (symbol.Parameters.Length > 0)
            {
                string parameterType = symbol.Parameters[0].Type.Name;
                methodCall = symbol.IsStatic
                    ? $$"""
                                if(parameter is not {{parameterType}} typedParameter) return;
                                {{symbol.ContainingType.Name}}.{{symbol.Name}}(typedParameter);
                """
                    : $$"""
                                if(parameter is not {{parameterType}} typedParameter) return;
                                _owner.{{symbol.Name}}(typedParameter);
                """;

                canExecute = !string.IsNullOrEmpty(canExecuteMethodName)
                    ? symbol.IsStatic
                        ? $"\t\t\t\treturn {symbol.ContainingType.Name}.{canExecuteMethodName}(parameter);"
                        : $"\t\t\t\treturn _owner.{canExecuteMethodName}(parameter);"
                    : $"\t\t\t\treturn parameter is {parameterType};";
            }

            var ownerField = symbol.IsStatic
                ? """

    """
                : $$"""
                readonly {{symbol.ContainingType.Name}} _owner;

    """;

            var constructor = $$"""
                public {{className}}({{(symbol.IsStatic ? string.Empty : $"{symbol.ContainingType.Name} owner")}})
                {
                {{(symbol.IsStatic ? string.Empty : """
                    _owner = owner;
            """ )}}
                }
    """;

            definitions.Add($$"""
            public class {{className}} : ICommand
            {
                public event EventHandler CanExecuteChanged;
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
            // Find the AutoCommandAttribute on the method
            var attributeData = methodSymbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == nameof(AutoCommandAttribute));

            if (attributeData == null)
            {
                return null; // Attribute not found
            }

            // Find the named argument for CanExecuteMethod
            var canExecuteMethodArg = attributeData.NamedArguments
                .FirstOrDefault(kvp => kvp.Key == nameof(AutoCommandAttribute.CanExecuteMethod));

            // Return the value as a string
            return canExecuteMethodArg.Value.Value as string;
        }

        string GetFieldName(IMethodSymbol symbol) => $$"""{{symbol.Name.Substring(0, 1).ToLower()}}{{symbol.Name.Substring(1)}}Command""";

        protected override void AddFields(List<string> definitions, IMethodSymbol symbol, SemanticModel model)
            => definitions.Add($$"""
                    [JsonIgnore]
                    private ICommand? {{GetFieldName(symbol)}};
            """);

        protected override void AddProperties(List<string> definitions, IMethodSymbol symbol, SemanticModel model)
        {
            var className = GetCommandClassName(symbol);
            var fieldName = GetFieldName(symbol);
            definitions.Add($$"""
                    [JsonIgnore]
                    public ICommand {{symbol.Name}}Command => {{fieldName}} ??= new {{className}}({{(symbol.IsStatic ? string.Empty : "this")}});
            """);
        }
    }
}
