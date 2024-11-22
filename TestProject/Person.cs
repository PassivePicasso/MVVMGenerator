using MVVM.Generator.Attributes;

namespace TestViewModels
{
    public partial class Person
    {
        [AutoNotify] private string firstName;
        [AutoNotify] private string lastName;

        [DependsOn(nameof(firstName), nameof(lastName))]
        public string FullName => $"{firstName} {lastName}";
    }
}