using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using MVVM.Generator.Attributes;

namespace MVVM.Generator.Analyzers;

using static MVVM.Generator.Diagnostics.Descriptors.Analzyer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AutoCommandAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [
            AutoCommand.NotPublic,
            AutoCommand.TooManyParameters,
            AutoCommand.InvalidCanExecute,
            AutoCommand.NamingConflict,
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.FilePath.EndsWith(".Generated.cs"))
            return;

        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
        if (methodSymbol == null) return;

        if (!HasAutoCommandAttribute(methodSymbol)) return;

        // Validate in order of importance
        if (!ValidateAccessibility(context, methodDeclaration, methodSymbol))
            return; // Stop on critical errors

        if (!ValidateParameters(context, methodDeclaration, methodSymbol))
            return;

        ValidateCanExecuteMethod(context, methodDeclaration, methodSymbol);
        ValidateNamingConflicts(context, methodDeclaration, methodSymbol);
    }

    private static bool HasAutoCommandAttribute(IMethodSymbol methodSymbol) =>
        methodSymbol.GetAttributes().Any(ad => ad.AttributeClass?.Name == nameof(AutoCommandAttribute));

    private static bool ValidateAccessibility(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AutoCommand.NotPublic,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name));
            return false;
        }
        return true;
    }

    private static bool ValidateParameters(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Parameters.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AutoCommand.TooManyParameters,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name));
            return false;
        }
        return true;
    }

    private static void ValidateCanExecuteMethod(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol)
    {
        var attributeData = methodSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == nameof(AutoCommandAttribute));

        if (attributeData?.ConstructorArguments.Length > 0)
        {
            var canExecuteMethodName = attributeData.ConstructorArguments[0].Value as string;
            if (!string.IsNullOrEmpty(canExecuteMethodName))
            {
                ValidateCanExecuteMethodSignature(context, methodDeclaration, methodSymbol, canExecuteMethodName);
            }
        }
    }

    private static void ValidateCanExecuteMethodSignature(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol, string canExecuteMethodName)
    {
        var containingType = methodSymbol.ContainingType;
        var canExecuteMethod = containingType.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == canExecuteMethodName);

        if (canExecuteMethod == null)
        {
            ReportCanExecuteError(context, methodDeclaration, methodSymbol, canExecuteMethodName, "Method not found");
            return;
        }

        if (canExecuteMethod.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            ReportCanExecuteError(context, methodDeclaration, methodSymbol, canExecuteMethodName, "Must return bool");
            return;
        }

        if (canExecuteMethod.Parameters.Length != methodSymbol.Parameters.Length)
        {
            ReportCanExecuteError(context, methodDeclaration, methodSymbol, canExecuteMethodName, "Parameter count mismatch");
            return;
        }

        for (int i = 0; i < canExecuteMethod.Parameters.Length; i++)
        {
            if (!canExecuteMethod.Parameters[i].Type.Equals(methodSymbol.Parameters[i].Type))
            {
                ReportCanExecuteError(context, methodDeclaration, methodSymbol, canExecuteMethodName, $"Parameter {i + 1} type mismatch");
                return;
            }
        }
    }

    private static void ReportCanExecuteError(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol, string canExecuteMethodName, string error)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            AutoCommand.InvalidCanExecute,
            methodDeclaration.Identifier.GetLocation(),
            canExecuteMethodName,
            methodSymbol.Name,
            error));
    }

    private static void ValidateNamingConflicts(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol)
    {
        var commandClassName = $"{methodSymbol.Name}CommandClass";
        var existingMembers = methodSymbol.ContainingType.GetMembers()
            .Where(m =>
                !IsGeneratedMember(m) && // Not from generated code
                m.Locations.Any(l => !l.SourceTree?.FilePath.EndsWith(".Generated.cs") ?? false) && // Only check source code
                !m.GetAttributes().Any(a => a.AttributeClass?.Name == "AutoCommandAttribute") // Not from AutoCommand
            )
            .Select(m => m.Name)
            .ToImmutableHashSet();

        // Only report conflict if the name exists in actual source code
        if (existingMembers.Contains(commandClassName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AutoCommand.NamingConflict,
                methodDeclaration.Identifier.GetLocation(),
                commandClassName));
        }
    }

    private static bool IsGeneratedMember(ISymbol member)
    {
        return member.DeclaringSyntaxReferences
            .Any(r => r.SyntaxTree.FilePath.EndsWith(".Generated.cs"));
    }
}