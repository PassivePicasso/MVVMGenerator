using System.Linq;

using Microsoft.CodeAnalysis;

using MVVM.Generator.Attributes;

namespace MVVM.Generator.Extraction;

internal enum CanExecuteFailure
{
    None,
    MemberNotFound,
    NotBoolean,
    ParameterCountMismatch,
    ParameterTypeMismatch,
}

/// <summary>
/// The resolved CanExecute member for a command, plus why it was rejected.
/// </summary>
internal sealed class CanExecuteResolution
{
    public ISymbol? Member { get; set; }
    public bool IsProperty { get; set; }
    public CanExecuteFailure Failure { get; set; }

    /// <summary>Index of the offending parameter for ParameterTypeMismatch.</summary>
    public int ParameterIndex { get; set; }

    public bool IsValid => Failure == CanExecuteFailure.None && Member != null;
}

/// <summary>
/// Single source of truth for how a CanExecute member is found and checked.
/// The generator and the analyzer both resolve through here so they cannot
/// disagree about what counts as a valid CanExecute.
/// </summary>
internal static class CanExecuteResolver
{
    /// <summary>The conventional CanExecute name for a command method.</summary>
    public static string ConventionName(IMethodSymbol command) => $"Can{command.Name}";

    /// <summary>The name supplied to the attribute, or empty when none was.</summary>
    public static string SuppliedName(IMethodSymbol command)
    {
        var attributeData = command.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == nameof(AutoCommandAttribute));

        if (attributeData?.ConstructorArguments.Length > 0
            && attributeData.ConstructorArguments[0].Value is string suppliedName)
        {
            return suppliedName;
        }

        return string.Empty;
    }

    /// <summary>
    /// Finds the member a command would bind to. A command taking a parameter
    /// can only bind a method; a parameterless one prefers a property.
    /// </summary>
    public static (ISymbol? Member, bool IsProperty) Find(IMethodSymbol command, string name)
    {
        if (string.IsNullOrEmpty(name)) return (null, false);

        if (command.Parameters.Length > 0)
            return (FindMethod(command, name), false);

        var property = FindProperty(command, name);
        if (property != null) return (property, true);

        return (FindMethod(command, name), false);
    }

    public static CanExecuteResolution Resolve(IMethodSymbol command, string name)
    {
        var (member, isProperty) = Find(command, name);
        var resolution = new CanExecuteResolution { Member = member, IsProperty = isProperty };

        if (member == null)
        {
            resolution.Failure = CanExecuteFailure.MemberNotFound;
            return resolution;
        }

        if (isProperty)
        {
            var property = (IPropertySymbol)member;
            if (property.Type.SpecialType != SpecialType.System_Boolean)
                resolution.Failure = CanExecuteFailure.NotBoolean;

            return resolution;
        }

        var method = (IMethodSymbol)member;

        if (method.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            resolution.Failure = CanExecuteFailure.NotBoolean;
            return resolution;
        }

        if (method.Parameters.Length != command.Parameters.Length)
        {
            resolution.Failure = CanExecuteFailure.ParameterCountMismatch;
            return resolution;
        }

        for (var index = 0; index < command.Parameters.Length; index++)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    command.Parameters[index].Type, method.Parameters[index].Type))
                continue;

            resolution.Failure = CanExecuteFailure.ParameterTypeMismatch;
            resolution.ParameterIndex = index;
            return resolution;
        }

        return resolution;
    }

    /// <summary>
    /// A static command renders its CanExecute against the type name, so an
    /// instance member cannot be bound to one.
    /// </summary>
    public static bool IsStaticCompatible(IMethodSymbol command, ISymbol member)
    {
        return !command.IsStatic || member.IsStatic;
    }

    private static IMethodSymbol? FindMethod(IMethodSymbol command, string name)
    {
        return command.ContainingType.GetMembers(name)
            .OfType<IMethodSymbol>()
            .FirstOrDefault();
    }

    private static IPropertySymbol? FindProperty(IMethodSymbol command, string name)
    {
        return command.ContainingType.GetMembers(name)
            .OfType<IPropertySymbol>()
            .FirstOrDefault();
    }
}
