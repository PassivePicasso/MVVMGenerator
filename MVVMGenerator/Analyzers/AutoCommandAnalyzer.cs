using System.Collections.Generic;
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
    private static readonly LocalizableString MessageFormat = "Method '{0}' with AutoCommandAttribute has an issue: {1}";
    private static readonly LocalizableString Description = "Methods with AutoCommandAttribute must adhere to specific rules.";
    private const string Category = "Usage";
    private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.FilePath.EndsWith(".g.cs"))
        {
            return; // Skip generated code
        }

        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var attributes = methodDeclaration.AttributeLists.SelectMany(al => al.Attributes);
        foreach (var attribute in attributes)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
            if (symbolInfo.Symbol?.ContainingType.Name == "AutoCommandAttribute")
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                if (methodSymbol == null) continue;

                // Check if the method is public
                if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    ReportDiagnostic(context, methodDeclaration, methodSymbol, "Method must be public.");
                    continue;
                }

                // Check the number of parameters
                if (methodSymbol.Parameters.Length > 1)
                {
                    ReportDiagnostic(context, methodDeclaration, methodSymbol, "Method must have zero or one parameter.");
                    continue;
                }

                // Validate CanExecuteMethod
                var attributeData = methodSymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "AutoCommandAttribute");
                if (attributeData != null && attributeData.ConstructorArguments.Length > 0)
                {
                    var canExecuteMethodName = attributeData.ConstructorArguments[0].Value as string;
                    if (!string.IsNullOrEmpty(canExecuteMethodName))
                    {
                        if (!IsValidCanExecuteMethod(canExecuteMethodName, methodSymbol, context, methodDeclaration))
                        {
                            ReportDiagnostic(context, methodDeclaration, methodSymbol, $"CanExecuteMethod '{canExecuteMethodName}' is invalid.");
                        }
                    }
                }

                // Check for naming conflicts
                var commandClassName = GetCommandClassName(methodSymbol);
                var containingType = methodSymbol.ContainingType;
                var containedMembers = containingType.GetMembers();
                var containedTypeMembers = containedMembers.OfType<INamedTypeSymbol>().ToArray();
                List<INamedTypeSymbol> nonGeneratedMembers = new();
                foreach(var namedTypeSymbol in containedTypeMembers)
                {
                    var refs = namedTypeSymbol.DeclaringSyntaxReferences;
                    foreach(var refSyntax in refs)
                    {
                        var syntax = refSyntax.GetSyntax();
                        var syntaxTree = syntax.SyntaxTree;
                        var filePath = syntaxTree.FilePath;
                        if (filePath.EndsWith(".g.cs"))
                        {
                            goto outer;
                        }
                    }
                outer: continue;
                }
                var members = nonGeneratedMembers
                    .Select(m => m.Name)
                    .ToImmutableHashSet();

                if (members.Contains(commandClassName))
                {
                    ReportDiagnostic(context, methodDeclaration, methodSymbol, "Generated command class name conflicts with existing member.");
                }
            }
        }
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol, string message)
    {
        var diagnostic = Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), methodSymbol.Name, message);
        context.ReportDiagnostic(diagnostic);
    }

    private static string GetCommandClassName(IMethodSymbol methodSymbol)
    {
        return $"{methodSymbol.Name}CommandClass";
    }

    private static bool IsValidCanExecuteMethod(string methodName, IMethodSymbol methodSymbol, SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration)
    {
        var containingType = methodSymbol.ContainingType;
        var canExecuteMethod = containingType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == methodName);
        if (canExecuteMethod == null || canExecuteMethod.ReturnType.SpecialType != SpecialType.System_Boolean || canExecuteMethod.Parameters.Length != methodSymbol.Parameters.Length)
        {
            return false;
        }
        for (int i = 0; i < canExecuteMethod.Parameters.Length; i++)
        {
            if (!canExecuteMethod.Parameters[i].Type.Equals(methodSymbol.Parameters[i].Type))
            {
                return false;
            }
        }
        return true;
    }

}
