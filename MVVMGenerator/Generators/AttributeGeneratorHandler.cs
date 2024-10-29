using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;

namespace MVVM.Generator.Generators
{
    internal interface IAttributeGenerator
    {
        void Process(BaseAttributeGenerator generator, INamedTypeSymbol classSymbol, SemanticModel model);
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

        public void Process(BaseAttributeGenerator generator, INamedTypeSymbol classSymbol, SemanticModel model)
        {
            if (!classSymbol.GetMembers().Any(SymbolContainsAttribute)) return;

            var symbols = classSymbol.GetMembers()
                .Where(p => p.GetAttributes().Any(SymbolAttribute))
                .ToArray();
            if (symbols.Length == 0) return;

            foreach (var tSymbol in symbols.OfType<Symbol>())
            {
                AddUsings/*                  */(generator.usings,/*                  */tSymbol, model);
                AddInterfaces/*              */(generator.interfaces,/*              */tSymbol, model);
                AddNestedClasses/*           */(generator.nestedClasses,/*           */tSymbol, model);
                AddInterfaceImplementations/**/(generator.interfaceImplementations,/**/tSymbol, model);
                AddFields/*                  */(generator.fields,/*                  */tSymbol, model);
                AddProperties/*              */(generator.properties,/*              */tSymbol, model);
                AddStaticFields/*            */(generator.staticFields,/*            */tSymbol, model);
                AddStaticProperties/*        */(generator.staticProperties,/*        */tSymbol, model);
            }
        }


        protected virtual void AddUsings(List<string> definitions, Symbol symbol, SemanticModel model) { }
        protected virtual void AddInterfaces(List<string> definitions, Symbol symbol, SemanticModel model) { }
        protected virtual void AddNestedClasses(List<string> definitions, Symbol symbol, SemanticModel model) { }
        protected virtual void AddStaticFields(List<string> definitions, Symbol symbol, SemanticModel model) { }
        protected virtual void AddStaticProperties(List<string> definitions, Symbol symbol, SemanticModel model) { }
        protected virtual void AddInterfaceImplementations(List<string> definitions, Symbol symbol, SemanticModel model) { }
        protected virtual void AddFields(List<string> definitions, Symbol symbol, SemanticModel model) { }
        protected virtual void AddProperties(List<string> definitions, Symbol symbol, SemanticModel model) { }


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
