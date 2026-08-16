using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Models;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Extraction;

internal static class BackingPropertyExtractor
{
    public static BackingPropertyModel Extract(IFieldSymbol fieldSymbol, string frameworkUsing)
    {
        var usings = new List<string> { frameworkUsing };
        NamespaceExtractor.AddNamespaceUsings(usings, fieldSymbol.Type);

        var name = fieldSymbol.Name;

        return new BackingPropertyModel(
            PropertyName: $"{name.Substring(0, 1).ToUpper()}{name.Substring(1)}",
            TypeDisplayName: fieldSymbol.Type.ToDisplayString(),
            TypeShortName: fieldSymbol.Type.Name,
            OwnerTypeName: fieldSymbol.ContainingType.Name,
            Usings: EquatableArray.From(usings));
    }
}
