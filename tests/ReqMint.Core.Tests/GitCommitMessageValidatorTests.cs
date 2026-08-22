using ReqMint.Core.Git;

namespace ReqMint.Core.Tests;

public sealed class GitCommitMessageValidatorTests
{
    [Fact]
    public void CommitModels_DefaultToFailClosedStates()
    {
        Assert.False(new GitCommitPreflight().IsReady);
        Assert.Equal(
            GitCommitResultState.PreflightBlocked,
            new GitCommitResult().State);
    }

    [Theory]
    [InlineData("feat: add orders request")]
    [InlineData("Fix timeout")]
    [InlineData("abc")]
    public void IsValid_AcceptsConciseSingleLineMessages(string message)
    {
        Assert.True(GitCommitMessageValidator.IsValid(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("first line\nsecond line")]
    public void IsValid_RejectsAmbiguousOrUnsafeMessages(string? message)
    {
        Assert.False(GitCommitMessageValidator.IsValid(message));
    }

    [Fact]
    public void IsValid_RejectsMessagesLongerThanTheSummaryLimit()
    {
        Assert.False(GitCommitMessageValidator.IsValid(new string('a', 73)));
    }
}
