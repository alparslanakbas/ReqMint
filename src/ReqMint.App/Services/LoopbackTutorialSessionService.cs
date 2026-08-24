using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ReqMint.Core.Requests;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.Services;

public sealed class LoopbackTutorialSessionService : ITutorialSessionService
{
    public const int MaximumRequestHeaderBytes = 16 * 1024;

    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();
    private readonly IWorkspaceStore _workspaceStore;
    private readonly string _tutorialRoot;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private TcpListener? _listener;
    private CancellationTokenSource? _shutdown;
    private Task? _serverTask;
    private TutorialSession? _session;
    private bool _isDisposed;

    public LoopbackTutorialSessionService(
        IWorkspaceStore workspaceStore,
        string tutorialRoot)
    {
        ArgumentNullException.ThrowIfNull(workspaceStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(tutorialRoot);
        _workspaceStore = workspaceStore;
        _tutorialRoot = Path.GetFullPath(tutorialRoot);
    }

    public async Task<TutorialSession> StartAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_session is not null)
            {
                return _session;
            }

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(backlog: 4);
            var shutdown = new CancellationTokenSource();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var baseUri = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute);
            var workspaceDirectory = Path.Combine(
                _tutorialRoot,
                Guid.NewGuid().ToString("N"));
            var session = CreateSession(workspaceDirectory, baseUri);

            try
            {
                await _workspaceStore.SaveAsync(
                    session.WorkspaceDirectory,
                    session.Snapshot,
                    cancellationToken);
            }
            catch
            {
                listener.Stop();
                shutdown.Dispose();
                TryDeleteOwnedWorkspace(workspaceDirectory);
                throw;
            }

            _listener = listener;
            _shutdown = shutdown;
            _session = session;
            _serverTask = RunServerAsync(listener, shutdown.Token);
            return session;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _shutdown?.Cancel();
        _listener?.Stop();
        try
        {
            _serverTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _shutdown?.Dispose();
        if (_session is not null)
        {
            TryDeleteOwnedWorkspace(_session.WorkspaceDirectory);
        }

        _startLock.Dispose();
    }

    private static TutorialSession CreateSession(string workspaceDirectory, Uri baseUri)
    {
        var collectionId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var collection = new CollectionDocument
        {
            Id = collectionId,
            Name = "Getting Started",
            Requests = CreateDemoRequests(),
        };
        var environment = new EnvironmentDocument
        {
            Id = environmentId,
            Name = "Tutorial",
            Variables =
            [
                new EnvironmentVariable(
                    "TUTORIAL_BASE_URL",
                    baseUri.GetLeftPart(UriPartial.Authority)),
            ],
        };
        var workspace = new WorkspaceDocument
        {
            Id = Guid.NewGuid(),
            Name = "ReqMint Tutorial",
            Collections =
            [
                new WorkspaceFileReference(
                    collectionId,
                    collection.Name,
                    "collections/getting-started.json"),
            ],
            Environments =
            [
                new WorkspaceFileReference(
                    environmentId,
                    environment.Name,
                    "environments/tutorial.json"),
            ],
        };
        var request = new RequestDocument
        {
            Id = Guid.NewGuid(),
            Name = "Say hello to ReqMint",
            Method = "GET",
            Url = "{{TUTORIAL_BASE_URL}}/api/hello",
            Headers = [new RequestField("Accept", "application/json")],
            TimeoutSeconds = 10,
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.StatusCodeEquals,
                    ExpectedStatusCode = 200,
                },
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.JsonPointerExists,
                    JsonPointer = "/message",
                },
            ],
        };

        return new TutorialSession(
            workspaceDirectory,
            baseUri,
            new WorkspaceSnapshot(workspace, [collection], [environment]),
            request,
            collectionId,
            environmentId);
    }

    private static IReadOnlyList<RequestDocument> CreateDemoRequests() =>
    [
        new RequestDocument
        {
            Id = Guid.NewGuid(),
            Name = "Check service health",
            Method = "GET",
            Url = "{{TUTORIAL_BASE_URL}}/api/health",
            Headers = [new RequestField("Accept", "application/json")],
            TimeoutSeconds = 10,
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.StatusCodeEquals,
                    ExpectedStatusCode = 200,
                },
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.MaximumDuration,
                    MaximumDurationMilliseconds = 1_000,
                },
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.JsonPointerExists,
                    JsonPointer = "/status",
                },
            ],
        },
        new RequestDocument
        {
            Id = Guid.NewGuid(),
            Name = "List active API projects",
            Method = "GET",
            Url = "{{TUTORIAL_BASE_URL}}/api/projects",
            QueryParameters = [new RequestField("status", "active")],
            Headers = [new RequestField("Accept", "application/json")],
            TimeoutSeconds = 10,
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.StatusCodeEquals,
                    ExpectedStatusCode = 200,
                },
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.JsonPointerExists,
                    JsonPointer = "/data",
                },
            ],
        },
        new RequestDocument
        {
            Id = Guid.NewGuid(),
            Name = "Inspect current release",
            Method = "GET",
            Url = "{{TUTORIAL_BASE_URL}}/api/releases/current",
            Headers = [new RequestField("Accept", "application/json")],
            TimeoutSeconds = 10,
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.StatusCodeEquals,
                    ExpectedStatusCode = 200,
                },
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.JsonPointerExists,
                    JsonPointer = "/version",
                },
            ],
        },
    ];

    private static async Task RunServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            using (client)
            {
                await HandleClientAsync(client, cancellationToken);
            }
        }
    }

    private static async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        var stream = client.GetStream();
        var buffer = new byte[MaximumRequestHeaderBytes];
        var totalBytes = 0;
        var hasCompleteHeaders = false;
        try
        {
            while (totalBytes < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(totalBytes, buffer.Length - totalBytes),
                    requestTimeout.Token);
                if (bytesRead == 0)
                {
                    return;
                }

                totalBytes += bytesRead;
                if (buffer.AsSpan(0, totalBytes).IndexOf(HeaderTerminator) >= 0)
                {
                    hasCompleteHeaders = true;
                    break;
                }
            }

            if (!hasCompleteHeaders)
            {
                await WriteResponseAsync(
                    stream,
                    431,
                    "Request Header Fields Too Large",
                    ErrorJson(),
                    requestTimeout.Token);
                return;
            }

            var firstLineEnd = buffer.AsSpan(0, totalBytes).IndexOf("\r\n"u8);
            if (firstLineEnd <= 0)
            {
                await WriteResponseAsync(stream, 400, "Bad Request", ErrorJson(), requestTimeout.Token);
                return;
            }

            var requestLine = Encoding.ASCII.GetString(buffer, 0, firstLineEnd);
            var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            {
                await WriteResponseAsync(stream, 400, "Bad Request", ErrorJson(), requestTimeout.Token);
                return;
            }

            if (!string.Equals(parts[0], "GET", StringComparison.Ordinal))
            {
                await WriteResponseAsync(stream, 405, "Method Not Allowed", ErrorJson(), requestTimeout.Token);
                return;
            }

            if (!Uri.TryCreate($"http://localhost{parts[1]}", UriKind.Absolute, out var target))
            {
                await WriteResponseAsync(stream, 404, "Not Found", ErrorJson(), requestTimeout.Token);
                return;
            }

            var body = CreateResponseBody(target.AbsolutePath);
            if (body is null)
            {
                await WriteResponseAsync(stream, 404, "Not Found", ErrorJson(), requestTimeout.Token);
                return;
            }

            await WriteResponseAsync(stream, 200, "OK", body, requestTimeout.Token);
        }
        catch (OperationCanceledException) when (requestTimeout.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
    }

    private static byte[]? CreateResponseBody(string path) => path switch
    {
        "/api/hello" => JsonSerializer.SerializeToUtf8Bytes(new
        {
            message = "Hello from ReqMint",
            source = "local-tutorial",
            success = true,
        }),
        "/api/health" => JsonSerializer.SerializeToUtf8Bytes(new
        {
            status = "healthy",
            service = "ReqMint Local Demo API",
            environment = "local",
        }),
        "/api/projects" => JsonSerializer.SerializeToUtf8Bytes(new
        {
            data = new[]
            {
                new { id = "proj_101", name = "Payments API", status = "active" },
                new { id = "proj_102", name = "Customer Portal", status = "active" },
                new { id = "proj_103", name = "Inventory Service", status = "active" },
            },
            total = 3,
        }),
        "/api/releases/current" => JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = "1.0.0-preview.1",
            channel = "Community Preview",
            platforms = new[] { "Windows", "macOS", "Linux" },
            ready = true,
        }),
        _ => null,
    };

    private static byte[] ErrorJson() =>
        "{\"error\":\"Tutorial endpoint not found\"}"u8.ToArray();

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int statusCode,
        string reasonPhrase,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(headers, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private void TryDeleteOwnedWorkspace(string workspaceDirectory)
    {
        try
        {
            var fullPath = Path.GetFullPath(workspaceDirectory);
            var expectedParent = _tutorialRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(fullPath);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(expectedParent, comparison)
                || !Guid.TryParseExact(leaf, "N", out _))
            {
                return;
            }

            if (Directory.Exists(fullPath))
            {
                File.Delete(Path.Combine(fullPath, "reqmint.workspace.json"));
                File.Delete(Path.Combine(fullPath, "collections", "getting-started.json"));
                File.Delete(Path.Combine(fullPath, "environments", "tutorial.json"));
                TryDeleteEmptyDirectory(Path.Combine(fullPath, "collections"));
                TryDeleteEmptyDirectory(Path.Combine(fullPath, "environments"));
                TryDeleteEmptyDirectory(fullPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: false);
        }
    }
}
