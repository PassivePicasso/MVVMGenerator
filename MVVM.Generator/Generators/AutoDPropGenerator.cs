using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

internal class AutoDPropGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoDPropAttribute>
{
    protected override void Execute(ClassGenerationContext context, IFieldSymbol fieldSymbol)
    {
        context.Usings.Add("using System.Windows;");
        NamespaceExtractor.AddNamespaceUsings(context.Usings, fieldSymbol.Type);

        string name = GetName(fieldSymbol);
        string type = GetReturnedType(fieldSymbol);

        context.Fields.Add($$"""
                        public static readonly DependencyProperty {{name}}Property =
                            DependencyProperty.Register("{{name}}", 
                                                        typeof({{fieldSymbol.Type.Name}}), 
                                                        typeof({{fieldSymbol.ContainingType.Name}}), 
                                                        new PropertyMetadata(default));
            """);

        context.Properties.Add($$"""
                        public {{type}} {{name}}
                        {
                            get { return ({{type}})GetValue({{name}}Property); }
                            set { SetValue({{name}}Property, value); }
                        }
            """);
    }
    string GetName(IFieldSymbol fieldSymbol)
    {
        return $$"""
        {{fieldSymbol.Name.Substring(0, 1).ToUpper()}}{{fieldSymbol.Name.Substring(1)}}
        """;
    }


}
