using System.Collections.Generic;

using MVVM.Generator.Models;

namespace MVVM.Generator.Rendering;

/// <summary>
/// Renders framework-registered properties. Literals are carried over unchanged
/// from the previous AutoDProp and AutoSProp generators.
/// </summary>
internal static class BackingPropertyRenderer
{
    public static void AddDependencyProperty(List<string> fields, List<string> properties, BackingPropertyModel model)
    {
        fields.Add($$"""
                        public static readonly DependencyProperty {{model.PropertyName}}Property =
                            DependencyProperty.Register("{{model.PropertyName}}", 
                                                        typeof({{model.TypeShortName}}), 
                                                        typeof({{model.OwnerTypeName}}), 
                                                        new PropertyMetadata(default));
            """);

        properties.Add($$"""
                        public {{model.TypeDisplayName}} {{model.PropertyName}}
                        {
                            get { return ({{model.TypeDisplayName}})GetValue({{model.PropertyName}}Property); }
                            set { SetValue({{model.PropertyName}}Property, value); }
                        }
            """);
    }

    public static void AddStyledProperty(List<string> fields, List<string> properties, BackingPropertyModel model)
    {
        fields.Add($$"""
                        public static readonly StyledProperty<{{model.TypeShortName}}> {{model.PropertyName}}Property =
                            AvaloniaProperty.Register<{{model.OwnerTypeName}}, {{model.TypeShortName}}>(nameof({{model.PropertyName}}));
            """);

        properties.Add($$"""
                        public {{model.TypeDisplayName}} {{model.PropertyName}}
                        {
                            get { return GetValue({{model.PropertyName}}Property); }
                            set { SetValue({{model.PropertyName}}Property, value); }
                        }
            """);
    }
}
