namespace MVVM.Generator.Models;

/// <summary>
/// A field projected onto a framework-registered property. AutoDProp (WPF
/// DependencyProperty) and AutoSProp (Avalonia StyledProperty) need identical
/// facts and differ only in how they render.
/// </summary>
internal sealed record BackingPropertyModel(
    string PropertyName,
    string TypeDisplayName,
    string TypeShortName,
    string OwnerTypeName,
    EquatableArray<string> Usings);
