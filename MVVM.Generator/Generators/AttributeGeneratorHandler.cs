using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Interfaces;
using MVVM.Generator.Utilities;

namespace MVVM.Generator.Generators;

internal abstract class AttributeGeneratorHandler<TSymbol, TAttribute> : IAttributeGenerator
    where TSymbol : ISymbol
    where TAttribute : Attribute
{
    protected static readonly string AttributeName = typeof(TAttribute).Name;
    private Func<ISymbol, bool> SymbolContainsAttribute => p => p.GetAttributes().Any(SymbolAttribute);
    private Func<AttributeData, bool> SymbolAttribute => a => a?.AttributeClass?.Name == AttributeName;
    public SourceProductionContext Context { get; set; }
    public string GetAttributeName() => typeof(TAttribute).Name;
    public void Process(ClassGenerationContext context, INamedTypeSymbol classSymbol)
    {
        if (!classSymbol.GetMembers().Any(SymbolContainsAttribute)) return;

        // Call the BeforeProcessAttribute method
        BeforeProcessAttribute(context, classSymbol);

        var symbols = classSymbol.GetMembers()
            .Where(p => p.GetAttributes().Any(SymbolAttribute))
            .ToArray();

        if (symbols.Length == 0) return;

        foreach (var tSymbol in symbols.OfType<TSymbol>())
        {
            Execute(context, tSymbol);
        }
    }
    protected virtual void BeforeProcessAttribute(ClassGenerationContext context, INamedTypeSymbol classSymbol) { }
    protected virtual void Execute(ClassGenerationContext context, TSymbol symbol) { }
    protected string GetReturnedType(TSymbol symbol)
    {
        return symbol switch
        {
            IFieldSymbol fs => fs.Type.ToDisplayString(),
            IPropertySymbol ps => ps.Type.ToDisplayString(),
            IMethodSymbol ms => ms.ReturnType.ToDisplayString(),
            _ => string.Empty,
        };
    }
    public virtual bool ValidateSymbol<TVS>(TVS symbol) where TVS : ISymbol => true;
}
