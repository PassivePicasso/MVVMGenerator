using System.Windows;

using EnumTypes;

using MVVM.Generator.Attributes;

namespace TestProject
{
    public partial class DependencyPropertyTest : DependencyObject
    {
        [AutoDProp] private string title;
        [AutoDProp] private int count;
        [AutoDProp] private OtherType.TestEnum kind;
    }
}
