namespace ReqMint.App.Tests;

public sealed class DialogBrandingContractTests
{
    public static TheoryData<string> PromptFiles => new()
    {
        "AvaloniaUnsavedChangesPrompt.cs",
        "AvaloniaHistoryClearPrompt.cs",
        "AvaloniaCollectionRunHistoryClearPrompt.cs",
        "AvaloniaWindowClosePreferencePrompt.cs",
    };

    [Theory]
    [MemberData(nameof(PromptFiles))]
    public void Dialogs_InheritReqMintBrandingWithoutCreatingTaskbarEntries(string fileName)
    {
        var prompt = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "Services", fileName));

        Assert.Contains("Icon = owner.Icon", prompt, StringComparison.Ordinal);
        Assert.Contains("ShowInTaskbar = false", prompt, StringComparison.Ordinal);
        Assert.Contains("WindowStartupLocation.CenterOwner", prompt, StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ReqMint.slnx")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return Path.Combine([current.FullName, .. segments]);
    }
}
