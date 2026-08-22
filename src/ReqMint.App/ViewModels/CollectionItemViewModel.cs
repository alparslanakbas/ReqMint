using System.Collections.ObjectModel;

namespace ReqMint.App.ViewModels;

public sealed class CollectionItemViewModel
{
    public CollectionItemViewModel(
        Guid id,
        string name,
        IEnumerable<SavedRequestItemViewModel> requests)
    {
        Id = id;
        Name = name;
        Requests = new ObservableCollection<SavedRequestItemViewModel>(requests);
    }

    public Guid Id { get; }

    public string Name { get; }

    public ObservableCollection<SavedRequestItemViewModel> Requests { get; }

    public int RequestCount => Requests.Count;
}
