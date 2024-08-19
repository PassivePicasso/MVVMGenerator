using Microsoft.CodeAnalysis;
using MVVM.Generator.Generators;
using MVVMGenerator.Attributes;
using System.Collections.Generic;

namespace MVVMGenerator.Generators
{
    [Generator]
    internal class AutoDPropGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoDPropAttribute>
    {
        protected override void AddUsings(List<string> usings, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            usings.Add("using System.Windows;");
            //Add field type's namespace to usings
            usings.Add($"using {fieldSymbol.Type.ContainingNamespace};");
        }

        string GetName(IFieldSymbol fieldSymbol)
        {
            return $$"""
            {{fieldSymbol.Name.Substring(0, 1).ToUpper()}}{{fieldSymbol.Name.Substring(1)}}
            """;
        }


        protected override void AddStaticFields(List<string> fields, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            string name = GetName(fieldSymbol);
            fields.Add($$"""
                            public static readonly DependencyProperty {{name}}Property =
                                DependencyProperty.Register("{{name}}", 
                                                            typeof({{fieldSymbol.Type.Name}}), 
                                                            typeof({{fieldSymbol.ContainingType.Name}}), 
                                                            new PropertyMetadata(default));
                """);
        }

        protected override void AddProperties(List<string> properties, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            string type = GetReturnedType(fieldSymbol);
            string name = GetName(fieldSymbol);
            properties.Add($$"""
                            public {{type}} {{name}}
                            {
                                get { return ({{type}})GetValue({{name}}Property); }
                                set { SetValue({{name}}Property, value); }
                            }
                """);
        }
    }
}
