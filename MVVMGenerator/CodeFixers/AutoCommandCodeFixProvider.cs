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

using MVVM.Generator.Analyzers;

using Document = Microsoft.CodeAnalysis.Document;

namespace MVVM.Generator.CodeFixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AutoCommandCodeFixProvider)), Shared]
public class AutoCommandCodeFixProvider : CodeFixProvider
{
    private const string title = "Make method public";

    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
        get { return ImmutableArray.Create(AutoCommandAnalyzer.DiagnosticId); }
    }

    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedSolution: c => MakePublicAsync(context.Document, declaration, c),
                equivalenceKey: title),
            diagnostic);
    }

    private async Task<Solution> MakePublicAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var modifiers = methodDecl.Modifiers;
        var publicModifier = SyntaxFactory.Token(SyntaxKind.PublicKeyword);
        var newModifiers = modifiers.Add(publicModifier);

        var newMethodDecl = methodDecl.WithModifiers(newModifiers);

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);

        return document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newRoot);
    }
}
