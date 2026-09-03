using ReqMint.App.Services;

namespace ReqMint.App.Tests;

public sealed class ApplicationSupportServiceTests
{
    [Fact]
    public void RuntimeApplicationInfo_ExposesReleaseSupportDetails()
    {
        var information = new RuntimeApplicationInfoService().Current;

        Assert.Equal("1.0.1.0", information.Version);
        Assert.False(string.IsNullOrWhiteSpace(information.OperatingSystem));
        Assert.False(string.IsNullOrWhiteSpace(information.Architecture));
        Assert.False(string.IsNullOrWhiteSpace(information.Runtime));
        Assert.Contains(information.Architecture, information.PlatformSummary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://example.com/support")]
    [InlineData("file:///tmp/support")]
    [InlineData("https://user@example.com/support")]
    public async Task ExternalLinkService_RejectsUntrustedUriShapes(string value)
    {
        var service = new DesktopExternalLinkService();

        var opened = await service.OpenAsync(new Uri(value));

        Assert.False(opened);
    }

    [Fact]
    public void SupportInformation_ContainsOnlyExplicitReleaseFields()
    {
        var information = new ApplicationInfoSnapshot(
            "3.0.0-preview.1",
            "Test OS",
            "Arm64",
            ".NET 10.0.0");

        var report = new SupportInformationService().Create(information);

        Assert.Equal(
            "ReqMint support information\n"
            + "Version: 3.0.0-preview.1\n"
            + "Operating system: Test OS\n"
            + "Architecture: Arm64\n"
            + "Runtime: .NET 10.0.0\n"
            + "Release channel: Community preview",
            report.ReplaceLineEndings("\n"));
    }
}
