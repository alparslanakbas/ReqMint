using ReqMint.Core.Git;

namespace ReqMint.Core.Tests;

public sealed class ReqMintGitFileClassifierTests
{
    [Theory]
    [InlineData("reqmint.workspace.json")]
    [InlineData("REQMINT.WORKSPACE.JSON")]
    [InlineData("collections/commerce.json")]
    [InlineData("collections/nested/commerce.json")]
    [InlineData("environments\\local.json")]
    [InlineData("data/runner-sample.json")]
    public void IsManaged_AcceptsReqMintWorkspaceDocuments(string path)
    {
        Assert.True(ReqMintGitFileClassifier.IsManaged(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("README.md")]
    [InlineData("src/ReqMint.App/App.axaml.cs")]
    [InlineData("settings.json")]
    [InlineData("collections/readme.md")]
    [InlineData("collections/../secrets.json")]
    [InlineData("../collections/commerce.json")]
    [InlineData("C:/collections/commerce.json")]
    public void IsManaged_RejectsFilesOutsideReqMintWorkspaceScope(string? path)
    {
        Assert.False(ReqMintGitFileClassifier.IsManaged(path));
    }
}
