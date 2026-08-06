using CommunityToolkit.Mvvm.ComponentModel;

namespace Sys.UI.ViewModels;

public partial class PlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;
}