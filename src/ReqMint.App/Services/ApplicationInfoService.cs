using System.Reflection;
using System.Runtime.InteropServices;

namespace ReqMint.App.Services;

public sealed record ApplicationInfoSnapshot(
    string Version,
    string OperatingSystem,
    string Architecture,
    string Runtime)
{
    public string PlatformSummary => $"{OperatingSystem} · {Architecture}";
}

public interface IApplicationInfoService
{
    ApplicationInfoSnapshot Current { get; }
}

public sealed class RuntimeApplicationInfoService : IApplicationInfoService
{
    public RuntimeApplicationInfoService()
        : this(typeof(RuntimeApplicationInfoService).Assembly)
    {
    }

    internal RuntimeApplicationInfoService(Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(applicationAssembly);
        Current = new ApplicationInfoSnapshot(
            ReadVersion(applicationAssembly),
            RuntimeInformation.OSDescription.Trim(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription.Trim());
    }

    public ApplicationInfoSnapshot Current { get; }

    private static string ReadVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        var version = assembly.GetName().Version;
        return version is null
            ? "Development"
            : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }
}
