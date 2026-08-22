using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.ViewModels;

public partial class EnvironmentVariableViewModel : ViewModelBase
{
    public EnvironmentVariableViewModel(
        string name = "",
        string value = "",
        bool isSecret = false)
    {
        Name = name;
        Value = value;
        IsSecret = isSecret;
        WasSecret = isSecret;
        OriginalName = name;
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial bool IsSecret { get; set; }

    public bool WasSecret { get; }

    public string OriginalName { get; }
}
