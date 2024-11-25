using MVVM.Generator.Attributes;

namespace TestViewModels
{
    public partial class SearchViewModel
    {
        [DependsOn(nameof(searchServiceSearching), nameof(itemServiceSearching))]
        public bool Searching => itemServiceSearching || searchServiceSearching;

        [AutoNotify] private bool itemServiceSearching;
        [AutoNotify] private bool searchServiceSearching;
    }
}