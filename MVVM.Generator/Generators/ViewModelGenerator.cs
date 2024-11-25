using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using MVVM.Generator.Interfaces;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

[Generator]
public sealed class ViewModelGenerator : IIncrementalGenerator
{
    public const string Suffix = ".ViewModel.cs";
    private readonly CodeRenderer _codeRenderer = new CodeRenderer();
    private readonly ErrorReporter _errorReporter = new ErrorReporter();
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Where(static m => m != null);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        var generatorsProvider = context.AnalyzerConfigOptionsProvider.Select((_, _) => CollectGenerators());

        var combined = compilationAndClasses.Combine(generatorsProvider);

        context.RegisterSourceOutput(combined, (spc, source) => Execute(source.Left.Left, source.Left.Right, source.Right, spc));
    }

    private void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classDeclarations, IAttributeGenerator[] generators, SourceProductionContext context)
    {
        var classSymbolsByFullName = CollectClassSymbols(compilation, classDeclarations);

        foreach (var kvp in classSymbolsByFullName)
        {
            var classSymbols = kvp.Value;
            var generatedCode = GeneratePartialClass(classSymbols, generators, context);
            if (generatedCode == null) continue;

            var sourceText = SourceText.From(generatedCode, Encoding.UTF8);
            var fileName = $"{classSymbols.First().Name}{Suffix}";
            context.AddSource(fileName, sourceText);
        }
    }

    private Dictionary<string, List<INamedTypeSymbol>> CollectClassSymbols(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classDeclarations)
    {
        var classSymbolsByFullName = new Dictionary<string, List<INamedTypeSymbol>>();

        foreach (var classDeclaration in classDeclarations)
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

    private string? GeneratePartialClass(IEnumerable<INamedTypeSymbol> classSymbols, IAttributeGenerator[] generators, SourceProductionContext context)
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

            foreach (var generator in generators)
            {
                generator.Context = context;
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
                {{renderedNestedClasses}}{{renderedStaticFields}}{{renderedStaticProperties}}{{renderedIntImpl}}{{renderedFields}}{{renderedProperties}}
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

    private IAttributeGenerator[] CollectGenerators()
    {
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

        var generatorTypes = types
            .Where(t => typeof(IAttributeGenerator).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToArray();

        IAttributeGenerator[] attributeGenerators = generatorTypes
                    .Select(Activator.CreateInstance)
                    .OfType<IAttributeGenerator>()
                    .ToArray();

        return attributeGenerators;
    }
}
