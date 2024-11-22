using Microsoft.CodeAnalysis;

using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators
{
    internal interface IAttributeGenerator
    {
        void Process(ClassGenerationContext context, INamedTypeSymbol classSymbol);
    }
}
