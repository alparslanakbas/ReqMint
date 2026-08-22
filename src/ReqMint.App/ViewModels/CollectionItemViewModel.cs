using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace ReqMint.App.ViewModels;

public sealed class CollectionItemViewModel
{
    public CollectionItemViewModel(
        Guid id,
        string name,
        IEnumerable<SavedRequestItemViewModel> requests,
        Action<Guid> selectCollection)
    {
        Id = id;
        Name = name;
        Requests = new ObservableCollection<SavedRequestItemViewModel>(requests);
        SelectCommand = new RelayCommand(() => selectCollection(Id));
    }

    public Guid Id { get; }

    public string Name { get; }

    public ObservableCollection<SavedRequestItemViewModel> Requests { get; }

    public int RequestCount => Requests.Count;

    public IRelayCommand SelectCommand { get; }
}
