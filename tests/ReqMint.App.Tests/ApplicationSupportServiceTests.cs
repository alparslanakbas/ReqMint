using ReqMint.App.Services;

namespace ReqMint.App.Tests;

public sealed class ApplicationSupportServiceTests
{
    [Fact]
    public void RuntimeApplicationInfo_ExposesReleaseSupportDetails()
    {
        var information = new RuntimeApplicationInfoService().Current;

        Assert.False(string.IsNullOrWhiteSpace(information.Version));
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
}
