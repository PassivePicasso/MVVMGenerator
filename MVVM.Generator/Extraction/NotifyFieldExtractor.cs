using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;
using MVVM.Generator.Models;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Extraction;

/// <summary>
/// Turns an [AutoNotify] field into a value model, reusing the symbol-based
/// helpers that already define what gets generated.
/// </summary>
internal static class NotifyFieldExtractor
{
    private const string AttrTypeName = nameof(AutoNotifyAttribute);
    private const string InccName = "INotifyCollectionChanged";

    public static NotifyFieldModel Extract(
        IFieldSymbol fieldSymbol,
        ImmutableDictionary<string, ImmutableHashSet<string>> dependencies)
    {
        var usings = new List<string>
        {
            "using System.ComponentModel;",
            "using System.Runtime.CompilerServices;",
        };
        NamespaceExtractor.AddNamespaceUsings(usings, fieldSymbol.Type);
        AddPropertyTargetedAttributeUsings(usings, fieldSymbol);
        AddTypeArgumentUsings(usings, fieldSymbol);

        var attributeData = fieldSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == AttrTypeName);

        var getterAccess = string.Empty;
        var setterAccess = string.Empty;
        var virtualPrefix = string.Empty;
        string? propertyChangedHandler = null;
        string? collectionChangedHandler = null;

        foreach (var namedArg in attributeData?.NamedArguments ?? ImmutableArray<KeyValuePair<string, TypedConstant>>.Empty)
        {
            var argValue = namedArg.Value.Value;
            if (argValue == null) continue;

            switch (namedArg.Key)
            {
                case nameof(AutoNotifyAttribute.GetterAccess):
                    getterAccess = $"{((Access)argValue).ToString().ToLower()} ";
                    break;
                case nameof(AutoNotifyAttribute.SetterAccess):
                    setterAccess = $"{((Access)argValue).ToString().ToLower()} ";
                    break;
                case nameof(AutoNotifyAttribute.IsVirtual):
                    virtualPrefix = (bool)argValue ? "virtual " : string.Empty;
                    break;
                case nameof(AutoNotifyAttribute.PropertyChangedHandlerName):
                    propertyChangedHandler = argValue as string;
                    break;
                case nameof(AutoNotifyAttribute.CollectionChangedHandlerName):
                    collectionChangedHandler = argValue as string;
                    break;
            }
        }

        var isNotifyingCollection = fieldSymbol.Type.AllInterfaces.Any(i => i.Name == InccName);
        if (isNotifyingCollection && collectionChangedHandler != null)
            usings.Add("using System.Collections.Specialized;");

        if (collectionChangedHandler != null)
            ValidateCollectionHandler(fieldSymbol, collectionChangedHandler);

        var propertyName = FieldAttributeReader.GetPropertyName(fieldSymbol);

        return new NotifyFieldModel(
            FieldName: fieldSymbol.Name,
            PropertyName: propertyName,
            TypeName: TypeHelper.GetTypeName(fieldSymbol.Type),
            IsStatic: fieldSymbol.IsStatic,
            PropertyAttributes: FieldAttributeReader.ReconstructAttributes(fieldSymbol),
            GetterAccess: getterAccess,
            SetterAccess: setterAccess,
            VirtualPrefix: virtualPrefix,
            PropertyChangedHandlerName: propertyChangedHandler,
            PropertyChangedHandlerIsParameterless: IsParameterlessHandler(fieldSymbol, propertyChangedHandler),
            CollectionChangedHandlerName: collectionChangedHandler,
            DependentProperties: DependentProperties(dependencies, propertyName),
            Usings: EquatableArray.From(usings));
    }

    /// <summary>
    /// Mirrors the base-type walk that decides whether this class declares
    /// INotifyPropertyChanged or inherits it.
    /// </summary>
    public static bool BaseImplementsInpc(INamedTypeSymbol containingType)
    {
        for (var baseType = containingType.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            var hasInterface = baseType.Interfaces.Any(i => i.Name == "INotifyPropertyChanged");

            var hasAutoNotifyFields = baseType.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == AttrTypeName));

            if (hasInterface || hasAutoNotifyFields) return true;
        }

        return false;
    }

    private static bool IsParameterlessHandler(IFieldSymbol fieldSymbol, string? handlerName)
    {
        if (handlerName == null) return false;

        var matched = fieldSymbol.ContainingType.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == handlerName);

        FieldAttributeReader.ValidateEventHandler(handlerName, fieldSymbol.ContainingType, matched);

        return matched!.Parameters.Length == 0;
    }

    private static void ValidateCollectionHandler(IFieldSymbol fieldSymbol, string handlerName)
    {
        var matched = fieldSymbol.ContainingType.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == handlerName);

        FieldAttributeReader.ValidateCollectionChangedHandler(handlerName, fieldSymbol.ContainingType, matched);
    }

    private static EquatableArray<string> DependentProperties(
        ImmutableDictionary<string, ImmutableHashSet<string>> dependencies,
        string propertyName)
    {
        return dependencies.TryGetValue(propertyName, out var dependents)
            ? EquatableArray.From(dependents)
            : EquatableArray<string>.Empty;
    }

    private static void AddPropertyTargetedAttributeUsings(List<string> usings, IFieldSymbol fieldSymbol)
    {
        foreach (var fieldAttribute in fieldSymbol.GetAttributes())
        {
            if (fieldAttribute?.AttributeClass?.Name == AttrTypeName) continue;

            var targets = fieldAttribute?.AttributeClass?.GetAttributes()
                .FirstOrDefault(aca => aca?.AttributeClass?.Name == "AttributeUsageAttribute")?.ConstructorArguments
                .FirstOrDefault(ad => ad.Type?.Name == "AttributeTargets")
                .Value;
            if (targets == null) continue;

            var result = (System.AttributeTargets)(int)targets;
            if (result.HasFlag(System.AttributeTargets.Property) && fieldAttribute?.AttributeClass != null)
                NamespaceExtractor.AddNamespaceUsings(usings, fieldAttribute.AttributeClass);
        }
    }

    private static void AddTypeArgumentUsings(List<string> usings, IFieldSymbol fieldSymbol)
    {
        if (fieldSymbol.Type is not INamedTypeSymbol { IsGenericType: true } namedTypeSymbol) return;

        foreach (var typeArgSymbol in namedTypeSymbol.TypeArguments)
        {
            NamespaceExtractor.AddNamespaceUsings(usings, typeArgSymbol);
        }
    }
}
