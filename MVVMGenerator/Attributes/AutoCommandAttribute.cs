using System;

namespace MVVMGenerator.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AutoCommandAttribute : Attribute
    {
        public string CanExecuteMethod { get; set; }

        public AutoCommandAttribute()
        {
        }
        public AutoCommandAttribute(string canExecuteMethod)
        {
            CanExecuteMethod = canExecuteMethod;
        }
    }
}
