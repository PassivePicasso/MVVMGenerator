using MVVM.Generator.Attributes;

namespace TestViewModels
{
    public partial class Person
    {
        [AutoNotify] private string firstName;
        [AutoNotify] private string lastName;

        public string FullName => $"{firstName} {lastName}";
    }
}