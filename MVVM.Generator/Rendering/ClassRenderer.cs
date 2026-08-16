using System.Collections.Generic;

using MVVM.Generator.Models;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Rendering;

/// <summary>
/// Assembles the generated partial class from a ClassModel.
/// </summary>
/// <remarks>
/// Emission order is fixed here as notify, command, dependency, styled. The
/// previous implementation derived it from member declaration order, so output
/// ordering could shift when members were reordered.
/// </remarks>
internal static class ClassRenderer
{
    private static readonly CodeRenderer Renderer = new();

    public static string? Render(ClassModel model)
    {
        if (!model.HasContent) return null;

        var usings = new List<string>();
        var interfaces = new List<string>();
        var interfaceImplementations = new List<string>();
        var nestedClasses = new List<string>();
        var fields = new List<string>();
        var properties = new List<string>();

        foreach (var field in model.NotifyFields)
        {
            usings.AddRange(field.Usings);
            NotifyPropertyRenderer.AddProperties(properties, field);
        }

        if (!model.NotifyFields.IsEmpty && !model.BaseImplementsInpc)
        {
            interfaces.Add("INotifyPropertyChanged");
            interfaceImplementations.Add(InpcImplementation);
        }

        foreach (var command in model.Commands)
        {
            usings.AddRange(command.Usings);
            if (command.IsOverrideOfCommand) continue;

            CommandClassRenderer.AddCommandClass(nestedClasses, command);

            fields.Add($$"""

        private ICommand? {{command.FieldName}};
""");

            var property = $$"""
        public ICommand {{command.MethodName}}Command => {{command.FieldName}} ??= new {{command.ClassName}}({{(command.IsStatic ? string.Empty : "this")}});
""";

            foreach (var attribute in command.AdditionalAttributes)
            {
                property = $@"{attribute}
{property}";
            }

            properties.Add(property);
        }

        foreach (var dependencyProperty in model.DependencyProperties)
        {
            usings.AddRange(dependencyProperty.Usings);
            BackingPropertyRenderer.AddDependencyProperty(fields, properties, dependencyProperty);
        }

        foreach (var styledProperty in model.StyledProperties)
        {
            usings.AddRange(styledProperty.Usings);
            BackingPropertyRenderer.AddStyledProperty(fields, properties, styledProperty);
        }

        usings.Sort();

        string derivationSeparator = interfaces.Count > 0 ? " : " : string.Empty;

        return $$"""
                {{Renderer.Render(usings)}}

                namespace {{model.Namespace}}
                {
                    public partial class {{model.ClassName}}{{derivationSeparator}}{{Renderer.RenderInterfaces(interfaces)}}
                    {
                {{Renderer.Render(nestedClasses)}}{{Renderer.Render(interfaceImplementations)}}{{Renderer.Render(fields)}}{{Renderer.Render(properties)}}
                    }
                }
                """;
    }

    private const string InpcImplementation = """

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
""";
}
