using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Interfaces;

namespace MVVM.Generator.Diagnostics;

public sealed class DiagnosticReporter : IDiagnosticReporter
{
    private readonly SourceProductionContext _context;
    private readonly ConcurrentQueue<Diagnostic> _batchedDiagnostics;

    public DiagnosticReporter(SourceProductionContext context)
    {
        _context = context;
        _batchedDiagnostics = new ConcurrentQueue<Diagnostic>();
    }

    public void Report(DiagnosticDescriptor descriptor, Location? location = null, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
        _context.ReportDiagnostic(diagnostic);
    }
    public void Report(DiagnosticDescriptor descriptor, SyntaxNode node, params object[] messageArgs)
    {
        Report(descriptor, node.GetLocation(), messageArgs);
    }
    public void ReportBatch(IEnumerable<(DiagnosticDescriptor Descriptor, Location? Location, object[] Args)> diagnostics)
    {
        foreach (var (descriptor, location, args) in diagnostics)
        {
            var diagnostic = Diagnostic.Create(descriptor, location, args);
            _batchedDiagnostics.Enqueue(diagnostic);
        }

        FlushBatchedDiagnostics();
    }
    private void FlushBatchedDiagnostics()
    {
        while (_batchedDiagnostics.TryDequeue(out var diagnostic))
        {
            _context.ReportDiagnostic(diagnostic);
        }
    }
}