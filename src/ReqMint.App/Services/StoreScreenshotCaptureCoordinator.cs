#if STORE_CAPTURE
using System.Diagnostics;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReqMint.App.ViewModels;

namespace ReqMint.App.Services;

internal static class StoreScreenshotCaptureCoordinator
{
    private const int ScreenshotWidth = 1920;
    private const int ScreenshotHeight = 1080;

    public static void Attach(
        Window window,
        MainViewModel viewModel)
    {
        var outputDirectory = Environment.GetEnvironmentVariable(
            "REQMINT_STORE_CAPTURE_OUTPUT");
        var locale = Environment.GetEnvironmentVariable(
            "REQMINT_STORE_CAPTURE_LOCALE");
        if (string.IsNullOrWhiteSpace(outputDirectory)
            || locale is not ("en-US" or "tr-TR"))
        {
            return;
        }

        window.Opened += async (_, _) =>
        {
            try
            {
                await CaptureAsync(
                    window,
                    viewModel,
                    Path.GetFullPath(outputDirectory),
                    locale);
                Environment.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(outputDirectory);
                await File.WriteAllTextAsync(
                    Path.Combine(outputDirectory, "capture-error.txt"),
                    exception.ToString());
                Environment.Exit(1);
            }
        };
    }

    private static async Task CaptureAsync(
        Window window,
        MainViewModel viewModel,
        string outputDirectory,
        string locale)
    {
        Directory.CreateDirectory(outputDirectory);
        ConfigureCaptureWindow(window);

        var languageCode = locale == "tr-TR" ? "tr" : "en";
        viewModel.Localization!.SelectedLanguage = viewModel.Localization.Languages.Single(
            language => language.Code == languageCode);
        viewModel.Themes.SelectedTheme = ThemeCatalog.Default;
        viewModel.IsOnboardingVisible = true;
        viewModel.OnboardingStep = 2;
        await viewModel.StartTutorialSampleCommand.ExecuteAsync(null);
        viewModel.IsTutorialGuideVisible = false;
        await viewModel.SendCommand.ExecuteAsync(null);
        await CaptureFrameAsync(window, outputDirectory, "01-request-builder.png");

        viewModel.ShowEnvironmentEditorCommand.Execute(null);
        await CaptureFrameAsync(window, outputDirectory, "02-collections-environments.png");

        viewModel.ShowCollectionsCommand.Execute(null);
        await viewModel.SaveRequestCommand.ExecuteAsync(null);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);
        await viewModel.StartCollectionRunCommand.ExecuteAsync(null);
        await CaptureFrameAsync(window, outputDirectory, "03-collection-runner.png");

        viewModel.CloseCollectionRunnerCommand.Execute(null);
        var workspaceDirectory = GetTutorialWorkspaceDirectory(viewModel);
        await PrepareGitSceneAsync(workspaceDirectory, locale);
        await viewModel.ShowGitCommand.ExecuteAsync(null);
        if (viewModel.GitChanges.Count == 0)
        {
            throw new InvalidOperationException(
                "The tutorial Git scene did not expose a ReqMint-managed change.");
        }

        await viewModel.GitChanges[0].OpenCommand.ExecuteAsync(null);
        viewModel.ReviewGitStageCommand.Execute(null);
        await CaptureFrameAsync(window, outputDirectory, "04-git-workflow.png");

        viewModel.ShowSettingsEditorCommand.Execute(null);
        viewModel.Themes.SelectedTheme = ThemeCatalog.Find("aurora-glass")
            ?? throw new InvalidOperationException("Aurora Glass theme is unavailable.");
        await CaptureFrameAsync(window, outputDirectory, "05-settings-support.png");
    }

    private static void ConfigureCaptureWindow(Window window)
    {
        window.WindowState = WindowState.Normal;
        window.WindowDecorations = WindowDecorations.None;
        window.CanResize = false;
        var scaling = Math.Max(1, window.RenderScaling);
        window.Width = ScreenshotWidth / scaling;
        window.Height = ScreenshotHeight / scaling;
    }

    private static async Task CaptureFrameAsync(
        Window window,
        string outputDirectory,
        string fileName)
    {
        await Dispatcher.UIThread.InvokeAsync(
            static () => { },
            DispatcherPriority.Render);
        await Task.Delay(250);
        await Dispatcher.UIThread.InvokeAsync(
            static () => { },
            DispatcherPriority.Render);

        var scaling = Math.Max(1, window.RenderScaling);
        using var bitmap = new RenderTargetBitmap(
            new PixelSize(ScreenshotWidth, ScreenshotHeight),
            new Vector(96 * scaling, 96 * scaling));
        bitmap.Render(window);
        bitmap.Save(
            Path.Combine(outputDirectory, fileName),
            PngBitmapEncoderOptions.Default);
    }

    private static string GetTutorialWorkspaceDirectory(MainViewModel viewModel)
    {
        var field = typeof(MainViewModel).GetField(
            "_workspaceDirectory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(viewModel) as string
            ?? throw new InvalidOperationException(
                "The tutorial workspace directory is unavailable.");
    }

    private static async Task PrepareGitSceneAsync(
        string workspaceDirectory,
        string locale)
    {
        var collectionPath = Path.Combine(
            workspaceDirectory,
            "collections",
            "getting-started.json");
        var content = await File.ReadAllTextAsync(collectionPath);
        var updatedName = locale == "tr-TR"
            ? "Ekip API sürümünü gözden geçir"
            : "Review teammate API release";
        var collection = JsonNode.Parse(content)?.AsObject()
            ?? throw new InvalidOperationException(
                "The tutorial collection is not valid JSON.");
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        await File.WriteAllTextAsync(
            collectionPath,
            collection.ToJsonString(serializerOptions));

        await RunGitAsync(workspaceDirectory, "init", "--initial-branch=main");
        await RunGitAsync(workspaceDirectory, "config", "user.name", "ReqMint Demo");
        await RunGitAsync(workspaceDirectory, "config", "user.email", "demo@reqmint.local");
        await RunGitAsync(workspaceDirectory, "add", "--all");
        await RunGitAsync(workspaceDirectory, "commit", "-m", "chore: seed local tutorial");

        var requests = collection["requests"]?.AsArray()
            ?? throw new InvalidOperationException(
                "The tutorial collection does not contain requests.");
        var releaseRequest = requests
            .Select(request => request?.AsObject())
            .SingleOrDefault(request => request?["url"]?.GetValue<string>()
                .EndsWith("/api/releases/current", StringComparison.Ordinal) == true)
            ?? throw new InvalidOperationException(
                "The tutorial collection did not contain the Git demo request.");
        releaseRequest["name"] = updatedName;
        await File.WriteAllTextAsync(
            collectionPath,
            collection.ToJsonString(serializerOptions));
    }

    private static async Task RunGitAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Git could not be started.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed ({process.ExitCode}): "
                + await standardError
                + await standardOutput);
        }

        await standardOutput;
        await standardError;
    }
}
#endif
