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
    static readonly Func<string, string, string> appendLines = (a, b) => $"{a}\r\n{b}";
    static readonly Func<string, string, string> appendInterfaces = (a, b) => $"{a}, {b}";

    IAttributeGenerator[]? generators;
    internal List<string> interfaces/*      */= new();
    internal List<string> usings/*          */= new();
    internal List<string> nestedClasses/*   */= new();
    internal List<string> interfaceImplementations/*          */= new();
    internal List<string> fields/*          */= new();
    internal List<string> properties/*      */= new();
    internal List<string> staticFields/*    */= new();
    internal List<string> staticProperties/**/= new();

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        if (!GenDebugger.LaunchRequested && !System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
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
                return rtle.Types;
            }
        }).ToArray();
        var iAttributeGenerators = types
            .Where(t => t != null)
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t != typeof(IAttributeGenerator) && typeof(IAttributeGenerator).IsAssignableFrom(t)).ToArray();
        var untypedGeneratorInstances = iAttributeGenerators.Select(Activator.CreateInstance).ToArray();

        generators = untypedGeneratorInstances.OfType<IAttributeGenerator>().ToArray();
    }

    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            var subName = GetType().Name;
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
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                    if (classSymbol is not INamedTypeSymbol namedTypeSymbol) continue;

                    // Group class symbols by their full name
                    var fullName = namedTypeSymbol.ToDisplayString();
                    if (!classSymbolsByFullName.ContainsKey(fullName))
                    {
                        classSymbolsByFullName[fullName] = new List<INamedTypeSymbol>();
                    }
                    classSymbolsByFullName[fullName].Add(namedTypeSymbol);
                }
            }

            // Generate the partial class for each unique type
            foreach (var kvp in classSymbolsByFullName)
            {
                var classSymbols = kvp.Value;
                var generatedCode = GeneratePartialClass(classSymbols, context);
                if (generatedCode == null) continue;

                var sourceText = SourceText.From(generatedCode, Encoding.UTF8);
                var fileName = $"{classSymbols.First().Name}.{subName}.cs";
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
            if (generators == null) return null;

            foreach (var generator in generators)
            {
                foreach (var symbol in classSymbols)
                {
                    generator.Process(this, symbol);
                }
            }
            if (!interfaces.Any()
              && !usings.Any()
              && !nestedClasses.Any()
              && !interfaceImplementations.Any()
              && !fields.Any()
              && !properties.Any()
              && !staticFields.Any()
              && !staticProperties.Any()
              ) return null;

            string classUsing = $"using {classSymbol.ContainingNamespace};";
            usings.RemoveAll(usng => usng.Contains(classUsing));
            usings.Sort();

            string derivationSeparator/*     */= interfaces.Any() ? " : " : string.Empty;
            string renderedInterfaceList/*   */= RenderInterfaces(interfaces);
            string renderedUsings/*          */= Render(usings);
            string renderedNestedClasses/*   */= Render(nestedClasses);
            string renderedIntImpl/*         */= Render(interfaceImplementations);
            string renderedFields/*          */= Render(fields);
            string renderedProperties/*      */= Render(properties);
            string renderedStaticFields/*    */= Render(staticFields);
            string renderedStaticProperties/**/= Render(staticProperties);

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

    string Render(IEnumerable<string> strings) => strings.Any()
                                                ? strings.Distinct().Aggregate(appendLines)
                                                : string.Empty;
    string RenderInterfaces(IEnumerable<string> interfaces)
    {
        var frozenInterfaces = interfaces.Distinct().ToArray();
        return frozenInterfaces.Length > 0
             ? frozenInterfaces.Aggregate(appendInterfaces)
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