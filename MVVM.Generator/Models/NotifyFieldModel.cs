namespace MVVM.Generator.Models;

/// <summary>
/// Everything the notify-property renderer needs about one [AutoNotify] field,
/// resolved to values so no symbol reaches the output stage.
/// </summary>
internal sealed record NotifyFieldModel(
    string FieldName,
    string PropertyName,
    string TypeName,
    bool IsStatic,
    string PropertyAttributes,
    string GetterAccess,
    string SetterAccess,
    string VirtualPrefix,
    string? PropertyChangedHandlerName,
    bool PropertyChangedHandlerIsParameterless,
    string? CollectionChangedHandlerName,
    EquatableArray<string> DependentProperties,
    EquatableArray<string> Usings);
