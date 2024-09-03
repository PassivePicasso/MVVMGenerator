using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MVVM.Generator;
using MVVM.Generator.Generators;

using MVVMGenerator.Attributes;

namespace MVVMGenerator.Generators
{
    [Generator]
    internal class AutoNotifyGenerator : AttributeGeneratorHandler<IFieldSymbol, AutoNotifyAttribute>
    {
        private const string AttrUsageName = nameof(AttributeUsageAttribute);
        private const string AttrTargetName = nameof(AttributeTargets);
        private const string AttrTypeName = nameof(AutoNotifyAttribute);
        private const string INCCName = nameof(INotifyCollectionChanged);
        protected override void AddUsings(List<string> usings, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            usings.Add("using System.ComponentModel;");
            usings.Add("using System.Runtime.CompilerServices;");

            //Add field type's namespace to usings
            if (fieldSymbol?.Type?.ContainingNamespace != null)
                usings.Add($"using {fieldSymbol.Type.ContainingNamespace};");

            foreach (var fieldAttribute in fieldSymbol.GetAttributes())
            {
                if (fieldAttribute.AttributeClass.Name == AttrTypeName) continue;
                //Determine if Attribute can be applied to a Property by acquiring
                //the AttributeUsageAttribute and checking its configuration
                var targets = fieldAttribute.AttributeClass.GetAttributes()
                    .First(aca => aca.AttributeClass.Name == AttrUsageName).ConstructorArguments
                    .First(ad => ad.Type.Name == AttrTargetName);

                var result = (AttributeTargets)(int)targets.Value;
                var validOnProperty = result.HasFlag(AttributeTargets.Property);
                //Add using statement for transferred attributes
                if (validOnProperty)
                    if (fieldAttribute.AttributeClass?.ContainingNamespace != null)
                        usings.Add($"using {fieldAttribute.AttributeClass.ContainingNamespace};");
            }

            //Add usings for generic type arguments
            if (fieldSymbol.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
                foreach (var typeArgSymbol in namedTypeSymbol.TypeArguments)
                    if (typeArgSymbol?.ContainingNamespace != null)
                        usings.Add($"using {typeArgSymbol.ContainingNamespace};");

            //Add using for NotifyCollectionChangedEventHandler if CollectionChangedHandlerName is set
            var attributeData = fieldSymbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == AttrTypeName);
            var hasChangeHandler = attributeData?.NamedArguments
                .Any(na => na.Key == nameof(AutoNotifyAttribute.CollectionChangedHandlerName)) ?? false;
            var isNotifyingCollection = fieldSymbol.Type.AllInterfaces.Any(i => i.Name == INCCName);
            if (isNotifyingCollection && hasChangeHandler)
                usings.Add($"using System.Collections.Specialized;");
        }
        protected override void AddProperties(List<string> properties, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            var name = fieldSymbol.Name;
            var type = TypeHelper.GetTypeName(fieldSymbol.Type);
            string defines = string.Empty;
            string prefix = string.Empty;
            string suffix = string.Empty;
            string propertyAttributesString = ReconstructAttributes(fieldSymbol);

            var attributeData = fieldSymbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == AttrTypeName);

            if ((attributeData?.NamedArguments.Length ?? 0) > 0)
            {
                foreach (var namedArg in attributeData.NamedArguments)
                    switch (namedArg.Key)
                    {
                        case nameof(AutoNotifyAttribute.PropertyChangedHandlerName):
                            {
                                var methodName = namedArg.Value.Value as string;
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

                {{handlerFieldName}} ??= {{methodName}};
                {{handlerFieldName}}.Invoke(this, EventArgs.Empty);
""";
                            }
                            break;
                        case nameof(AutoNotifyAttribute.CollectionChangedHandlerName):
                            {
                                var methodName = namedArg.Value.Value as string;
                                var containingType = fieldSymbol.ContainingType;
                                var matchedMethodSymbol = containingType.GetMembers()
                                    .OfType<IMethodSymbol>()
                                    .FirstOrDefault(m => m.Name == methodName);

                                ValidateCollectionChangedHandler(methodName, containingType, matchedMethodSymbol);

                                string handlerFieldName = $"_{name}CollectionChangedHandler";
                                defines = $$"""
        private NotifyCollectionChangedEventHandler {{handlerFieldName}};
""";
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

            // Existing logic for properties that do not implement INotifyCollectionChanged
            properties.Add($$"""
{{defines}}{{propertyAttributesString}}
        public {{type}} {{name.Substring(0, 1).ToUpper()}}{{name.Substring(1)}}
        {
            get => {{name}};
            set
            {{{prefix}}
                {{name}} = value;{{suffix}}
                OnPropertyChanged();
            }
        }
""");
        }
        protected override void AddInterfaceImplementations(List<string> impls, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            impls.Add($$"""
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
""");
        }
        protected override void AddInterfaces(List<string> interfaces, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            interfaces.Add("INotifyPropertyChanged");
        }

        private static string ReconstructAttributes(IFieldSymbol fieldSymbol)
        {
            List<string> propertyAttributes = new();
            foreach (var fieldAttribute in fieldSymbol.GetAttributes())
            {
                var attributeClass = fieldAttribute.AttributeClass;
                var attributeClassName = attributeClass.Name;
                if (attributeClassName == AttrTypeName) continue;

                var attrClassAttributes = attributeClass.GetAttributes();

                var usageAttributeData = attrClassAttributes.First(aca => aca.AttributeClass.Name == AttrUsageName);
                var targets = usageAttributeData.ConstructorArguments.First(ad => ad.Type.Name == AttrTargetName);
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
        [{(propertyAttributes.Aggregate((a, b) => $"{a}, {b}"))}]
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

            if (IsOrDescendedFrom<EventArgs>(methodName, secondParameter)) return;

            throw new InvalidOperationException($"Parameter '{secondParameter.Name}' of Method '{methodName}' is not 'EventArgs'.");
        }
        private static void ValidateCollectionChangedHandler(string methodName, INamedTypeSymbol containingType, IMethodSymbol matchedMethodSymbol)
        {
            var parameter = matchedMethodSymbol.Parameters[1];
            ValidateEventHandler(methodName, containingType, matchedMethodSymbol);
            if (!IsOrDescendedFrom<NotifyCollectionChangedEventArgs>(methodName, parameter))
                throw new InvalidOperationException(
                    $"Parameter '{parameter.Name}' of Method '{methodName}' is not 'NotifyCollectionChangedEventArgs'."
                    );
        }
        private static bool IsOrDescendedFrom<T>(string methodName, IParameterSymbol parameter)
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
}
