using System;

namespace MVVM.Generator.Attributes;


[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class AddAttributeAttribute : Attribute
{
    public AddAttributeAttribute(Type attributeType, object[] args)
    {
        AttributeType = attributeType;
        Args = args;
    }

    public Type AttributeType { get; }
    public object[] Args { get; }
}
