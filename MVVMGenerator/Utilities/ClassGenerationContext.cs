using System.Collections.Generic;
using System.Linq;

namespace MVVM.Generator.Utilities;

public class ClassGenerationContext
{
    public List<string> Interfaces { get; } = new();
    public List<string> Usings { get; } = new();
    public List<string> NestedClasses { get; } = new();
    public List<string> InterfaceImplementations { get; } = new();
    public List<string> Fields { get; } = new();
    public List<string> Properties { get; } = new();
    public List<string> StaticFields { get; } = new();
    public List<string> StaticProperties { get; } = new();

    public bool IsPopulated() => Interfaces.Any() || Usings.Any() || NestedClasses.Any()
            || InterfaceImplementations.Any() || Fields.Any() || Properties.Any()
            || StaticFields.Any() || StaticProperties.Any();
}
