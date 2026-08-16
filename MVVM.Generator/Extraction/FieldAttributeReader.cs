using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MVVM.Generator.Attributes;

namespace MVVM.Generator.Extraction;

/// <summary>
/// Reads naming and attribute facts off a field. Behaviour is carried over from
/// the previous PropertyGenerator so generated output is unchanged.
/// </summary>
internal static class FieldAttributeReader
{
    public static string GetPropertyName(IFieldSymbol fieldSymbol)
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

    /// <summary>
    /// Rebuilds attributes that target properties so they can be re-emitted on
    /// the generated property.
    /// </summary>
    public static string ReconstructAttributes(IFieldSymbol fieldSymbol)
    {
        var propertyAttributes = new List<string>();
        foreach (var fieldAttribute in fieldSymbol.GetAttributes())
        {
            var attributeClass = fieldAttribute.AttributeClass;
            if (attributeClass == null) continue;

            var attributeClassName = attributeClass.Name;
            if (attributeClassName == nameof(AutoNotifyAttribute)) continue;

            var usageAttributeData = attributeClass.GetAttributes()
                .FirstOrDefault(aca => aca?.AttributeClass?.Name == nameof(AttributeUsageAttribute));
            var targets = usageAttributeData?.ConstructorArguments
                .FirstOrDefault(ad => ad.Type?.Name == nameof(AttributeTargets))
                .Value;
            if (targets == null) continue;

            var result = (AttributeTargets)(int)targets;
            if (!result.HasFlag(AttributeTargets.Property)) continue;

            var attributeArguments = new List<string>();

            foreach (var arg in fieldAttribute.ConstructorArguments)
                attributeArguments.Add(arg.ToCSharpString());

            foreach (var namedArg in fieldAttribute.NamedArguments)
                attributeArguments.Add($"{namedArg.Key} = {namedArg.Value.ToCSharpString()}");

            var attributeString = $"{attributeClassName.Replace("Attribute", "")}";
            if (attributeArguments.Any())
                attributeString += $"({string.Join(", ", attributeArguments)})";

            propertyAttributes.Add(attributeString);
        }

        if (propertyAttributes.Count > 0)
            return $"""
        [{propertyAttributes.Aggregate((a, b) => $"{a}, {b}")}]
""";
        return string.Empty;
    }

    public static void ValidateEventHandler(string methodName, INamedTypeSymbol containingType, IMethodSymbol? matchedMethodSymbol)
    {
        if (matchedMethodSymbol == null)
            throw new InvalidOperationException($"Method '{methodName}' not found on type '{containingType.Name}'.");

        if (matchedMethodSymbol.ReturnType.SpecialType != SpecialType.System_Void)
            throw new InvalidOperationException($"Method '{methodName}' does not return void.");

        // Allow parameterless handlers OR handlers with (object sender, EventArgs e)
        if (matchedMethodSymbol.Parameters.Length == 0)
            return;

        if (matchedMethodSymbol.Parameters.Length != 2)
            throw new InvalidOperationException($"Method '{methodName}' must be parameterless or take (object sender, EventArgs e) parameters.");

        if (matchedMethodSymbol.Parameters[0].Type.SpecialType != SpecialType.System_Object)
            throw new InvalidOperationException($"Method '{methodName}' does not have the correct first parameter type.");

        if (!IsOrDescendedFrom<EventArgs>(matchedMethodSymbol.Parameters[1]))
            throw new InvalidOperationException($"Parameter '{matchedMethodSymbol.Parameters[1].Name}' of method '{methodName}' is not 'EventArgs' or derived from it.");
    }

    public static void ValidateCollectionChangedHandler(string methodName, INamedTypeSymbol containingType, IMethodSymbol? matchedMethodSymbol)
    {
        ValidateEventHandler(methodName, containingType, matchedMethodSymbol);

        var secondParameter = matchedMethodSymbol!.Parameters[1];
        if (!IsOrDescendedFrom<NotifyCollectionChangedEventArgs>(secondParameter))
        {
            throw new InvalidOperationException($"Parameter '{secondParameter.Name}' of method '{methodName}' is not 'NotifyCollectionChangedEventArgs' or derived from it.");
        }
    }

    private static bool IsOrDescendedFrom<T>(IParameterSymbol parameter)
    {
        var currentType = parameter.Type;
        while (currentType != null)
        {
            if (currentType.Name == typeof(T).Name) return true;
            currentType = currentType.BaseType;
        }
        return false;
    }
}
