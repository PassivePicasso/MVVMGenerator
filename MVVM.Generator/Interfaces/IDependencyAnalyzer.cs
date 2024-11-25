using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace MVVM.Generator.Interfaces;

public interface IDependencyAnalyzer
{
    ImmutableDictionary<string, ImmutableHashSet<string>> AnalyzeDependencies(INamedTypeSymbol typeSymbol);
}
