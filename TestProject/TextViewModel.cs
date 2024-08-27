using MVVMGenerator.Attributes;

namespace TestProject
{
    public partial class TestViewModel
    {
        [AutoCommand(nameof(CanTest))]
        public void Test(int input)
        {
        }

        public bool CanTest(int input) => true;
    }
}
