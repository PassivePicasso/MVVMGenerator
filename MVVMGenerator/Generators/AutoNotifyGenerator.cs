using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MVVM.Generator.Attributes;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

internal class AutoNotifyGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoNotifyAttribute>
{
    private const string AttrUsageName = nameof(AttributeUsageAttribute);
    private const string AttrTargetName = nameof(AttributeTargets);
    private const string AttrTypeName = nameof(AutoNotifyAttribute);
    private const string INCCName = nameof(INotifyCollectionChanged);
    private const string DependsAttrTypeName = nameof(DependsOnAttribute);

    private Dictionary<string, List<string>> _dependsOnLookup = new();

    protected override void BeforeProcessAttribute(ClassGenerationContext context, INamedTypeSymbol classSymbol)
    {
        // Build the depends-on lookup before generating code
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
                        if (!_dependsOnLookup.ContainsKey(propertyName))
                        {
                            _dependsOnLookup[propertyName] = new List<string>();
                        }
                        _dependsOnLookup[propertyName].Add(GetPropertyName(fieldSymbol));
                    }
                }
            }
        }
    }

    protected override void AddUsings(List<string> usings, IFieldSymbol fieldSymbol)
    {
        usings.Add("using System.ComponentModel;");
        usings.Add("using System.Runtime.CompilerServices;");
        NamespaceExtractor.AddNamespaceUsings(usings, fieldSymbol.Type);

        foreach (var fieldAttribute in fieldSymbol.GetAttributes())
        {
            if (fieldAttribute?.AttributeClass?.Name == AttrTypeName) continue;
            var targets = fieldAttribute?.AttributeClass?.GetAttributes()
                .FirstOrDefault(aca => aca?.AttributeClass?.Name == AttrUsageName)?.ConstructorArguments
                .FirstOrDefault(ad => ad.Type?.Name == AttrTargetName)
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
        var fieldName = fieldSymbol.Name;
        var propertyName = GetPropertyName(fieldSymbol);
        var type = TypeHelper.GetTypeName(fieldSymbol.Type);
        var defines = string.Empty;
        var prefix = string.Empty;
        var suffix = string.Empty;
        var virtualPrefix = string.Empty;
        var getVisibility = string.Empty;
        var setVisibility = string.Empty;
        var staticString = fieldSymbol.IsStatic ? "static " : string.Empty;


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
                        virtualPrefix = (bool)argValue ? "virtual " : string.Empty;
                        break;
                    case nameof(AutoNotifyAttribute.PropertyChangedHandlerName):
                        {
                            var methodName = argValue as string;
                            if (methodName == null)
                                break;
                            var containingType = fieldSymbol.ContainingType;
                            var matchedMethodSymbol = containingType.GetMembers()
                                .OfType<IMethodSymbol>()
                                .FirstOrDefault(m => m.Name == methodName);

                            ValidateEventHandler(methodName, containingType, matchedMethodSymbol);

                            string handlerFieldName = $"_{fieldName}ChangedHandler";
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
                            var methodName = argValue as string;
                            if (methodName == null)
                                break;
                            var containingType = fieldSymbol.ContainingType;
                            var matchedMethodSymbol = containingType.GetMembers()
                                .OfType<IMethodSymbol>()
                                .FirstOrDefault(m => m.Name == methodName);

                            ValidateCollectionChangedHandler(methodName, containingType, matchedMethodSymbol);

                            string handlerFieldName = $"_{fieldName}CollectionChangedHandler";
                            defines = $"private NotifyCollectionChangedEventHandler {handlerFieldName};";
                            prefix = $$"""

                if ({{fieldName}} != null && {{handlerFieldName}} != null)
                {
                    (({{INCCName}}){{fieldName}}).CollectionChanged -= {{handlerFieldName}};
                }
""";
                            suffix = $$"""
                if ({{fieldName}} != null && {{handlerFieldName}} != null)
                {
                    {{handlerFieldName}} = {{methodName}};
                    (({{INCCName}}){{fieldName}}).CollectionChanged += {{handlerFieldName}};
                }
""";
                        }
                        break;
                }
            }
        }

        // Handle DependsOnAttribute
        var dependsSuffix = string.Empty;
        if (_dependsOnLookup.TryGetValue(propertyName, out var dependsProperties))
        {
            dependsSuffix = string.Join("\r\n", dependsProperties.Select(p => $"OnPropertyChanged(nameof({p}));"));
        }

        // Generate the property code
        properties.Add($$"""
        {{defines}}{{propertyAttributesString}}
        public {{staticString}}{{virtualPrefix}}{{type}} {{propertyName}}
        {
            {{getVisibility}}get => {{fieldName}};
            {{setVisibility}}set
            {
                {{prefix}}
                {{fieldName}} = value;{{suffix}}
                OnPropertyChanged();
                {{dependsSuffix}}
            }
        }
""");
    }
    
    protected override void AddInterfaces(List<string> interfaces, IFieldSymbol fieldSymbol)
    {
        if (!interfaces.Contains("INotifyPropertyChanged"))
            interfaces.Add("INotifyPropertyChanged");
    }

    protected override void AddInterfaceImplementations(List<string> impls, IFieldSymbol fieldSymbol)
    {
        if (!impls.Any(i => i.Contains("PropertyChanged")))
        {
            impls.Add($$"""
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
""");
        }
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

    private static string ReconstructAttributes(IFieldSymbol fieldSymbol)
    {
        var propertyAttributes = new List<string>();
        foreach (var fieldAttribute in fieldSymbol.GetAttributes())
        {
            var attributeClass = fieldAttribute.AttributeClass;
            if (attributeClass == null) continue;

            var attributeClassName = attributeClass.Name;
            if (attributeClassName == AttrTypeName) continue;

            var attrClassAttributes = attributeClass.GetAttributes();

            var usageAttributeData = attrClassAttributes
                .FirstOrDefault(aca => aca?.AttributeClass?.Name == AttrUsageName);
            var targets = usageAttributeData?.ConstructorArguments
                .FirstOrDefault(ad => ad.Type?.Name == AttrTargetName)
                .Value;
            if (targets == null) continue;

            var result = (AttributeTargets)(int)targets;

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

    private static void ValidateEventHandler(string methodName, INamedTypeSymbol containingType, IMethodSymbol? matchedMethodSymbol)
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

        if (!IsOrDescendedFrom<EventArgs>(secondParameter))
            throw new InvalidOperationException($"Parameter '{secondParameter.Name}' of method '{methodName}' is not 'EventArgs' or derived from it.");
    }

    private static void ValidateCollectionChangedHandler(string methodName, INamedTypeSymbol containingType, IMethodSymbol? matchedMethodSymbol)
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
