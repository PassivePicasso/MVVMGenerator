using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;

namespace MVVM.Generator.Utilities;

public class AttributeProcessor
{
    private const string DependsAttrShortName = nameof(DependsOnAttribute);
    private const string DependsAttrFullName = "MVVM.Generator.Attributes." + DependsAttrShortName;

    public Dictionary<string, List<string>> BuildDependsOnLookup(INamedTypeSymbol classSymbol)
    {
        var dependsOnLookup = new Dictionary<string, List<string>>();

        foreach (var member in classSymbol.GetMembers())
        {
            var memberName = member switch
            {
                IFieldSymbol field => GetPropertyNameFromField(field),
                IPropertySymbol prop => prop.Name,
                _ => null
            };

            if (string.IsNullOrEmpty(memberName))
                continue;

            var attributes = member switch
            {
                IFieldSymbol field => field.GetAttributes(),
                IPropertySymbol prop => prop.GetAttributes(),
                _ => ImmutableArray<AttributeData>.Empty
            };

            foreach (var attribute in attributes)
            {
                var attrClassName = attribute.AttributeClass?.ToDisplayString();
                if (attrClassName != DependsAttrFullName && 
                    attribute.AttributeClass?.Name != DependsAttrShortName)
                    continue;

                var propertyNames = attribute.ConstructorArguments.FirstOrDefault().Values
                    .Select(v => v.Value as string)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Cast<string>();

                foreach (var dependsOnPropertyName in propertyNames)
                {
                    if (!dependsOnLookup.ContainsKey(dependsOnPropertyName))
                    {
                        dependsOnLookup[dependsOnPropertyName] = new List<string>();
                    }
                    if (!dependsOnLookup[dependsOnPropertyName].Contains(memberName))
                    {
                        dependsOnLookup[dependsOnPropertyName].Add(memberName);
                    }
                }
            }
        }

        return dependsOnLookup;
    }

    private static string GetPropertyNameFromField(IFieldSymbol fieldSymbol)
    {
        var name = fieldSymbol.Name;
        if (name.StartsWith("_"))
            name = name.TrimStart('_');
        else if (name.StartsWith("s_"))
            name = name.Substring(2);
        return char.ToUpper(name[0]) + name.Substring(1);
    }
}
