namespace ReqMint.App.Services;

public sealed record PickedRequestFile(string Name, string LocalPath);

public interface IRequestFilePicker
{
    Task<PickedRequestFile?> PickAsync();
}
