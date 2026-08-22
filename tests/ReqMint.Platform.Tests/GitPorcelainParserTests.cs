using ReqMint.Platform.Git;

namespace ReqMint.Platform.Tests;

public sealed class GitPorcelainParserTests
{
    [Fact]
    public void Parse_ReadsBranchDivergenceAndFileChanges()
    {
        var output = "## main...origin/main [ahead 2, behind 1]\0 M src/App.cs\0?? docs/new file.md\0";

        var status = GitPorcelainParser.Parse("C:/repo", output);

        Assert.Equal("main", status.Branch);
        Assert.Equal(2, status.AheadBy);
        Assert.Equal(1, status.BehindBy);
        Assert.Collection(
            status.Changes,
            change => Assert.Equal(("src/App.cs", " M"), (change.Path, change.Status)),
            change => Assert.Equal(("docs/new file.md", "??"), (change.Path, change.Status)));
    }

    [Fact]
    public void Parse_HandlesInitialAndDetachedRepositories()
    {
        var initial = GitPorcelainParser.Parse("C:/repo", "## No commits yet on mint\0");
        var detached = GitPorcelainParser.Parse("C:/repo", "## HEAD (no branch)\0");

        Assert.Equal("mint", initial.Branch);
        Assert.False(initial.IsDetached);
        Assert.Equal("HEAD", detached.Branch);
        Assert.True(detached.IsDetached);
    }

    [Fact]
    public void Parse_ConsumesRenameSourceWithoutCreatingASecondChange()
    {
        var status = GitPorcelainParser.Parse(
            "C:/repo",
            "## main\0R  src/New Name.cs\0src/Old Name.cs\0");

        var change = Assert.Single(status.Changes);
        Assert.Equal("src/New Name.cs", change.Path);
        Assert.Equal("R ", change.Status);
    }

    [Fact]
    public void Parse_PreservesUnmergedStatusForConflictGuidance()
    {
        var status = GitPorcelainParser.Parse(
            "C:/repo",
            "## main\0UU environments/local.json\0");

        var change = Assert.Single(status.Changes);
        Assert.True(change.IsConflict);
        Assert.True(change.HasStagedChanges);
        Assert.True(change.HasWorkingTreeChanges);
    }
}
