using System.Net;
using ReqMint.Core.Requests;

namespace ReqMint.Http;

/// <summary>
/// Keeps automatic HTTP cookies in memory and isolates them by workspace.
/// Explicit Cookie headers continue to work when automatic handling is disabled.
/// </summary>
public sealed class WorkspaceHttpRequestExecutor : IRequestExecutor, IRequestCookieManager, IDisposable
{
    private const string NoWorkspaceScope = "<no-workspace>";

    private readonly object _sync = new();
    private readonly HttpRequestExecutor _statelessExecutor = new();
    private readonly Dictionary<string, CookieSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private string _activeScope = NoWorkspaceScope;
    private bool _isDisposed;

    public bool IsEnabled { get; private set; }

    public Task<ApiResponse> ExecuteAsync(
        ApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpRequestExecutor executor;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            executor = IsEnabled
                ? GetOrCreateSession(_activeScope).Executor
                : _statelessExecutor;
        }

        return executor.ExecuteAsync(request, cancellationToken);
    }

    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (IsEnabled == enabled)
            {
                return;
            }

            IsEnabled = enabled;
            if (!enabled)
            {
                ClearAllCookies();
            }
        }
    }

    public void SelectWorkspace(string? workspaceDirectory)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _activeScope = string.IsNullOrWhiteSpace(workspaceDirectory)
                ? NoWorkspaceScope
                : Path.GetFullPath(workspaceDirectory);
        }
    }

    public void ClearActiveWorkspace()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_sessions.TryGetValue(_activeScope, out var session))
            {
                ExpireCookies(session.Cookies);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _statelessExecutor.Dispose();
            foreach (var session in _sessions.Values)
            {
                session.Executor.Dispose();
            }

            _sessions.Clear();
        }
    }

    private CookieSession GetOrCreateSession(string scope)
    {
        if (_sessions.TryGetValue(scope, out var session))
        {
            return session;
        }

        var cookies = new CookieContainer();
        session = new CookieSession(
            cookies,
            new HttpRequestExecutor(HttpRequestExecutor.CreateDefaultHandler(
                useCookies: true,
                cookieContainer: cookies)));
        _sessions.Add(scope, session);
        return session;
    }

    private void ClearAllCookies()
    {
        foreach (var session in _sessions.Values)
        {
            ExpireCookies(session.Cookies);
        }
    }

    private static void ExpireCookies(CookieContainer container)
    {
        foreach (Cookie cookie in container.GetAllCookies())
        {
            cookie.Expired = true;
        }
    }

    private sealed record CookieSession(
        CookieContainer Cookies,
        HttpRequestExecutor Executor);
}
