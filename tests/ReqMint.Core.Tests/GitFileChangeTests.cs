using ReqMint.Core.Git;

namespace ReqMint.Core.Tests;

public sealed class GitFileChangeTests
{
    [Theory]
    [InlineData("DD")]
    [InlineData("AU")]
    [InlineData("UD")]
    [InlineData("UA")]
    [InlineData("DU")]
    [InlineData("AA")]
    [InlineData("UU")]
    public void IsConflict_RecognizesGitUnmergedStatuses(string status)
    {
        Assert.True(new GitFileChange("collections/conflicted.json", status).IsConflict);
    }

    [Theory]
    [InlineData(" M")]
    [InlineData("M ")]
    [InlineData("MM")]
    [InlineData("??")]
    public void IsConflict_RejectsOrdinaryChanges(string status)
    {
        Assert.False(new GitFileChange("collections/ordinary.json", status).IsConflict);
    }

    [Theory]
    [InlineData(" M", true)]
    [InlineData("??", true)]
    [InlineData("M ", false)]
    [InlineData("MM", false)]
    [InlineData("UU", false)]
    public void IsStageCandidate_RequiresOnlySafeWorkingTreeChanges(
        string status,
        bool expected)
    {
        var change = new GitFileChange("collections/orders.json", status);

        Assert.Equal(expected, change.IsStageCandidate);
    }
}
