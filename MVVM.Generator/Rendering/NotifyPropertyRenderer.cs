using System.Collections.Generic;
using System.Linq;

using MVVM.Generator.Models;

namespace MVVM.Generator.Rendering;

/// <summary>
/// Renders the notifying property for one [AutoNotify] field. Literals are
/// carried over unchanged from the previous symbol-based generator.
/// </summary>
internal static class NotifyPropertyRenderer
{
    private const string INCCName = "INotifyCollectionChanged";

    public static void AddProperties(List<string> properties, NotifyFieldModel field)
    {
        var fieldName = field.FieldName;
        var defines = string.Empty;
        var prefix = string.Empty;
        var staticString = field.IsStatic ? "static " : string.Empty;

        var propertyChangedSuffix = string.Empty;
        var collectionChangedSuffix = string.Empty;

        if (field.PropertyChangedHandlerName is { } propertyChangedHandler)
        {
            // Parameterless handlers are called directly; (object, EventArgs)
            // handlers are invoked through a cached delegate field.
            if (field.PropertyChangedHandlerIsParameterless)
            {
                propertyChangedSuffix = $$"""

                {{propertyChangedHandler}}();
""";
            }
            else
            {
                string handlerFieldName = $"_{fieldName}ChangedHandler";
                defines += $$"""

        private EventHandler {{handlerFieldName}};
""";

                propertyChangedSuffix = $$"""

                if ({{handlerFieldName}} == null)
                    {{handlerFieldName}} = {{propertyChangedHandler}};
                {{handlerFieldName}}.Invoke(this, EventArgs.Empty);
""";
            }
        }

        if (field.CollectionChangedHandlerName is { } collectionChangedHandler)
        {
            string handlerFieldName = $"_{fieldName}CollectionChangedHandler";
            defines += $"""

        private NotifyCollectionChangedEventHandler {handlerFieldName};
""";
            prefix = $$"""

                if ({{fieldName}} != null && {{handlerFieldName}} != null)
                {
                    (({{INCCName}}){{fieldName}}).CollectionChanged -= {{handlerFieldName}};
                }

""";
            collectionChangedSuffix = $$"""

                if ({{fieldName}} != null)
                {
                    {{handlerFieldName}} ??= {{collectionChangedHandler}};
                    (({{INCCName}}){{fieldName}}).CollectionChanged += {{handlerFieldName}};
                }
""";
        }

        // Combine handler suffixes: collection changed subscription first, then property changed invocation
        var suffix = collectionChangedSuffix + propertyChangedSuffix;

        var dependsSuffix = field.DependentProperties.Aggregate(
            string.Empty,
            (current, p) => current + $"\n                OnPropertyChanged(nameof({p}));");

        string item = $$"""

{{field.PropertyAttributes}}
        public {{staticString}}{{field.VirtualPrefix}}{{field.TypeName}} {{field.PropertyName}}
        {
            {{field.GetterAccess}}get => {{fieldName}};
            {{field.SetterAccess}}set
            {{{prefix}}
                {{fieldName}} = value;{{suffix}}
                OnPropertyChanged();{{dependsSuffix}}
            }
        }
""";
        if (!string.IsNullOrWhiteSpace(defines))
            item = $"""
{defines}{item}
""";

        properties.Add(item);
    }
}
