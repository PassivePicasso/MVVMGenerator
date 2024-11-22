using System;
using System.Collections.Generic;
using System.Text;

namespace MVVM.Generator.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public class DependsOnAttribute : Attribute
{
    public string[] PropertyNames { get; }

    public DependsOnAttribute(params string[] propertyNames)
    {
        PropertyNames = propertyNames;
    }
}

