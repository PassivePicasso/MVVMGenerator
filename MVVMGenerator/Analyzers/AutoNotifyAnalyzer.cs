using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using MVVM.Generator.Attributes;

namespace MVVM.Generator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AutoNotifyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "AutoNotifyAnalyzer";
    private static readonly LocalizableString Title = "AutoNotifyAttribute usage";
    private static readonly LocalizableString MessageFormat = "Field '{0}' with AutoNotifyAttribute has an issue: {1}";
    private static readonly LocalizableString Description = "Fields with AutoNotifyAttribute must adhere to specific rules.";
    private const string Category = "Usage";
    private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.FilePath.EndsWith(".g.cs"))
        {
            return; // Skip generated code
        }

        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
        var attributes = fieldDeclaration.AttributeLists.SelectMany(al => al.Attributes);
        foreach (var attribute in attributes)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
            if (symbolInfo.Symbol?.ContainingType.Name == "AutoNotifyAttribute")
            {
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(fieldDeclaration.Declaration.Variables.First()) as IFieldSymbol;
                if (fieldSymbol == null) continue;

                // Check if the field is static
                if (fieldSymbol.IsStatic)
                {
                    ReportDiagnostic(context, fieldDeclaration, fieldSymbol, "Field must not be static.");
                    continue;
                }

                // Check for naming conflicts
                var propertyName = GetPropertyName(fieldSymbol);
                var containingType = fieldSymbol.ContainingType;
                var containedMembers = containingType.GetMembers();
                var containedTypeMembers = containedMembers.OfType<INamedTypeSymbol>().ToArray();
                List<INamedTypeSymbol> nonGeneratedMembers = new();
                foreach (var namedTypeSymbol in containedTypeMembers)
                {
                    var refs = namedTypeSymbol.DeclaringSyntaxReferences;
                    foreach (var refSyntax in refs)
                    {
                        var syntax = refSyntax.GetSyntax();
                        var syntaxTree = syntax.SyntaxTree;
                        var filePath = syntaxTree.FilePath;
                        if (filePath.EndsWith(".g.cs"))
                        {
                            goto outer;
                        }
                    }
                outer: continue;
                }
                var members = nonGeneratedMembers
                    .Select(m => m.Name)
                    .ToImmutableHashSet();

                if (members.Contains(propertyName))
                {
                    ReportDiagnostic(context, fieldDeclaration, fieldSymbol, "Generated property name conflicts with existing member.");
                    continue;
                }

                // Validate PropertyChangedHandlerName
                var attributeData = fieldSymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "AutoNotifyAttribute");
                if (attributeData != null)
                {
                    foreach (var namedArg in attributeData.NamedArguments)
                    {
                        if (namedArg.Key == nameof(AutoNotifyAttribute.PropertyChangedHandlerName))
                        {
                            var methodName = namedArg.Value.Value as string;
                            if (methodName == null)
                            {
                                ReportDiagnostic(context, fieldDeclaration, fieldSymbol, "PropertyChangedHandlerName must be a string.");
                            }
                            else if (!string.IsNullOrEmpty(methodName) && !IsValidEventHandler(methodName, containingType, context, fieldDeclaration, fieldSymbol))
                            {
                                ReportDiagnostic(context, fieldDeclaration, fieldSymbol, $"PropertyChangedHandler '{methodName}' is invalid.");
                            }
                        }
                        else if (namedArg.Key == nameof(AutoNotifyAttribute.CollectionChangedHandlerName))
                        {
                            var methodName = namedArg.Value.Value as string;
                            if (methodName == null)
                            {
                                ReportDiagnostic(context, fieldDeclaration, fieldSymbol, "CollectionChangedHandlerName must be a string.");
                            }
                            else if (!string.IsNullOrEmpty(methodName) && !IsValidCollectionChangedHandler(methodName, containingType, context, fieldDeclaration, fieldSymbol))
                            {
                                ReportDiagnostic(context, fieldDeclaration, fieldSymbol, $"CollectionChangedHandler '{methodName}' is invalid.");
                            }
                        }
                    }
                }
            }
        }
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration, IFieldSymbol fieldSymbol, string message)
    {
        var diagnostic = Diagnostic.Create(Rule, fieldDeclaration.GetLocation(), fieldSymbol.Name, message);
        context.ReportDiagnostic(diagnostic);
    }

    private static string GetPropertyName(IFieldSymbol fieldSymbol)
    {
        var name = fieldSymbol.Name;
        if (name.StartsWith("_"))
        {
            name = name.TrimStart('_');
        }
        else if (name.StartsWith("s_"))
        {
            name = name.Substring(2);
        }
        return char.ToUpper(name[0]) + name.Substring(1);
    }

    private static bool IsValidEventHandler(string methodName, INamedTypeSymbol containingType, SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration, IFieldSymbol fieldSymbol)
    {
        var methodSymbol = containingType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == methodName);
        if (methodSymbol == null || methodSymbol.ReturnType.SpecialType != SpecialType.System_Void || methodSymbol.Parameters.Length != 2)
        {
            return false;
        }
        var firstParameter = methodSymbol.Parameters[0];
        var secondParameter = methodSymbol.Parameters[1];
        return firstParameter.Type.SpecialType == SpecialType.System_Object && IsOrDescendedFrom<EventArgs>(secondParameter);
    }

    private static bool IsValidCollectionChangedHandler(string methodName, INamedTypeSymbol containingType, SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration, IFieldSymbol fieldSymbol)
    {
        var methodSymbol = containingType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == methodName);
        if (methodSymbol == null || methodSymbol.ReturnType.SpecialType != SpecialType.System_Void || methodSymbol.Parameters.Length != 2)
        {
            return false;
        }
        var firstParameter = methodSymbol.Parameters[0];
        var secondParameter = methodSymbol.Parameters[1];
        return firstParameter.Type.SpecialType == SpecialType.System_Object && IsOrDescendedFrom<NotifyCollectionChangedEventArgs>(secondParameter);
    }

    private static bool IsOrDescendedFrom<T>(IParameterSymbol parameter)
    {
        var eventArgsType = parameter.Type;
        while (eventArgsType != null)
        {
            if (eventArgsType.Name == typeof(T).Name)
            {
                return true;
            }
            eventArgsType = eventArgsType.BaseType;
        }
        return false;
    }
}
