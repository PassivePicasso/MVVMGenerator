using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

[Generator]
public sealed class BaseAttributeGenerator : ISourceGenerator
{
    private static readonly Func<string, string, string> AppendLines = (a, b) => $"{a}\r\n{b}";
    private static readonly Func<string, string, string> AppendInterfaces = (a, b) => $"{a}, {b}";

    private IAttributeGenerator[]? _generators;
    private readonly CodeRenderer _codeRenderer = new CodeRenderer();
    private readonly ErrorReporter _errorReporter = new ErrorReporter();

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
        }
#endif
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var types = assemblies.SelectMany(t =>
        {
            try
            {
                return t.GetTypes();
            }
            catch (ReflectionTypeLoadException rtle)
            {
                return rtle.Types.Where(type => type != null);
            }
        }).ToArray();

        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());

        var generatorTypes = types
            .Where(t => typeof(IAttributeGenerator).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToArray();

        _generators = generatorTypes
            .Select(Activator.CreateInstance)
            .OfType<IAttributeGenerator>()
            .ToArray();
    }

    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            var compilation = context.Compilation;

            var classSymbolsByFullName = CollectClassSymbols(context, compilation);

            foreach (var kvp in classSymbolsByFullName)
            {
                var classSymbols = kvp.Value;
                var generatedCode = GeneratePartialClass(classSymbols, context);
                if (generatedCode == null) continue;

                var sourceText = SourceText.From(generatedCode, Encoding.UTF8);
                var fileName = $"{classSymbols.First().Name}.Generated.cs";
                context.AddSource(fileName, sourceText);
            }
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(context, "BAG001", $"Error executing generator: {ex.Message}");
        }
    }

    private Dictionary<string, List<INamedTypeSymbol>> CollectClassSymbols(
        GeneratorExecutionContext context,
        Compilation compilation)
    {
        var classSymbolsByFullName = new Dictionary<string, List<INamedTypeSymbol>>();

        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            return classSymbolsByFullName;

        foreach (var classDeclaration in receiver.ClassDeclarations)
        {
            var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol namedTypeSymbol)
            {
                var fullName = namedTypeSymbol.ToDisplayString();

                if (!classSymbolsByFullName.TryGetValue(fullName, out var symbolList))
                {
                    symbolList = new List<INamedTypeSymbol>();
                    classSymbolsByFullName[fullName] = symbolList;
                }
                symbolList.Add(namedTypeSymbol);
            }
        }

        return classSymbolsByFullName;
    }

    private string? GeneratePartialClass(IEnumerable<INamedTypeSymbol> classSymbols, GeneratorExecutionContext context)
    {
        var classSymbol = classSymbols.First();
        var generationContext = new ClassGenerationContext();
        try
        {
            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            generationContext.Interfaces.Clear();
            generationContext.Usings.Clear();
            generationContext.NestedClasses.Clear();
            generationContext.InterfaceImplementations.Clear();
            generationContext.Fields.Clear();
            generationContext.Properties.Clear();
            generationContext.StaticFields.Clear();
            generationContext.StaticProperties.Clear();

            if (_generators == null) return null;

            foreach (var generator in _generators)
            {
                generator.Process(generationContext, classSymbol);
            }

            if (!generationContext.IsPopulated())
                return null;

            generationContext.Usings.Sort();

            string derivationSeparator = generationContext.Interfaces.Any() ? " : " : string.Empty;
            string renderedInterfaceList = _codeRenderer.RenderInterfaces(generationContext.Interfaces);
            string renderedUsings = _codeRenderer.Render(generationContext.Usings);
            string renderedNestedClasses = _codeRenderer.Render(generationContext.NestedClasses);
            string renderedIntImpl = _codeRenderer.Render(generationContext.InterfaceImplementations);
            string renderedFields = _codeRenderer.Render(generationContext.Fields);
            string renderedProperties = _codeRenderer.Render(generationContext.Properties);
            string renderedStaticFields = _codeRenderer.Render(generationContext.StaticFields);
            string renderedStaticProperties = _codeRenderer.Render(generationContext.StaticProperties);

            return $$"""
                {{renderedUsings}}

                namespace {{namespaceName}}
                {
                    public partial class {{classSymbol.Name}}{{derivationSeparator}}{{renderedInterfaceList}}
                    {
                {{renderedNestedClasses}}
                {{renderedStaticFields}}
                {{renderedStaticProperties}}
                {{renderedIntImpl}}
                {{renderedFields}}
                {{renderedProperties}}
                    }
                }
                """;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(context, "BAG002", $"Error generating partial class for {classSymbol.Name}: {ex.Message}");
            return null;
        }
    }
}