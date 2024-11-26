using Microsoft.CodeAnalysis;

using MVVM.Generator.Utilities;

namespace MVVM.Generator.Interfaces;

internal interface IAttributeGenerator
{
    SourceProductionContext Context { get; set; }
    bool ValidateSymbol<T>(T symbol) where T : ISymbol;
    void Process(ClassGenerationContext context, INamedTypeSymbol classSymbol);
}
