using System.Linq;
using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Utilities;

public static class DiagnosticExtensions
{
        public static void ReportPropertyError(
        this SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        ISymbol symbol,
        string message)
    {
        var diagnostic = Diagnostic.Create(
            descriptor,
            symbol.Locations.FirstOrDefault(),
            symbol.Name,
            message);
            
        context.ReportDiagnostic(diagnostic);
    }
    public static void ReportError(
        this SourceProductionContext context,
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
