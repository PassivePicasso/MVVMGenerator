    using Microsoft.CodeAnalysis;

    namespace MVVM.Generator.Utilities;

    public class ErrorReporter
    {
        public void ReportError(
            GeneratorExecutionContext context,
            string id,
            string message,
            Location? location = null)
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
    