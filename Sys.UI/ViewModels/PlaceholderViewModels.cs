using CommunityToolkit.Mvvm.ComponentModel;

using Sys.UI.Localization;

namespace Sys.UI.ViewModels;

public partial class PlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ComingSoonText))]
    public partial string Title { get; set; } = string.Empty;

    // Cümle tek parça olarak çevriliyor. Eskiden ekran adı ve " — bu ekran yakında
    // eklenecek." iki ayrı <Run> idi; cümlenin yarısını XAML'de bırakmak, İngilizcede
    // kelime sırası değiştiğinde çalışmazdı.
    public string ComingSoonText => Strings.T("Shell.ComingSoon", Title);
}