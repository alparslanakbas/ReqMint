using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Requests;

namespace ReqMint.App.ViewModels;

/// <summary>
/// Everything the composer holds for one open request. The editor keeps working
/// on the view model's own properties; a tab simply stashes that state while it
/// is in the background, which keeps every existing binding untouched.
/// </summary>
public sealed record RequestTabState
{
    public string RequestName { get; init; } = "New request";

    public string Method { get; init; } = "GET";

    public string Url { get; init; } = string.Empty;

    public string BodyType { get; init; } = "None";

    public string Body { get; init; } = string.Empty;

    public int AuthenticationTypeIndex { get; init; }

    public string AuthenticationBearerToken { get; init; } = "{{TOKEN}}";

    public string AuthenticationBasicUsername { get; init; } = string.Empty;

    public string AuthenticationBasicPassword { get; init; } = "{{PASSWORD}}";

    public string AuthenticationApiKeyName { get; init; } = "X-API-Key";

    public string AuthenticationApiKeyValue { get; init; } = "{{API_KEY}}";

    public int AuthenticationApiKeyLocationIndex { get; init; }

    public decimal TimeoutSeconds { get; init; } = 30;

    public bool IsStatusAssertionEnabled { get; init; }

    public decimal AssertionExpectedStatusCode { get; init; } = 200;

    public bool IsDurationAssertionEnabled { get; init; }

    public decimal AssertionMaximumDurationMilliseconds { get; init; } = 1000;

    public bool IsJsonFieldAssertionEnabled { get; init; }

    public string AssertionJsonPointer { get; init; } = "/id";

    public IReadOnlyList<RequestField> QueryParameters { get; init; } = [];

    public IReadOnlyList<RequestField> Headers { get; init; } = [];

    public IReadOnlyList<RequestField> FormBodyFields { get; init; } = [];

    public IReadOnlyList<RequestFileField> MultipartFileFields { get; init; } = [];

    public string ResponseBody { get; init; } = string.Empty;

    public string ResponseStatus { get; init; } = string.Empty;

    public string ResponseTime { get; init; } = "—";

    public bool HasResponse { get; init; }

    public ResponseStatusKind ResponseStatusKind { get; init; }

    public string CleanDraft { get; init; } = string.Empty;
}

public sealed partial class RequestTabViewModel : ViewModelBase
{
    public RequestTabViewModel(
        Func<RequestTabViewModel, Task> select,
        Func<RequestTabViewModel, Task> close,
        Action<RequestTabViewModel, int> move)
    {
        SelectCommand = new AsyncRelayCommand(() => select(this));
        CloseCommand = new AsyncRelayCommand(() => close(this));
        MoveLeftCommand = new RelayCommand(() => move(this, -1));
        MoveRightCommand = new RelayCommand(() => move(this, 1));
    }

    /// <summary>Saved request this tab edits, or null while it is still new.</summary>
    public Guid? RequestId { get; set; }

    public Guid? CollectionId { get; set; }

    public RequestTabState State { get; set; } = new();

    [ObservableProperty]
    public partial string Title { get; set; } = "New request";

    /// <summary>
    /// Collection name, shown only when another open tab carries the same title,
    /// so two requests called "New request" can still be told apart.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubtitle))]
    public partial string Subtitle { get; set; } = string.Empty;

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGetMethod))]
    [NotifyPropertyChangedFor(nameof(IsPostMethod))]
    [NotifyPropertyChangedFor(nameof(IsPutMethod))]
    [NotifyPropertyChangedFor(nameof(IsPatchMethod))]
    [NotifyPropertyChangedFor(nameof(IsDeleteMethod))]
    public partial string Method { get; set; } = "GET";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    public bool IsGetMethod => HttpMethodStyle.IsGet(Method);

    public bool IsPostMethod => HttpMethodStyle.IsPost(Method);

    public bool IsPutMethod => HttpMethodStyle.IsPut(Method);

    public bool IsPatchMethod => HttpMethodStyle.IsPatch(Method);

    public bool IsDeleteMethod => HttpMethodStyle.IsDelete(Method);

    public IAsyncRelayCommand SelectCommand { get; }

    public IAsyncRelayCommand CloseCommand { get; }

    public IRelayCommand MoveLeftCommand { get; }

    public IRelayCommand MoveRightCommand { get; }
}
