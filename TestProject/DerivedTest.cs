using MVVM.Generator.Attributes;

namespace TestProject
{
    public partial class DerivedTest : Test
    {

        [AutoNotify]
        public string someNewValue;
    }
}
