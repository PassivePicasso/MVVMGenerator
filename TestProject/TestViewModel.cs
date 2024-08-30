using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using System.Windows;

using MVVMGenerator.Attributes;

using Newtonsoft.Json.Converters;

namespace TestProject
{
    public partial class TestViewModel
    {
        [JsonConverter(typeof(StringEnumConverter))]
        [AutoNotify] private Visibility visibility = Visibility.Visible;

        [AutoNotify] private Dictionary<string?, byte[]>? dataDictionary;
        [AutoNotify] private byte[] data;
        [AutoNotify] private bool isOpen;
        
        [AutoNotify(CollectionChangedHandlerName = nameof(OnCollectionChanged))] 
        private ObservableCollection<string> collection = new ObservableCollection<string>();

        public void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
        }

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
