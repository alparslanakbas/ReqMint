using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public sealed class SavedRequestItemViewModel : ViewModelBase
{
    public SavedRequestItemViewModel(RequestDocument document, Func<RequestDocument, Task> openRequest)
    {
        Document = document;
        OpenCommand = new AsyncRelayCommand(() => openRequest(Document));
    }

    public RequestDocument Document { get; }

    public string Name => Document.Name;

    public string Method => Document.Method;

    public IAsyncRelayCommand OpenCommand { get; }
}
