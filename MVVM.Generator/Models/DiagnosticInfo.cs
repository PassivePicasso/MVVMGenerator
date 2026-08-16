using System;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MVVM.Generator.Models;

/// <summary>
/// A source position stored as values. Location holds a SyntaxTree reference,
/// which would tie a model to the compilation it came from.
/// </summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public static LocationInfo? From(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.SourceTree != null)
                return From(location);
        }

        return null;
    }

    public static LocationInfo? From(Location location)
    {
        if (location.SourceTree == null) return null;

        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span);
    }

    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
}

/// <summary>
/// A diagnostic captured during extraction and replayed when output is produced.
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(
        DiagnosticDescriptor descriptor,
        ISymbol symbol,
        params string[] messageArgs)
    {
        return new DiagnosticInfo(
            descriptor,
            LocationInfo.From(symbol),
            EquatableArray.From(messageArgs));
    }

    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Length];
        for (var index = 0; index < MessageArgs.Length; index++)
        {
            args[index] = MessageArgs[index];
        }

        return Diagnostic.Create(Descriptor, Location?.ToLocation(), args);
    }
}
