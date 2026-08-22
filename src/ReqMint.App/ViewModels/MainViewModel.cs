using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Requests;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public IReadOnlyList<string> Methods { get; } =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    public IReadOnlyList<string> BodyTypes { get; } =
        ["None", "JSON", "Text", "XML", "Form URL Encoded"];

    public ObservableCollection<RequestFieldViewModel> QueryParameters { get; } =
    [
        new("include", "items"),
        new("locale", "en-US") { IsEnabled = false },
    ];

    public ObservableCollection<RequestFieldViewModel> Headers { get; } =
    [
        new("Accept", "application/json"),
        new("X-Client", "ReqMint") { IsEnabled = false },
    ];

    [ObservableProperty]
    public partial string SelectedMethod { get; set; } = "GET";

    [ObservableProperty]
    public partial string Url { get; set; } = "https://api.example.com/v1/orders/42";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBodyEnabled))]
    public partial string SelectedBodyType { get; set; } = "None";

    [ObservableProperty]
    public partial string RequestBody { get; set; } = "{\n  \"name\": \"Sample order\"\n}";

    [ObservableProperty]
    public partial decimal TimeoutSeconds { get; set; } = 30;

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

    public bool IsBodyEnabled => SelectedBodyType != "None";

    public MainViewModel(IRequestExecutor requestExecutor)
    {
        _requestExecutor = requestExecutor;
    }

    private bool CanSend() => !IsSending;

    [RelayCommand]
    private void AddQueryParameter() => QueryParameters.Add(new RequestFieldViewModel());

    [RelayCommand]
    private void AddHeader() => Headers.Add(new RequestFieldViewModel());

    [RelayCommand(CanExecute = nameof(CanSend), IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        ApiRequest request;

        try
        {
            if (TimeoutSeconds <= 0)
            {
                throw new ArgumentException("Request timeout must be greater than zero.");
            }

            request = ApiRequest.Create(SelectedMethod, Url) with
            {
                QueryParameters = QueryParameters
                    .Where(field => field.IsEnabled && !string.IsNullOrWhiteSpace(field.Name))
                    .Select(field => new RequestField(field.Name.Trim(), field.Value))
                    .ToArray(),
                Headers = Headers
                    .Where(field => field.IsEnabled && !string.IsNullOrWhiteSpace(field.Name))
                    .Select(field => new RequestField(field.Name.Trim(), field.Value))
                    .ToArray(),
                Body = CreateBody(),
                Timeout = TimeSpan.FromSeconds((double)TimeoutSeconds),
            };
        }
        catch (ArgumentException exception)
        {
            ResponseStatus = "Invalid request";
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
        catch (TimeoutException exception)
        {
            ResponseStatus = "Timed out";
            ResponseBody = exception.Message;
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

    private ApiRequestBody? CreateBody() => SelectedBodyType switch
    {
        "JSON" => new ApiRequestBody(RequestBody, "application/json"),
        "Text" => new ApiRequestBody(RequestBody, "text/plain"),
        "XML" => new ApiRequestBody(RequestBody, "application/xml"),
        "Form URL Encoded" => new ApiRequestBody(RequestBody, "application/x-www-form-urlencoded"),
        _ => null,
    };

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
