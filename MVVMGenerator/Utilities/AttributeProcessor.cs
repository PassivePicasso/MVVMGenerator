using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;

namespace MVVM.Generator.Utilities;

public class AttributeProcessor
{
    private const string DependsAttrTypeName = nameof(DependsOnAttribute);

    public Dictionary<string, List<string>> BuildDependsOnLookup(INamedTypeSymbol classSymbol)
    {
        var dependsOnLookup = new Dictionary<string, List<string>>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IFieldSymbol fieldSymbol) continue;

            foreach (var attribute in fieldSymbol.GetAttributes())
            {
                if (attribute.AttributeClass?.Name == DependsAttrTypeName)
                {
                    var propertyNames = attribute.ConstructorArguments.FirstOrDefault().Values
                        .Select(v => v.Value as string)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Cast<string>()
                        .ToList();

                    foreach (var propertyName in propertyNames)
                    {
                        if (!dependsOnLookup.ContainsKey(propertyName))
                        {
                            dependsOnLookup[propertyName] = new List<string>();
                        }
                        dependsOnLookup[propertyName].Add(GetPropertyName(fieldSymbol));
                    }
                }
            }
        }

        return dependsOnLookup;
    }

    private static string GetPropertyName(IFieldSymbol fieldSymbol)
    {
        var name = fieldSymbol.Name;
        if (name.StartsWith("_"))
        {
            name = name.TrimStart('_');
        }
        else if (name.StartsWith("s_"))
        {
            name = name.Substring(2);
        }
        return char.ToUpper(name[0]) + name.Substring(1);
    }
}
