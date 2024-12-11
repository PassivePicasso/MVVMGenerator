using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

internal class AutoSPropGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoSPropAttribute>
{
    protected override void Execute(ClassGenerationContext context, IFieldSymbol fieldSymbol)
    {
        context.Usings.Add("using Avalonia;");
        NamespaceExtractor.AddNamespaceUsings(context.Usings, fieldSymbol.Type);

        string name = GetName(fieldSymbol);
        string type = GetReturnedType(fieldSymbol);

        context.Fields.Add($$"""
                        public static readonly StyledProperty<{{fieldSymbol.Type.Name}}> {{name}}Property =
                            AvaloniaProperty.Register<{{fieldSymbol.ContainingType.Name}}, {{fieldSymbol.Type.Name}}>(nameof({{name}}));
            """);

        context.Properties.Add($$"""
                        public {{type}} {{name}}
                        {
                            get { return GetValue({{name}}Property); }
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
