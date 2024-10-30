using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MVVM.Generator.Generators
{
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
    public class BaseAttributeGenerator : ISourceGenerator
    {
        protected static readonly Func<string, string, string> appendLines = (a, b) => $"{a}\r\n{b}";
        protected static readonly Func<string, string, string> appendInterfaces = (a, b) => $"{a}, {b}";

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
                var customExportClassSymbols = context.Compilation.SyntaxTrees
                    .SelectMany(tree =>
                        tree.GetRoot()
                            .DescendantNodes()
                            .OfType<ClassDeclarationSyntax>()
                            .Select(classDec =>
                            {
                                var semanticModel = compilation.GetSemanticModel(classDec.SyntaxTree);
                                var classSymbol = semanticModel.GetDeclaredSymbol(classDec);
                                return (classDec, classSymbol, semanticModel);
                            })
                        );

                foreach (var (dec, classSymbol, model) in customExportClassSymbols)
                {
                    try
                    {
                        if (classSymbol == null) continue;
                        var generatedCode = GeneratePartialClass(classSymbol, model);
                        if (generatedCode == null) continue;

                        var sourceText = SourceText.From(generatedCode, Encoding.UTF8);
                        var fileName = $"{classSymbol.Name}.{subName}.cs";
                        context.AddSource(fileName, sourceText);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private string? GeneratePartialClass(INamedTypeSymbol classSymbol, SemanticModel model)
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
            if(generators == null) return null;

            foreach (var generator in generators)
            {
                generator.Process(this, classSymbol, model);
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
    }
}
