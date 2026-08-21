using Avalonia.Controls;
using Avalonia.Interactivity;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class ContractListView : UserControl
{
    public ContractListView()
    {
        InitializeComponent();
    }

    // Red diyaloğu bir pencere (Window) açtığı için komut yerine kod-arkasından
    // yürütülür — ApprovalQueueView/ContractDetailView ile aynı desen.
    private async void OnRejectRequestClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ContractCardViewModel card }) return;
        if (DataContext is not ContractListViewModel vm) return;
        if (vm.IsBusy) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var result = await RejectRequestDialog.ShowAsync(owner, card.Title);
        if (result is null) return; // kullanıcı vazgeçti

        await vm.RejectRequestAsync(card, result.Note, result.AllowResubmit);
    }
}
