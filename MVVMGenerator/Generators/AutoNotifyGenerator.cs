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
        protected override void AddUsings(List<string> usings, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            usings.Add("using System.ComponentModel;");
            usings.Add("using System.Runtime.CompilerServices;");

            //Add field type's namespace to usings
            if (fieldSymbol?.Type?.ContainingNamespace != null)
                usings.Add($"using {fieldSymbol.Type.ContainingNamespace};");

            foreach (var fieldAttribute in fieldSymbol.GetAttributes())
            {
                if (fieldAttribute.AttributeClass.Name == nameof(AutoNotifyAttribute)) continue;
                //Determine if Attribute can be applied to a Property by acquiring the AttributeUsageAttribute and checking its configuration
                var attributeClassAttributes = fieldAttribute.AttributeClass.GetAttributes();
                var usageAttributeData = attributeClassAttributes.First(aca => aca.AttributeClass.Name == nameof(AttributeUsageAttribute));
                var targets = usageAttributeData.ConstructorArguments.First(ad => ad.Type.Name == nameof(AttributeTargets));
                var result = (AttributeTargets)(int)targets.Value;
                var validOnProperty = result.HasFlag(AttributeTargets.Property);
                //Add using statement for transferred attributes
                if (validOnProperty)
                    if (fieldAttribute.AttributeClass?.ContainingNamespace != null)
                        usings.Add($"using {fieldAttribute.AttributeClass.ContainingNamespace};");
            }
            if (fieldSymbol.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
                foreach (var typeArgSymbol in namedTypeSymbol.TypeArguments)
                    if (typeArgSymbol?.ContainingNamespace != null)
                        usings.Add($"using {typeArgSymbol.ContainingNamespace};");

            var isINCC = fieldSymbol.Type.AllInterfaces.Any(i => i.Name == nameof(INotifyCollectionChanged));
            var collectionChangedHandler = isINCC ? GetCollectionChangedHandlerName(fieldSymbol) : null;
            var hasChangeHandler = !string.IsNullOrEmpty(collectionChangedHandler);
            if (isINCC && hasChangeHandler)
                usings.Add($"using System.Collections.Specialized;");
        }
        protected override void AddProperties(List<string> properties, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            var name = fieldSymbol.Name;
            var type = TypeHelper.GetTypeName(fieldSymbol.Type);

            // Check if the field type implements INotifyCollectionChanged
            // Check if AutoNotifyAttribute has a CollectionChangedHandlerName property
            var isINCC = fieldSymbol.Type.AllInterfaces.Any(i => i.Name == nameof(INotifyCollectionChanged));
            var collectionChangedHandlerName = isINCC ? GetCollectionChangedHandlerName(fieldSymbol) : null;
            var hasChangeHandler = !string.IsNullOrEmpty(collectionChangedHandlerName);
            if (isINCC && hasChangeHandler)
            {
                string handlerFieldName = $"_{name}CollectionChangedHandler";
                string propertyAttributesString = ReconstructAttributes(fieldSymbol);

                // Modify the property setter to manage the event handler registration
                properties.Add($$"""
                            private NotifyCollectionChangedEventHandler {{handlerFieldName}};
                            {{propertyAttributesString}}
                            public {{type}} {{name.Substring(0, 1).ToUpper()}}{{name.Substring(1)}}
                            {
                                get => {{name}};
                                set
                                {
                                    if ({{name}} != null && {{handlerFieldName}} != null)
                                    {
                                        ((INotifyCollectionChanged){{name}}).CollectionChanged -= {{handlerFieldName}};
                                    }

                                    {{name}} = value;
                                    if ({{name}} != null && {{handlerFieldName}} != null)
                                    {
                                        {{handlerFieldName}} = {{collectionChangedHandlerName}};
                                        ((INotifyCollectionChanged){{name}}).CollectionChanged += {{handlerFieldName}};
                                    }
                                    OnPropertyChanged();
                                }
                            }
                    """);
            }
            else
            {
                var propertyAttributesString = ReconstructAttributes(fieldSymbol);
                // Existing logic for properties that do not implement INotifyCollectionChanged
                properties.Add($$"""
                            {{propertyAttributesString}}
                            public {{type}} {{name.Substring(0, 1).ToUpper()}}{{name.Substring(1)}}
                            {
                                get => {{name}};
                                set
                                {
                                    {{name}} = value;
                                    OnPropertyChanged();
                                }
                            }
                    """);
            }

        }
        private static string ReconstructAttributes(IFieldSymbol fieldSymbol)
        {
            List<string> propertyAttributes = new();
            foreach (var fieldAttribute in fieldSymbol.GetAttributes())
            {
                var attributeClass = fieldAttribute.AttributeClass;
                var attributeClassName = attributeClass.Name;
                if (attributeClassName == nameof(AutoNotifyAttribute)) continue;

                var attrClassAttributes = attributeClass.GetAttributes();

                var usageAttributeData = attrClassAttributes.First(aca => aca.AttributeClass.Name == nameof(AttributeUsageAttribute));
                var targets = usageAttributeData.ConstructorArguments.First(ad => ad.Type.Name == nameof(AttributeTargets));
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
            string propertyAttributesString = string.Empty;
            if (propertyAttributes.Any())
                propertyAttributesString = $"[{(propertyAttributes.Aggregate((a, b) => $"{a}, {b}"))}]";
            return propertyAttributesString;
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
        public string GetCollectionChangedHandlerName(IFieldSymbol fieldSymbol)
        {
            var attributeData = fieldSymbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == nameof(AutoNotifyAttribute));

            if (attributeData == null)
            {
                return string.Empty;
            }

            if (attributeData.NamedArguments.Length > 0)
            {
                foreach (var namedArg in attributeData.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case nameof(AutoNotifyAttribute.CollectionChangedHandlerName):
                            {
                                var methodName = namedArg.Value.Value as string;
                                var containingType = fieldSymbol.ContainingType;
                                var matchedMethodSymbol = containingType.GetMembers()
                                    .OfType<IMethodSymbol>()
                                    .FirstOrDefault(m => m.Name == methodName);
                                if (matchedMethodSymbol == null)
                                {
                                    throw new InvalidOperationException($"Method '{methodName}' not found on type '{containingType.Name}'.");
                                }
                                //Ensure matched symbol has the correct method signature for an NotifyCollectionChangedEventHandler
                                // it should return void and take two parameters, object and NotifyCollectionChangedEventArgs
                                if (matchedMethodSymbol.ReturnType.SpecialType != SpecialType.System_Void)
                                {
                                    throw new InvalidOperationException($"Method '{methodName}' does not return void.");
                                }
                                if (matchedMethodSymbol.Parameters.Length != 2)
                                {
                                    throw new InvalidOperationException($"Method '{methodName}' does not have the correct number of parameters.");
                                }
                                if (matchedMethodSymbol.Parameters[0].Type.SpecialType != SpecialType.System_Object)
                                {
                                    throw new InvalidOperationException($"Method '{methodName}' does not have the correct first parameter type.");
                                }
                                if (matchedMethodSymbol.Parameters[1].Type.Name != nameof(NotifyCollectionChangedEventArgs))
                                {
                                    throw new InvalidOperationException($"Method '{methodName}' does not have the correct second parameter type.");
                                }

                                return methodName;
                            }
                    }
                }
            }

            return string.Empty;
        }
    }

}
