using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Interfaces;

public interface IDiagnosticReporter
{
    void Report(DiagnosticDescriptor descriptor, Location? location = null, params object[] messageArgs);
    void Report(DiagnosticDescriptor descriptor, SyntaxNode node, params object[] messageArgs);
    void ReportBatch(IEnumerable<(DiagnosticDescriptor Descriptor, Location? Location, object[] Args)> diagnostics);
}