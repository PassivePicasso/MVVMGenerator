using Microsoft.CodeAnalysis;
using MVVM.Generator.Generators;
using MVVMGenerator.Attributes;
using System.Collections.Generic;

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
            string methodCall = $"""
                            _owner.{symbol.Name}();
            """;
            string canExecute = """
                            return true;
            """;
            if (symbol.Parameters.Length > 0)
            {
                string parameterType = symbol.Parameters[0].Type.Name;
                methodCall = $$"""
                            if(parameter is not {{parameterType}} typedParameter) return;
                            _owner.{{symbol.Name}}(typedParameter);
            """;
                canExecute = $"""
                            return parameter is {parameterType};
            """;
            }

            definitions.Add($$"""
                    public class {{className}} : ICommand
                    {
                        public event EventHandler CanExecuteChanged;
                        
                        readonly {{symbol.ContainingType.Name}} _owner;

                        public {{className}}({{symbol.ContainingType.Name}} owner)
                        {
                            _owner = owner;
                        }

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
                    public ICommand {{symbol.Name}}Command => {{fieldName}} ??= new {{className}}(this);
            """);
        }
    }
}
