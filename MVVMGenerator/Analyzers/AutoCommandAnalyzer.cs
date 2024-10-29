using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MVVM.Generator.Analyzers;
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AutoCommandAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "AutoCommandAnalyzer";
    private static readonly LocalizableString Title = "AutoCommandAttribute usage";
    private static readonly LocalizableString MessageFormat = "AutoCommand can only be applied to public methods";
    private static readonly LocalizableString Description = "Methods with AutoCommandAttribute must be public.";
    private const string Category = "Usage";

    private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        var attributes = methodDeclaration.AttributeLists.SelectMany(al => al.Attributes);
        foreach (var attribute in attributes)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
            if (symbolInfo.Symbol?.ContainingType.Name == "AutoCommandAttribute")
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                if (methodSymbol == null)
                    continue;
                if (methodSymbol?.DeclaredAccessibility != Accessibility.Public)
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), methodSymbol?.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
