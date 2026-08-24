namespace ReqMint.App.Tests;

public sealed class ReleaseReadinessContractTests
{
    [Fact]
    public void QualityWorkflow_CoversSupportedPlatformsAndDependencyAudit()
    {
        var workflow = File.ReadAllText(RepositoryPath(".github", "workflows", "quality-gates.yml"));

        Assert.Contains("permissions:", workflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("macos-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test ReqMint.slnx --configuration Release --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("package list --project ReqMint.slnx --vulnerable --include-transitive --format json --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("known vulnerability", workflow, StringComparison.Ordinal);
        Assert.Contains("Website build and audit", workflow, StringComparison.Ordinal);
        Assert.Contains("npm audit --audit-level=high", workflow, StringComparison.Ordinal);
        Assert.Contains("npm run build", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicPolicies_StateLocalFirstBehaviorAndPrivateSecurityReporting()
    {
        var privacy = File.ReadAllText(RepositoryPath("PRIVACY.md"));
        var security = File.ReadAllText(RepositoryPath("SECURITY.md"));
        var readiness = File.ReadAllText(RepositoryPath("docs", "RELEASE_READINESS.md"));

        Assert.Contains("does not require an account", privacy, StringComparison.Ordinal);
        Assert.Contains("does not provide a ReqMint-hosted synchronization service", privacy, StringComparison.Ordinal);
        Assert.Contains("private vulnerability reporting", security, StringComparison.Ordinal);
        Assert.Contains("Any failed required gate blocks publication", readiness, StringComparison.Ordinal);
        Assert.Contains("cannot be waived", readiness, StringComparison.Ordinal);
    }

    [Fact]
    public void CommercialPlan_KeepsPreviewFreeAndWebsiteOutOfCheckout()
    {
        var commercial = File.ReadAllText(RepositoryPath("docs", "COMMERCIAL_PLAN.md"));
        var website = File.ReadAllText(RepositoryPath("website", "README.md"));
        var dependabot = File.ReadAllText(RepositoryPath(".github", "dependabot.yml"));

        Assert.Contains("Public preview", commercial, StringComparison.Ordinal);
        Assert.Contains("USD 39.99 per year", commercial, StringComparison.Ordinal);
        Assert.Contains("does not collect payment details", commercial, StringComparison.Ordinal);
        Assert.Contains("does not process payments", website, StringComparison.Ordinal);
        Assert.Contains("package-ecosystem: npm", dependabot, StringComparison.Ordinal);
        Assert.Contains("directory: /website", dependabot, StringComparison.Ordinal);
    }

    [Fact]
    public void Website_ExposesPublicPrivacySecurityAndSupportRoutes()
    {
        var privacy = File.ReadAllText(RepositoryPath("website", "app", "privacy", "page.tsx"));
        var security = File.ReadAllText(RepositoryPath("website", "app", "security", "page.tsx"));
        var support = File.ReadAllText(RepositoryPath("website", "app", "support", "page.tsx"));

        Assert.Contains("requires no account", privacy, StringComparison.Ordinal);
        Assert.Contains("does not enable product analytics or crash telemetry", privacy, StringComparison.Ordinal);
        Assert.Contains("security/advisories/new", security, StringComparison.Ordinal);
        Assert.Contains("Never publish secrets", security, StringComparison.Ordinal);
        Assert.Contains("Remove authorization headers", support, StringComparison.Ordinal);
        Assert.Contains("GitHub issues", support, StringComparison.Ordinal);
    }

    [Fact]
    public void Website_ExposesArabicRtlCustomerJourneyAsNativeReviewDraft()
    {
        var arabicRoot = RepositoryPath("website", "app", "ar");
        var requiredPages = new[]
        {
            "page.tsx",
            Path.Combine("downloads", "page.tsx"),
            Path.Combine("docs", "page.tsx"),
            Path.Combine("privacy", "page.tsx"),
            Path.Combine("security", "page.tsx"),
            Path.Combine("support", "page.tsx")
        };

        Assert.All(requiredPages, page => Assert.True(File.Exists(Path.Combine(arabicRoot, page))));

        var arabicShell = File.ReadAllText(
            RepositoryPath("website", "app", "components", "ArabicShell.tsx"));
        var arabicGuides = File.ReadAllText(
            RepositoryPath("website", "app", "lib", "docs-ar.ts"));
        var expansion = File.ReadAllText(
            RepositoryPath("docs", "INTERNATIONAL_EXPANSION.md"));

        Assert.Contains("dir=\"rtl\"", arabicShell, StringComparison.Ordinal);
        Assert.Contains("/ar/privacy", arabicShell, StringComparison.Ordinal);
        Assert.Contains("security/advisories/new", File.ReadAllText(
            Path.Combine(arabicRoot, "security", "page.tsx")), StringComparison.Ordinal);
        Assert.Equal(8, CountOccurrences(arabicGuides, "slug: '"));
        Assert.Contains("native Arabic reviewer", expansion, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
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
