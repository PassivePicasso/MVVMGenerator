using System;

namespace MVVM.Generator.Attributes;
public enum Access { Private, Internal, Protected, Public }
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class AutoNotifyAttribute : Attribute
{
    public string? CollectionChangedHandlerName { get; set; }
    public string? PropertyChangedHandlerName { get; set; }
    public bool IsVirtual { get; set; }
    public Access GetterAccess { get; set; } = Access.Public;
    public Access SetterAccess { get; set; } = Access.Public;
}