using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

using EnumTypes;

using MVVM.Generator.Attributes;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;


namespace TestProject
{
    public partial class Test
    {
        [AutoNotify]
        private OtherType.TestEnum[]? testEnumValues;

        [JsonConverter(typeof(StringEnumConverter))]
        [AutoNotify]
        private Visibility visibility = Visibility.Visible;

        [AutoNotify(SetterAccess = Access.Private)]
        private Dictionary<string, byte[]>? dataDictionary;

        [AutoNotify] private byte[]? data;

        [JsonIgnore, AutoNotify(PropertyChangedHandlerName = nameof(IsOpenedChanged))]
        private bool isOpen;
        private void IsOpenedChanged(object? sender, EventArgs args) { }

        [DependsOn(nameof(Visibility))]
        public bool Ready => IsOpen;


        [AutoNotify(CollectionChangedHandlerName = nameof(OnCollectionChanged))]
        private ObservableCollection<string> collection = new ObservableCollection<string>();
        public void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) { }

        [AutoCommand(nameof(CanExecuteClose))]
        public void Close() => IsOpen = false;
        public bool CanExecuteClose() => IsOpen;

        //[AutoCommand]
        //private void BrokenTest()
        //{
        //}

        [AutoCommand]
        [AddAttribute(typeof(JsonIgnoreAttribute), [])]
        public void TestMethod(int input)
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
