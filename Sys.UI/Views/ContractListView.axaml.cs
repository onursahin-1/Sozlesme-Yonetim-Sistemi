using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        var result = await RejectRequestDialog.ShowAsync(owner, card.Title, card.IsOwnRequest);
        if (result is null) return; // kullanıcı vazgeçti

        await vm.RejectRequestAsync(card, result.Note, result.AllowResubmit);
    }

    // Dosya seçim penceresi yalnızca kod-arkasından (TopLevel üzerinden) açılabildiği
    // için, seçilen yol ViewModel'e buradan iletiliyor.
    private async void OnExportExcelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContractListViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Excel Olarak Kaydet",
                SuggestedFileName = vm.SuggestedExportFileName,
                DefaultExtension = "xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel Çalışma Kitabı") { Patterns = new[] { "*.xlsx" } }
                },
            });

            if (file is null) return;

            var path = file.TryGetLocalPath();
            if (path is not null) await vm.ExportToExcelAsync(path);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "Excel'e aktarılırken bir hata oluştu: " + ex.Message;
        }
    }
}
