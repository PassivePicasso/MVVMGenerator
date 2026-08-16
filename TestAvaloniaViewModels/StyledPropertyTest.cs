using Avalonia;

using MVVM.Generator.Attributes;

namespace TestAvaloniaViewModels
{
    public partial class StyledPropertyTest : AvaloniaObject
    {
        [AutoSProp] private string title;
        [AutoSProp] private int count;
    }
}
