using Microsoft.CodeAnalysis;

using MVVM.Generator.Utilities;

namespace MVVM.Generator.Interfaces;

internal interface IAttributeGenerator
{
    SourceProductionContext Context { get; set; }
    void Process(ClassGenerationContext context, INamedTypeSymbol classSymbol);
}
