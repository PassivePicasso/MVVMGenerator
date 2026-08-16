namespace MVVM.Generator.Models;

/// <summary>
/// The complete, symbol-free description of one partial class to generate.
/// Value equality here is what lets the output stage be skipped when an edit
/// does not change what would be produced.
/// </summary>
internal sealed record ClassModel(
    string Namespace,
    string ClassName,
    string HintName,
    bool BaseImplementsInpc,
    EquatableArray<NotifyFieldModel> NotifyFields,
    EquatableArray<CommandModel> Commands,
    EquatableArray<BackingPropertyModel> DependencyProperties,
    EquatableArray<BackingPropertyModel> StyledProperties,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    public bool HasContent =>
        !NotifyFields.IsEmpty
        || !Commands.IsEmpty
        || !DependencyProperties.IsEmpty
        || !StyledProperties.IsEmpty;
}
