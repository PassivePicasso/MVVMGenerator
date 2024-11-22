using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MVVM.Generator.Attributes;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;


[Generator]
internal class AutoNotifyGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoNotifyAttribute>
{
    private const string AttrUsageName = nameof(AttributeUsageAttribute);
    private const string AttrTargetName = nameof(AttributeTargets);
    private const string AttrTypeName = nameof(AutoNotifyAttribute);
    private const string INCCName = nameof(INotifyCollectionChanged);

    protected override void AddUsings(List<string> usings, IFieldSymbol fieldSymbol)
    {
        usings.Add("using System.ComponentModel;");
        usings.Add("using System.Runtime.CompilerServices;");
        NamespaceExtractor.AddNamespaceUsings(usings, fieldSymbol.Type);

        foreach (var fieldAttribute in fieldSymbol.GetAttributes())
        {
            if (fieldAttribute?.AttributeClass?.Name == AttrTypeName) continue;
            var targets = fieldAttribute?.AttributeClass?.GetAttributes()
                .First(aca => aca?.AttributeClass?.Name == AttrUsageName).ConstructorArguments
                .First(ad => ad.Type?.Name == AttrTargetName)
                .Value;
            if (targets == null) continue;

            var result = (AttributeTargets)(int)targets;
            var validOnProperty = result.HasFlag(AttributeTargets.Property);
            if (validOnProperty && fieldAttribute?.AttributeClass != null)
                NamespaceExtractor.AddNamespaceUsings(usings, fieldAttribute.AttributeClass);
        }

        if (fieldSymbol.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
            foreach (var typeArgSymbol in namedTypeSymbol.TypeArguments)
                NamespaceExtractor.AddNamespaceUsings(usings, typeArgSymbol);

        var attributeData = fieldSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == AttrTypeName);
        var hasChangeHandler = attributeData?.NamedArguments
            .Any(na => na.Key == nameof(AutoNotifyAttribute.CollectionChangedHandlerName)) ?? false;
        var isNotifyingCollection = fieldSymbol.Type.AllInterfaces.Any(i => i.Name == INCCName);
        if (isNotifyingCollection && hasChangeHandler)
            usings.Add("using System.Collections.Specialized;");
    }

    protected override void AddProperties(List<string> properties, IFieldSymbol fieldSymbol)
    {
        var name = fieldSymbol.Name;
        var type = TypeHelper.GetTypeName(fieldSymbol.Type);
        var defines = string.Empty;
        var prefix = string.Empty;
        var suffix = string.Empty;
        var virtualPrefix = string.Empty;
        var getVisibility = string.Empty;
        var setVisibility = string.Empty;
        var staticString = fieldSymbol.IsStatic ? "static " : string.Empty;

        // Trim leading underscores and prefixes
        if (name.StartsWith("_"))
        {
            name = name.TrimStart('_');
        }
        else if (name.StartsWith("s_"))
        {
            name = name.Substring(2);
        }

        var propertyAttributesString = ReconstructAttributes(fieldSymbol);

        var attributeData = fieldSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == AttrTypeName);

        if (attributeData != null && attributeData.NamedArguments.Length > 0)
        {
            foreach (var namedArg in attributeData.NamedArguments)
            {
                var argValue = namedArg.Value.Value;
                if (argValue == null) continue;

                switch (namedArg.Key)
                {
                    case nameof(AutoNotifyAttribute.GetterAccess):
                        getVisibility = $"{((Access)argValue).ToString().ToLower()} ";
                        break;
                    case nameof(AutoNotifyAttribute.SetterAccess):
                        setVisibility = $"{((Access)argValue).ToString().ToLower()} ";
                        break;
                    case nameof(AutoNotifyAttribute.IsVirtual):
                        virtualPrefix = (bool)argValue
                                      ? "virtual "
                                      : string.Empty;
                        break;
                    case nameof(AutoNotifyAttribute.PropertyChangedHandlerName):
                        {
                            var methodName = namedArg.Value.Value as string;
                            if (methodName == null)
                                break;
                            var containingType = fieldSymbol.ContainingType;
                            var matchedMethodSymbol = containingType.GetMembers()
                                .OfType<IMethodSymbol>()
                                .FirstOrDefault(m => m.Name == methodName);

                            ValidateEventHandler(methodName, containingType, matchedMethodSymbol);

                            string handlerFieldName = $"_{name}ChangedHandler";
                            defines = $$"""

        private EventHandler {{handlerFieldName}};
""";

                            suffix = $$"""

                if ({{handlerFieldName}} == null)
                    {{handlerFieldName}} = {{methodName}};
                {{handlerFieldName}}.Invoke(this, EventArgs.Empty);
""";
                        }
                        break;
                    case nameof(AutoNotifyAttribute.CollectionChangedHandlerName):
                        {
                            var methodName = namedArg.Value.Value as string;
                            if (methodName == null)
                                break;
                            var containingType = fieldSymbol.ContainingType;
                            var matchedMethodSymbol = containingType.GetMembers()
                                .OfType<IMethodSymbol>()
                                .FirstOrDefault(m => m.Name == methodName);

                            ValidateCollectionChangedHandler(methodName, containingType, matchedMethodSymbol);

                            string handlerFieldName = $"_{name}CollectionChangedHandler";
                            defines = $"private NotifyCollectionChangedEventHandler {handlerFieldName};";
                            prefix = $$"""

                if ({{name}} != null && {{handlerFieldName}} != null)
                {
                    (({{INCCName}}){{name}}).CollectionChanged -= {{handlerFieldName}};
                }
""";
                            suffix = $$"""

                if ({{name}} != null && {{handlerFieldName}} != null)
                {
                    {{handlerFieldName}} = {{methodName}};
                    (({{INCCName}}){{name}}).CollectionChanged += {{handlerFieldName}};
                }
""";
                        }
                        break;
                }
            }
        }

        // Existing logic for properties that do not implement INotifyCollectionChanged
        properties.Add($$"""
        {{defines}}{{propertyAttributesString}}
        public {{staticString}}{{virtualPrefix}}{{type}} {{name.Substring(0, 1).ToUpper()}}{{name.Substring(1)}}
        {
            {{getVisibility}}get => {{name}};
            {{setVisibility}}set
            {{{prefix}}
                {{name}} = value;{{suffix}}
                OnPropertyChanged();
            }
        }
""");
    }
    protected override void AddInterfaceImplementations(List<string> impls, IFieldSymbol fieldSymbol)
    {
        impls.Add($$"""
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
""");
    }
    protected override void AddInterfaces(List<string> interfaces, IFieldSymbol fieldSymbol)
    {
        interfaces.Add("INotifyPropertyChanged");
    }

    private static string ReconstructAttributes(IFieldSymbol fieldSymbol)
    {
        List<string> propertyAttributes = new();
        foreach (var fieldAttribute in fieldSymbol.GetAttributes())
        {
            var attributeClass = fieldAttribute.AttributeClass;
            if (attributeClass == null) continue;

            var attributeClassName = attributeClass.Name;
            if (attributeClassName == AttrTypeName) continue;

            var attrClassAttributes = attributeClass.GetAttributes();

            var usageAttributeData = attrClassAttributes.First(aca => aca?.AttributeClass?.Name == AttrUsageName);
            var targets = usageAttributeData.ConstructorArguments.First(ad => ad.Type?.Name == AttrTargetName);
            if (targets.Value == null) continue;

            var result = (AttributeTargets)(int)targets.Value;

            if (result.HasFlag(AttributeTargets.Property))
            {
                var attributeArguments = new List<string>();

                // Handle constructor arguments
                foreach (var arg in fieldAttribute.ConstructorArguments)
                    attributeArguments.Add(arg.ToCSharpString());

                // Handle named arguments
                foreach (var namedArg in fieldAttribute.NamedArguments)
                    attributeArguments.Add($"{namedArg.Key} = {namedArg.Value.ToCSharpString()}");

                var attributeString = $"{attributeClassName.Replace("Attribute", "")}";
                if (attributeArguments.Any())
                    attributeString += $"({string.Join(", ", attributeArguments)})";

                propertyAttributes.Add(attributeString);
            }
        }

        if (propertyAttributes.Count > 0)
            return $"""
[{propertyAttributes.Aggregate((a, b) => $"{a}, {b}")}]
""";
        return string.Empty;
    }
    private static void ValidateEventHandler(string methodName, INamedTypeSymbol containingType, IMethodSymbol matchedMethodSymbol)
    {
        if (matchedMethodSymbol == null)
            throw new InvalidOperationException($"Method '{methodName}' not found on type '{containingType.Name}'.");

        if (matchedMethodSymbol.ReturnType.SpecialType != SpecialType.System_Void)
            throw new InvalidOperationException($"Method '{methodName}' does not return void.");

        if (matchedMethodSymbol.Parameters.Length != 2)
            throw new InvalidOperationException($"Method '{methodName}' does not have the correct number of parameters.");

        var firstParameter = matchedMethodSymbol.Parameters[0];
        var secondParameter = matchedMethodSymbol.Parameters[1];

        if (firstParameter.Type.SpecialType != SpecialType.System_Object)
            throw new InvalidOperationException($"Method '{methodName}' does not have the correct first parameter type.");

        if (IsOrDescendedFrom<EventArgs>(secondParameter)) return;

        throw new InvalidOperationException($"Parameter '{secondParameter.Name}' of Method '{methodName}' is not 'EventArgs'.");
    }
    private static void ValidateCollectionChangedHandler(string methodName, INamedTypeSymbol containingType, IMethodSymbol matchedMethodSymbol)
    {
        var parameter = matchedMethodSymbol.Parameters[1];
        ValidateEventHandler(methodName, containingType, matchedMethodSymbol);
        if (!IsOrDescendedFrom<NotifyCollectionChangedEventArgs>(parameter))
            throw new InvalidOperationException(
                $"Parameter '{parameter.Name}' of Method '{methodName}' is not 'NotifyCollectionChangedEventArgs'."
                );
    }
    private static bool IsOrDescendedFrom<T>(IParameterSymbol parameter)
    {
        var eventArgsType = parameter.Type;
        while (eventArgsType != null)
        {
            if (eventArgsType.Name == typeof(T).Name)
            {
                return true;
            }
            eventArgsType = eventArgsType.BaseType;
        }
        return false;
    }
}
