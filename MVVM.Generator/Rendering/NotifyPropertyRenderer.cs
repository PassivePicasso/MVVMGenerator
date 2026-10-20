using System.Collections.Generic;
using System.Linq;
using System.Text;

using MVVM.Generator.Models;

namespace MVVM.Generator.Rendering;

/// <summary>
/// Renders the notifying property for one [AutoNotify] field. The setter
/// returns without notifying when the value is unchanged.
/// </summary>
internal static class NotifyPropertyRenderer
{
    private const string INCCName = "INotifyCollectionChanged";

    public static void AddProperties(
        List<string> properties,
        NotifyFieldModel field,
        IReadOnlyList<ChainModel> chains,
        string observerTypeName)
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

        var chainSuffix = string.Empty;
        foreach (var chain in chains)
        {
            defines += $$"""

        private {{observerTypeName}}? {{chain.ObserverFieldName}};
""";

            // Attach releases the previous subscriptions itself, so reassigning the
            // head does not need a paired detach here.
            chainSuffix += $$"""

                {{chain.ObserverFieldName}} ??= new {{observerTypeName}}(
{{RenderChainCallback(chain)}}
{{RenderChainLinks(chain, observerTypeName)}});
                {{chain.ObserverFieldName}}.Attach({{fieldName}});
""";
        }

        // Combine handler suffixes: collection changed subscription first, then property changed invocation
        var suffix = collectionChangedSuffix + chainSuffix + propertyChangedSuffix;

        var notified = new List<string>(field.DependentProperties);
        foreach (var chain in chains)
        {
            // The head changing invalidates everything reading through it, and the
            // observer only fires for changes below the head.
            foreach (var dependent in chain.DependentProperties)
            {
                if (!notified.Contains(dependent)) notified.Add(dependent);
            }
        }

        var dependsSuffix = notified.Aggregate(
            string.Empty,
            (current, p) => current + $"\n                OnPropertyChanged(nameof({p}));");

        string item = $$"""

{{field.PropertyAttributes}}
        public {{staticString}}{{field.VirtualPrefix}}{{field.TypeName}} {{field.PropertyName}}
        {
            {{field.GetterAccess}}get => {{fieldName}};
            {{field.SetterAccess}}set
            {
                if (EqualityComparer<{{field.TypeName}}>.Default.Equals({{fieldName}}, value)) return;{{prefix}}
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

    /// <summary>
    /// Renders the observer's callback argument, including its trailing comma. A
    /// lone dependent gets an expression-bodied lambda; only several need a block.
    /// </summary>
    private static string RenderChainCallback(ChainModel chain)
    {
        var dependents = chain.DependentProperties.ToList();

        if (dependents.Count == 1)
            return $"                    () => OnPropertyChanged(nameof({dependents[0]})),";

        var builder = new StringBuilder();
        builder.Append("                    () =>\n");
        builder.Append("                    {\n");
        foreach (var dependent in dependents)
        {
            builder.Append($"                        OnPropertyChanged(nameof({dependent}));\n");
        }
        builder.Append("                    },");

        return builder.ToString();
    }

    private static string RenderChainLinks(ChainModel chain, string observerTypeName)
    {
        return string.Join(
            ",\n",
            chain.Links.Select(link =>
                $"                    new {observerTypeName}.Link("
                + $"\"{link.PropertyName}\", "
                + $"o => (({link.OwnerTypeName})o).{link.PropertyName}, "
                + $"{(link.ObserveCollection ? "true" : "false")})"));
    }
}
