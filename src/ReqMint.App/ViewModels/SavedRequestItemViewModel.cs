using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public sealed class SavedRequestItemViewModel : ViewModelBase
{
    public SavedRequestItemViewModel(
        RequestDocument document,
        Func<RequestDocument, Task> openRequest,
        Func<RequestDocument, Task>? duplicateRequest = null,
        Func<RequestDocument, Task>? deleteRequest = null)
    {
        Document = document;
        OpenCommand = new AsyncRelayCommand(() => openRequest(Document));
        DuplicateCommand = new AsyncRelayCommand(
            () => duplicateRequest?.Invoke(Document) ?? Task.CompletedTask);
        DeleteCommand = new AsyncRelayCommand(
            () => deleteRequest?.Invoke(Document) ?? Task.CompletedTask);
    }

    public RequestDocument Document { get; }

    public string Name => Document.Name;

    public string Method => Document.Method;

    public bool IsGetMethod => HttpMethodStyle.IsGet(Method);

    public bool IsPostMethod => HttpMethodStyle.IsPost(Method);

    public bool IsPutMethod => HttpMethodStyle.IsPut(Method);

    public bool IsPatchMethod => HttpMethodStyle.IsPatch(Method);

    public bool IsDeleteMethod => HttpMethodStyle.IsDelete(Method);

    public IAsyncRelayCommand OpenCommand { get; }

    public IAsyncRelayCommand DuplicateCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }
}
