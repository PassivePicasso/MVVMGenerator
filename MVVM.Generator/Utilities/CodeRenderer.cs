using System;
using System.Collections.Generic;
using System.Linq;

namespace MVVM.Generator.Utilities;

public class CodeRenderer
{
    private static readonly Func<string, string, string> AppendLines = (a, b) => $"{a}\r\n{b}";
    private static readonly Func<string, string, string> AppendInterfaces = (a, b) => $"{a}, {b}";

    public string Render(IEnumerable<string> strings) => strings.Any()
        ? strings.Distinct().Aggregate(AppendLines)
        : string.Empty;

    public string RenderInterfaces(IEnumerable<string> interfaceList)
    {
        var distinctInterfaces = interfaceList.Distinct().ToArray();
        return distinctInterfaces.Length > 0
            ? distinctInterfaces.Aggregate(AppendInterfaces)
            : string.Empty;
    }
}
