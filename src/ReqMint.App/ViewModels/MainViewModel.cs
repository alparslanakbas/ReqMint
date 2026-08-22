using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Requests;
using System.Text.Json;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public IReadOnlyList<string> Methods { get; } = ["GET"];

    [ObservableProperty]
    public partial string SelectedMethod { get; set; } = "GET";

    [ObservableProperty]
    public partial string Url { get; set; } = "https://api.example.com/v1/orders/42";

    [ObservableProperty]
    public partial string ResponseBody { get; set; } = "Send a request to inspect its response.";

    [ObservableProperty]
    public partial string ResponseStatus { get; set; } = "Ready";

    [ObservableProperty]
    public partial string ResponseTime { get; set; } = "—";

    [ObservableProperty]
    public partial bool HasResponse { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    private readonly IRequestExecutor _requestExecutor;

    public MainViewModel(IRequestExecutor requestExecutor)
    {
        _requestExecutor = requestExecutor;
    }

    private bool CanSend() => !IsSending;

    [RelayCommand(CanExecute = nameof(CanSend), IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        ApiRequest request;

        try
        {
            request = ApiRequest.Create(SelectedMethod, Url);
        }
        catch (ArgumentException exception)
        {
            ResponseStatus = "Invalid URL";
            ResponseBody = exception.Message;
            HasResponse = true;
            return;
        }

        IsSending = true;
        ResponseStatus = "Sending...";
        ResponseTime = "—";

        try
        {
            var response = await _requestExecutor.ExecuteAsync(request, cancellationToken);

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}".TrimEnd();
            ResponseTime = $"{response.Duration.TotalMilliseconds:N0} ms";
            ResponseBody = FormatBody(response.Body, response.ContentType);

            if (response.IsBodyTruncated)
            {
                ResponseBody += "\n\n— Preview limited to 2 MB —";
            }

            HasResponse = true;
        }
        catch (OperationCanceledException)
        {
            ResponseStatus = "Cancelled";
            ResponseBody = "The request was cancelled.";
            HasResponse = true;
        }
        catch (HttpRequestException exception)
        {
            ResponseStatus = "Connection failed";
            ResponseBody = exception.Message;
            HasResponse = true;
        }
        finally
        {
            IsSending = false;
        }
    }

    private static string FormatBody(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body) ||
            contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return body;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
