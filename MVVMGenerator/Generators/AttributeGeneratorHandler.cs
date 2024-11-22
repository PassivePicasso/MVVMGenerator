using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace MVVM.Generator.Generators
{
    internal interface IAttributeGenerator
    {
        void Process(BaseAttributeGenerator generator, INamedTypeSymbol classSymbol);
    }

    internal abstract class AttributeGeneratorHandler<TSymbol, TAttribute> : IAttributeGenerator
        where TSymbol : ISymbol
        where TAttribute : Attribute
    {
        protected static readonly string AttributeName = typeof(TAttribute).Name;
        private Func<ISymbol, bool> SymbolContainsAttribute => p => p.GetAttributes().Any(SymbolAttribute);
        private Func<AttributeData, bool> SymbolAttribute => a => a?.AttributeClass?.Name == AttributeName;

        public void Process(BaseAttributeGenerator generator, INamedTypeSymbol classSymbol)
        {
            if (!classSymbol.GetMembers().Any(SymbolContainsAttribute)) return;

            // Call the BeforeProcessAttribute method
            BeforeProcessAttribute(generator, classSymbol);

            var symbols = classSymbol.GetMembers()
                .Where(p => p.GetAttributes().Any(SymbolAttribute))
                .ToArray();

            if (symbols.Length == 0) return;

            foreach (var tSymbol in symbols.OfType<TSymbol>())
            {
                AddUsings(generator.usings, tSymbol);
                AddInterfaces(generator.interfaces, tSymbol);
                AddNestedClasses(generator.nestedClasses, tSymbol);
                AddInterfaceImplementations(generator.interfaceImplementations, tSymbol);
                AddFields(generator.fields, tSymbol);
                AddProperties(generator.properties, tSymbol);
                AddStaticFields(generator.staticFields, tSymbol);
                AddStaticProperties(generator.staticProperties, tSymbol);
            }
        }

        protected virtual void BeforeProcessAttribute(BaseAttributeGenerator generator, INamedTypeSymbol classSymbol) { }
        protected virtual void AddUsings(List<string> definitions, TSymbol symbol) { }
        protected virtual void AddInterfaces(List<string> definitions, TSymbol symbol) { }
        protected virtual void AddNestedClasses(List<string> definitions, TSymbol symbol) { }
        protected virtual void AddStaticFields(List<string> definitions, TSymbol symbol) { }
        protected virtual void AddStaticProperties(List<string> definitions, TSymbol symbol) { }
        protected virtual void AddInterfaceImplementations(List<string> definitions, TSymbol symbol) { }
        protected virtual void AddFields(List<string> definitions, TSymbol symbol) { }
        protected virtual void AddProperties(List<string> definitions, TSymbol symbol) { }

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
    }
}
