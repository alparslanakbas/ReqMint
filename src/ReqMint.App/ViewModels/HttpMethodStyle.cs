namespace ReqMint.App.ViewModels;

/// <summary>
/// Tells the view which style class an HTTP method belongs to. Colours live in
/// App.axaml as theme resources rather than here, so switching theme repaints
/// them straight away.
/// </summary>
public static class HttpMethodStyle
{
    public static bool IsGet(string? method) => Is(method, "GET");

    public static bool IsPost(string? method) => Is(method, "POST");

    public static bool IsPut(string? method) => Is(method, "PUT");

    public static bool IsPatch(string? method) => Is(method, "PATCH");

    public static bool IsDelete(string? method) => Is(method, "DELETE");

    private static bool Is(string? method, string expected) =>
        string.Equals(method?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// How a response finished, used only to colour the status text.
/// </summary>
public enum ResponseStatusKind
{
    Neutral,
    Success,
    Redirect,
    ClientError,
    Failure,
}

public static class ResponseStatusKinds
{
    public static ResponseStatusKind FromStatusCode(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => ResponseStatusKind.Success,
        >= 300 and < 400 => ResponseStatusKind.Redirect,
        >= 400 and < 500 => ResponseStatusKind.ClientError,
        >= 500 => ResponseStatusKind.Failure,
        _ => ResponseStatusKind.Neutral,
    };
}
