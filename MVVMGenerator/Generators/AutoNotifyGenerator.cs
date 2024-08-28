using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MVVM.Generator;
using MVVM.Generator.Generators;

using MVVMGenerator.Attributes;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        }
        protected override void AddProperties(List<string> properties, IFieldSymbol fieldSymbol, SemanticModel model)
        {
            var name = fieldSymbol.Name;
            string type = string.Empty;
            type = TypeHelper.GetTypeName(fieldSymbol.Type);

            string propertyAttributesString = ReconstructAttributes(fieldSymbol);

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

        private static string ReconstructAttributes(IFieldSymbol fieldSymbol)
        {
            List<string> propertyAttributes = new();
            foreach (var fieldAttribute in fieldSymbol.GetAttributes())
            {
                if (fieldAttribute.AttributeClass.Name == nameof(AutoNotifyAttribute)) continue;

                var attributeClassAttributes = fieldAttribute.AttributeClass.GetAttributes();

                var usageAttributeData = attributeClassAttributes.First(aca => aca.AttributeClass.Name == nameof(AttributeUsageAttribute));
                var targets = usageAttributeData.ConstructorArguments.First(ad => ad.Type.Name == nameof(AttributeTargets));
                var result = (AttributeTargets)(int)targets.Value;
                
                if (result.HasFlag(AttributeTargets.Property))
                {
                    var attributeArguments = new List<string>();

                    // Handle constructor arguments
                    foreach (var arg in fieldAttribute.ConstructorArguments)
                    {
                        attributeArguments.Add(arg.ToCSharpString()); // ToCSharpString is a hypothetical method that converts the argument to its C# representation
                    }

                    // Handle named arguments
                    foreach (var namedArg in fieldAttribute.NamedArguments)
                    {
                        attributeArguments.Add($"{namedArg.Key} = {namedArg.Value.ToCSharpString()}");
                    }

                    // Combine attribute name with arguments
                    var attributeString = $"{fieldAttribute.AttributeClass.Name.Replace("Attribute", "")}({string.Join(", ", attributeArguments)})";
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
    }

}
