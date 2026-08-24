using System.Text;

namespace ReqMint.App.Services;

public interface ISupportInformationService
{
    string Create(ApplicationInfoSnapshot applicationInfo);
}

public sealed class SupportInformationService : ISupportInformationService
{
    public string Create(ApplicationInfoSnapshot applicationInfo)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);

        return new StringBuilder()
            .AppendLine("ReqMint support information")
            .Append("Version: ").AppendLine(applicationInfo.Version)
            .Append("Operating system: ").AppendLine(applicationInfo.OperatingSystem)
            .Append("Architecture: ").AppendLine(applicationInfo.Architecture)
            .Append("Runtime: ").AppendLine(applicationInfo.Runtime)
            .Append("Release channel: Community preview")
            .ToString();
    }
}
