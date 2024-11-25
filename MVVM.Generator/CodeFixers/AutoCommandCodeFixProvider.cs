using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MVVM.Generator.CodeFixers;
using static MVVM.Generator.Diagnostics.Descriptors.Analzyer;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AutoCommandCodeFixProvider)), Shared]
public class AutoCommandCodeFixProvider : CodeFixProvider
{
    private const string MakePublicTitle = "Make method public";

    public sealed override ImmutableArray<string> FixableDiagnosticIds => [AutoCommand.NotPublic.Id];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault() is MethodDeclarationSyntax declaration)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: MakePublicTitle,
                    createChangedSolution: c => MakePublicAsync(context.Document, declaration, c),
                    equivalenceKey: MakePublicTitle),
                diagnostic);
        }
    }

    private async Task<Solution> MakePublicAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root == null) return document.Project.Solution;

        var newMethodDecl = methodDecl.WithModifiers(
            methodDecl.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));

        var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
        return document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newRoot);
    }
}