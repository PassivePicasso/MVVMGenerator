using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace MVVM.Generator.Diagnostics;

internal static class LocationHelper
{
    public static Location? GetBestLocation(SyntaxNode node)
    {
        // First try to get the identifier location
        if (node is INamedTypeSymbol namedType)
        {
            return namedType.Locations.FirstOrDefault();
        }

        // Fall back to the node's location
        return node.GetLocation();
    }

    public static Location CreateLocation(string path, TextSpan span)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("", path: path);
        return Location.Create(syntaxTree, span);
    }
}