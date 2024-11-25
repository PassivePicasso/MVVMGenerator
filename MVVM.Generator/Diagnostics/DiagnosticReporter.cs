using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Interfaces;

namespace MVVM.Generator.Diagnostics;

public sealed class DiagnosticReporter : IDiagnosticReporter
{
    private readonly SourceProductionContext _context;
    private readonly ConcurrentBag<Diagnostic> _batchedDiagnostics;
    private volatile bool _isFlushing;

    public DiagnosticReporter(SourceProductionContext context)
    {
        _context = context;
        _batchedDiagnostics = new ConcurrentBag<Diagnostic>();

        // Register flush on completion
        context.CancellationToken.Register(FlushBatchedDiagnostics);
    }

    public void Report(DiagnosticDescriptor descriptor, Location? location = null, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);

        // Fast path for immediate reporting
        if (!_isFlushing)
        {
            _context.ReportDiagnostic(diagnostic);
            return;
        }

        // Fallback to batch if currently flushing
        _batchedDiagnostics.Add(diagnostic);
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
            _batchedDiagnostics.Add(diagnostic);
        }

        FlushBatchedDiagnostics();
    }

    private void FlushBatchedDiagnostics()
    {
        if (_isFlushing)
            return;

        _isFlushing = true;

        try
        {
            while (_batchedDiagnostics.TryTake(out var diagnostic))
            {
                _context.ReportDiagnostic(diagnostic);
            }
        }
        finally
        {
            _isFlushing = false;
        }
    }
}