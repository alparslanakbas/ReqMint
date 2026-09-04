using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.ViewModels;

public partial class RequestFileFieldViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string FileName { get; set; }

    [ObservableProperty]
    public partial string? LocalPath { get; set; }

    public RequestFileFieldViewModel(
        string name = "file",
        string fileName = "",
        string? localPath = null)
    {
        Name = name;
        FileName = fileName;
        LocalPath = localPath;
    }
}
