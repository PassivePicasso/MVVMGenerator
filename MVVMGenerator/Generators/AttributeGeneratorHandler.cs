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
    internal abstract class AttributeGeneratorHandler<Symbol, Attribute> : IAttributeGenerator
        where Symbol : ISymbol
        where Attribute : System.Attribute
    {

        protected static readonly string AttributeName = typeof(Attribute).Name;
        Func<ISymbol, bool> SymbolContainsAttribute => p => p.GetAttributes().Any(SymbolAttribute);
        Func<AttributeData, bool> SymbolAttribute
        {
            get
            {
                return a =>
                {
                    bool v = a?.AttributeClass?.Name == AttributeName;
                    return v;
                };
            }
        }

        public void Process(BaseAttributeGenerator generator, INamedTypeSymbol classSymbol)
        {
            if (!classSymbol.GetMembers().Any(SymbolContainsAttribute)) return;

            var symbols = classSymbol.GetMembers()
                .Where(p => p.GetAttributes().Any(SymbolAttribute))
                .ToArray();
            if (symbols.Length == 0) return;

            foreach (var tSymbol in symbols.OfType<Symbol>())
            {
                AddUsings/*                  */(generator.usings,/*                  */tSymbol);
                AddInterfaces/*              */(generator.interfaces,/*              */tSymbol);
                AddNestedClasses/*           */(generator.nestedClasses,/*           */tSymbol);
                AddInterfaceImplementations/**/(generator.interfaceImplementations,/**/tSymbol);
                AddFields/*                  */(generator.fields,/*                  */tSymbol);
                AddProperties/*              */(generator.properties,/*              */tSymbol);
                AddStaticFields/*            */(generator.staticFields,/*            */tSymbol);
                AddStaticProperties/*        */(generator.staticProperties,/*        */tSymbol);
            }
        }


        protected virtual void AddUsings(List<string> definitions, Symbol symbol) { }
        protected virtual void AddInterfaces(List<string> definitions, Symbol symbol) { }
        protected virtual void AddNestedClasses(List<string> definitions, Symbol symbol) { }
        protected virtual void AddStaticFields(List<string> definitions, Symbol symbol) { }
        protected virtual void AddStaticProperties(List<string> definitions, Symbol symbol) { }
        protected virtual void AddInterfaceImplementations(List<string> definitions, Symbol symbol) { }
        protected virtual void AddFields(List<string> definitions, Symbol symbol) { }
        protected virtual void AddProperties(List<string> definitions, Symbol symbol) { }


        protected string GetReturnedType(Symbol symbol)
        {
            string type = string.Empty;
            switch (symbol)
            {
                case IFieldSymbol fs:
                    type = fs.Type.ToDisplayString();
                    break;
                case IPropertySymbol ps:
                    type = ps.Type.ToDisplayString();
                    break;
                case IMethodSymbol ms:
                    type = ms.ReturnType.ToDisplayString();
                    break;
            }
            return type;
        }


    }
}
