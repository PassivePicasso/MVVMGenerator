using System;

namespace MVVM.Generator.Attributes;
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AutoCommandAttribute : Attribute
{
    public string? CanExecuteMethod { get; }
    public AutoCommandAttribute() { }
    public AutoCommandAttribute(string canExecuteMethod)
    {
        CanExecuteMethod = canExecuteMethod;
    }
}