using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Interfaces;

namespace MVVM.Generator.Diagnostics;

public static class DiagnosticReporterExtensions
{
    public static void ReportError(this IDiagnosticReporter reporter, DiagnosticDescriptor descriptor, 
        SyntaxNode node, params object[] args)
    {
        reporter.Report(descriptor, node.GetLocation(), args);
    }

    public static void ReportWarning(this IDiagnosticReporter reporter, DiagnosticDescriptor descriptor, 
        Location? location = null, params object[] args)
    {
        if (descriptor.DefaultSeverity != DiagnosticSeverity.Warning)
        {
            var warningDescriptor = new DiagnosticDescriptor(
                descriptor.Id,
                descriptor.Title.ToString(),
                descriptor.MessageFormat.ToString(),
                descriptor.Category,
                DiagnosticSeverity.Warning,
                descriptor.IsEnabledByDefault,
                descriptor.Description.ToString(),
                descriptor.HelpLinkUri,
                descriptor.CustomTags.ToArray());
            
            reporter.Report(warningDescriptor, location, args);
        }
        else
        {
            reporter.Report(descriptor, location, args);
        }
    }
}