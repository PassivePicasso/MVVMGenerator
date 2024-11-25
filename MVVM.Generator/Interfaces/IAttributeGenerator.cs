using Microsoft.CodeAnalysis;

using MVVM.Generator.Utilities;

namespace MVVM.Generator.Interfaces;

internal interface IAttributeGenerator
{
    void Process(ClassGenerationContext context, INamedTypeSymbol classSymbol);
}
