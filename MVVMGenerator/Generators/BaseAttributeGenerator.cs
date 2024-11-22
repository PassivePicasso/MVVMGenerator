using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MVVM.Generator.Generators;

internal static class GenDebugger
{
    private static bool launchedRequest = false;
    public static bool LaunchRequested
    {
        get
        {
            if (launchedRequest) return true;

            launchedRequest = true;
            return false;
        }
    }
}

[Generator]
public sealed class BaseAttributeGenerator : ISourceGenerator
{
    private static readonly Func<string, string, string> AppendLines = (a, b) => $"{a}\r\n{b}";
    private static readonly Func<string, string, string> AppendInterfaces = (a, b) => $"{a}, {b}";

    private IAttributeGenerator[]? _generators;

    internal List<string> interfaces = new();
    internal List<string> usings = new();
    internal List<string> nestedClasses = new();
    internal List<string> interfaceImplementations = new();
    internal List<string> fields = new();
    internal List<string> properties = new();
    internal List<string> staticFields = new();
    internal List<string> staticProperties = new();

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

            // Dictionary to group class symbols by their full name
            var classSymbolsByFullName = new Dictionary<string, List<INamedTypeSymbol>>();

            // Process each syntax tree in the current compilation
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                // Get the semantic model for the syntax tree
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                // Find all class declarations in the syntax tree
                var classDeclarations = syntaxTree.GetRoot().DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();

                foreach (var classDeclaration in classDeclarations)
                {
                    // Get the symbol representing the class
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
            }

            // Generate the partial class for each unique type
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
            ReportError(context, "BAG001", $"Error executing generator: {ex.Message}");
        }
    }

    private string? GeneratePartialClass(IEnumerable<INamedTypeSymbol> classSymbols, GeneratorExecutionContext context)
    {
        var classSymbol = classSymbols.First();
        try
        {
            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            interfaces.Clear();
            usings.Clear();
            nestedClasses.Clear();
            interfaceImplementations.Clear();
            fields.Clear();
            properties.Clear();
            staticFields.Clear();
            staticProperties.Clear();

            if (_generators == null) return null;

            foreach (var generator in _generators)
            {
                generator.Process(this, classSymbol);
            }

            if (!interfaces.Any()
                && !usings.Any()
                && !nestedClasses.Any()
                && !interfaceImplementations.Any()
                && !fields.Any()
                && !properties.Any()
                && !staticFields.Any()
                && !staticProperties.Any())
            {
                return null;
            }

            usings.Sort();

            string derivationSeparator = interfaces.Any() ? " : " : string.Empty;
            string renderedInterfaceList = RenderInterfaces(interfaces);
            string renderedUsings = Render(usings);
            string renderedNestedClasses = Render(nestedClasses);
            string renderedIntImpl = Render(interfaceImplementations);
            string renderedFields = Render(fields);
            string renderedProperties = Render(properties);
            string renderedStaticFields = Render(staticFields);
            string renderedStaticProperties = Render(staticProperties);

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
            ReportError(context, "BAG002", $"Error generating partial class for {classSymbol.Name}: {ex.Message}");
            return null;
        }
    }

    private string Render(IEnumerable<string> strings) => strings.Any()
        ? strings.Distinct().Aggregate(AppendLines)
        : string.Empty;

    private string RenderInterfaces(IEnumerable<string> interfaceList)
    {
        var distinctInterfaces = interfaceList.Distinct().ToArray();
        return distinctInterfaces.Length > 0
            ? distinctInterfaces.Aggregate(AppendInterfaces)
            : string.Empty;
    }

    private void ReportError(GeneratorExecutionContext context, string id, string message, Location? location = null)
    {
        var descriptor = new DiagnosticDescriptor(
            id: id,
            title: "Generator Error",
            messageFormat: message,
            category: "Generator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        var diagnostic = Diagnostic.Create(descriptor, location);
        context.ReportDiagnostic(diagnostic);
    }
}