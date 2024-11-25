using System;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using MVVM.Generator.Attributes;
using MVVM.Generator.Interfaces;

namespace MVVM.Generator.Analyzers;

using static MVVM.Generator.Diagnostics.Descriptors.Analzyer;

/// <summary>
/// Analyzer for AutoNotify attribute usage that validates field declarations and their associated handlers
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AutoNotifyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            AutoNotify.StaticField,
            AutoNotify.NamingConflict,
            AutoNotify.InvalidPropertyChangedHandler,
            AutoNotify.InvalidCollectionChangedHandler
,
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        // Skip generated files
        if (context.Node.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

        // Find AutoNotify attributes
        var autoNotifyAttribute = fieldDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => IsAutoNotifyAttribute(context, attr));

        if (autoNotifyAttribute == null) return;

        var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(fieldDeclaration.Declaration.Variables.First()) as IFieldSymbol;
        if (fieldSymbol == null) return;

        ValidateField(context, fieldDeclaration, fieldSymbol);
        ValidateHandlers(context, fieldDeclaration, fieldSymbol);
    }

    private static bool IsAutoNotifyAttribute(SyntaxNodeAnalysisContext context, AttributeSyntax attribute)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
        return symbolInfo.Symbol?.ContainingType.Name == "AutoNotifyAttribute";
    }

    private static void ValidateField(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration, IFieldSymbol fieldSymbol)
    {
        // Check for static fields
        if (fieldSymbol.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AutoNotify.StaticField,
                fieldDeclaration.GetLocation(),
                fieldSymbol.Name));
            return;
        }

        // Check for naming conflicts
        var propertyName = GetPropertyName(fieldSymbol);
        var containingType = fieldSymbol.ContainingType;
        var existingMembers = GetNonGeneratedMembers(containingType);

        if (existingMembers.Contains(propertyName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AutoNotify.NamingConflict,
                fieldDeclaration.GetLocation(),
                propertyName));
        }
    }

    private static void ValidateHandlers(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration, IFieldSymbol fieldSymbol)
    {
        var attributeData = fieldSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == "AutoNotifyAttribute");

        if (attributeData == null) return;

        foreach (var namedArg in attributeData.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case nameof(AutoNotifyAttribute.PropertyChangedHandlerName):
                    ValidatePropertyChangedHandler(context, fieldDeclaration, fieldSymbol, namedArg.Value.Value as string);
                    break;
                case nameof(AutoNotifyAttribute.CollectionChangedHandlerName):
                    ValidateCollectionChangedHandler(context, fieldDeclaration, fieldSymbol, namedArg.Value.Value as string);
                    break;
            }
        }
    }

    private static void ValidatePropertyChangedHandler(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration, IFieldSymbol fieldSymbol, string? handlerName)
    {
        if (string.IsNullOrEmpty(handlerName)) return;

        if (!IsValidEventHandler(handlerName, fieldSymbol.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AutoNotify.InvalidPropertyChangedHandler,
                fieldDeclaration.GetLocation(),
                handlerName,
                fieldSymbol.Name,
                "Handler must be a void method taking object sender and EventArgs e parameters"));
        }
    }

    private static void ValidateCollectionChangedHandler(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration, IFieldSymbol fieldSymbol, string? handlerName)
    {
        if (string.IsNullOrEmpty(handlerName)) return;

        if (!IsValidCollectionChangedHandler(handlerName, fieldSymbol.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AutoNotify.InvalidCollectionChangedHandler,
                fieldDeclaration.GetLocation(),
                handlerName,
                fieldSymbol.Name,
                "Handler must be a void method taking object sender and NotifyCollectionChangedEventArgs e parameters"));
        }
    }

    private static string GetPropertyName(IFieldSymbol fieldSymbol)
    {
        var name = fieldSymbol.Name;
        if (name.StartsWith("_"))
            name = name.TrimStart('_');
        else if (name.StartsWith("s_"))
            name = name.Substring(2);
        return char.ToUpper(name[0]) + name.Substring(1);
    }

    private static ImmutableHashSet<string> GetNonGeneratedMembers(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .Where(m => !IsGeneratedMember(m))
            .Select(m => m.Name)
            .ToImmutableHashSet();
    }

    private static bool IsGeneratedMember(ISymbol member)
    {
        return member.DeclaringSyntaxReferences
            .Any(r => r.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidEventHandler(string methodName, INamedTypeSymbol containingType)
    {
        var methodSymbol = containingType.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == methodName);

        return IsValidHandlerSignature<EventArgs>(methodSymbol);
    }

    private static bool IsValidCollectionChangedHandler(string methodName, INamedTypeSymbol containingType)
    {
        var methodSymbol = containingType.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == methodName);

        return IsValidHandlerSignature<NotifyCollectionChangedEventArgs>(methodSymbol);
    }

    private static bool IsValidHandlerSignature<TEventArgs>(IMethodSymbol? methodSymbol) where TEventArgs : EventArgs
    {
        if (methodSymbol == null ||
            methodSymbol.ReturnType.SpecialType != SpecialType.System_Void ||
            methodSymbol.Parameters.Length != 2)
            return false;

        var firstParam = methodSymbol.Parameters[0];
        var secondParam = methodSymbol.Parameters[1];

        return firstParam.Type.SpecialType == SpecialType.System_Object &&
               IsOrDescendedFrom<TEventArgs>(secondParam);
    }

    private static bool IsOrDescendedFrom<T>(IParameterSymbol parameter)
    {
        var currentType = parameter.Type;
        while (currentType != null)
        {
            if (currentType.Name == typeof(T).Name)
                return true;
            currentType = currentType.BaseType;
        }
        return false;
    }
}