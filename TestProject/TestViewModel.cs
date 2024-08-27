using MVVMGenerator.Attributes;

namespace TestProject
{
    public partial class TestViewModel
    {
        [AutoNotify] public bool isOpen;

        [AutoCommand(nameof(CanExecuteClose))]
        public void Close() => IsOpen = false;
        public bool CanExecuteClose() => IsOpen;


        [AutoCommand(nameof(CanTest))]
        public void Test(int input)
        {
        }

        public bool CanTest(int input) => true;

        [AutoCommand]
        public void ExecuteStuff()
        {

        }

        [AutoCommand]
        public static void TestStatic()
        {

        }
    }
}
