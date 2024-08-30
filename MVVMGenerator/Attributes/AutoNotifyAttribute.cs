using System;

namespace MVVMGenerator.Attributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class AutoNotifyAttribute : Attribute
    {
        public string CollectionChangedHandlerName { get; set; }
    }
}
