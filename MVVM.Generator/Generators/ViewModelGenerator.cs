using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using MVVM.Generator.Attributes;
using MVVM.Generator.Extraction;
using MVVM.Generator.Models;
using MVVM.Generator.Rendering;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

[Generator]
public sealed class ViewModelGenerator : IIncrementalGenerator
{
    public const string Suffix = ".ViewModel.cs";

    private static readonly string[] TriggerAttributes =
    [
        typeof(AutoNotifyAttribute).FullName!,
        typeof(AutoCommandAttribute).FullName!,
        typeof(AutoDPropAttribute).FullName!,
        typeof(AutoSPropAttribute).FullName!,
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Projected to a string first: AnalyzerConfigOptionsProvider itself has
        // no value equality and combining it directly would invalidate every
        // model on each compilation.
        var logPath = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => LogConfiguration.Resolve(provider));

        // Extraction runs once per class, then SelectMany hands each model
        // downstream on its own so an unchanged class skips its output step.
        var models = CollectAttributedClasses(context)
            .Combine(logPath)
            .Select(static (pair, _) =>
            {
                LogManager.Configure(pair.Right);
                return ExtractModels(pair.Left);
            })
            .SelectMany(static (extracted, _) => extracted);

        context.RegisterSourceOutput(models, static (spc, model) => Emit(spc, model));
    }

    private static IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> CollectAttributedClasses(
        IncrementalGeneratorInitializationContext context)
    {
        var perAttribute = TriggerAttributes
            .Select(attributeName => CollectOwningClasses(context, attributeName))
            .ToArray();

        var merged = perAttribute[0];
        for (var i = 1; i < perAttribute.Length; i++)
        {
            merged = merged
                .Combine(perAttribute[i])
                .Select(static (pair, _) => pair.Left.AddRange(pair.Right));
        }

        return merged;
    }

    private static IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> CollectOwningClasses(
        IncrementalGeneratorInitializationContext context,
        string attributeName)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                attributeName,
                // Attributed fields surface as VariableDeclaratorSyntax rather than
                // MemberDeclarationSyntax, so no syntactic narrowing is safe here.
                predicate: static (_, _) => true,
                transform: static (attributeContext, _) => attributeContext.TargetSymbol.ContainingType)
            .Where(static owner => owner is { TypeKind: TypeKind.Class, IsRecord: false })
            .Select(static (owner, _) => owner!)
            .Collect();
    }

    /// <summary>
    /// Deduplicates classes reached through several attributes or partial
    /// declarations, then orders the result so downstream slots stay stable.
    /// </summary>
    private static ImmutableArray<ClassModel> ExtractModels(ImmutableArray<INamedTypeSymbol> classSymbols)
    {
        if (classSymbols.IsDefaultOrEmpty) return ImmutableArray<ClassModel>.Empty;

        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var models = new List<ClassModel>();

        foreach (var classSymbol in classSymbols)
        {
            if (!seen.Add(classSymbol)) continue;

            var model = ClassModelExtractor.Extract(classSymbol);
            if (model != null) models.Add(model);
        }

        return models
            .OrderBy(model => model.Namespace, StringComparer.Ordinal)
            .ThenBy(model => model.ClassName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static void Emit(SourceProductionContext context, ClassModel model)
    {
        foreach (var diagnostic in model.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        var generatedCode = ClassRenderer.Render(model);
        if (generatedCode == null) return;

        context.AddSource(model.HintName, SourceText.From(generatedCode, Encoding.UTF8));
    }
}
