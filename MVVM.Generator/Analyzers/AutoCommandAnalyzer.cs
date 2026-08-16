using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using MVVM.Generator.Attributes;
using MVVM.Generator.Extraction;
using MVVM.Generator.Generators;

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
            AutoCommand.UnreferencedCanExecute,
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.FilePath.EndsWith(ViewModelGenerator.Suffix))
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

    /// <summary>
    /// Resolution goes through CanExecuteResolver so this agrees with the
    /// generator, which accepts a property for a parameterless command.
    /// </summary>
    private static void ValidateCanExecuteMethod(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol)
    {
        var canExecuteName = CanExecuteResolver.SuppliedName(methodSymbol);

        if (string.IsNullOrEmpty(canExecuteName))
        {
            ValidateConventionMemberIsReferenced(context, methodDeclaration, methodSymbol);
            return;
        }

        var resolution = CanExecuteResolver.Resolve(methodSymbol, canExecuteName);
        if (resolution.IsValid) return;

        ReportCanExecuteError(context, methodDeclaration, methodSymbol, canExecuteName, resolution.Failure switch
        {
            CanExecuteFailure.MemberNotFound => "Member not found",
            CanExecuteFailure.NotBoolean => "Must return bool",
            CanExecuteFailure.ParameterCountMismatch => "Parameter count mismatch",
            _ => $"Parameter {resolution.ParameterIndex + 1} type mismatch",
        });
    }

    /// <summary>
    /// Warns when a member named Can{Command} exists and would bind, but the
    /// attribute never references it, leaving the command always executable.
    /// </summary>
    private static void ValidateConventionMemberIsReferenced(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol)
    {
        // An override of a command generates nothing, so there is nothing to wire.
        if (IsOverrideOfCommand(methodSymbol)) return;

        var conventionName = CanExecuteResolver.ConventionName(methodSymbol);
        var resolution = CanExecuteResolver.Resolve(methodSymbol, conventionName);

        if (!resolution.IsValid) return;
        if (!CanExecuteResolver.IsStaticCompatible(methodSymbol, resolution.Member!)) return;

        context.ReportDiagnostic(Diagnostic.Create(
            AutoCommand.UnreferencedCanExecute,
            methodDeclaration.Identifier.GetLocation(),
            conventionName,
            methodSymbol.Name));
    }

    private static bool IsOverrideOfCommand(IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.IsOverride) return false;

        return methodSymbol.OverriddenMethod?.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == nameof(AutoCommandAttribute)) ?? false;
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
                m.Locations.Any(l => !l.SourceTree?.FilePath.EndsWith(ViewModelGenerator.Suffix) ?? false) && // Only check source code
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
            .Any(r => r.SyntaxTree.FilePath.EndsWith(ViewModelGenerator.Suffix));
    }
}