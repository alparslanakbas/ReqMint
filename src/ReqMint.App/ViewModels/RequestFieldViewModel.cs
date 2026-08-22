using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.ViewModels;

public partial class RequestFieldViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }

    public RequestFieldViewModel(string name = "", string value = "")
    {
        Name = name;
        Value = value;
    }
}
